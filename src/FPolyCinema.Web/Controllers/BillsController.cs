using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Quản lý hóa đơn (SPEC 3.6, UI A8, Lab 6 bài 1; ManagerOnly): danh sách lọc theo khoảng ngày, trạng thái, kênh, rạp, nhân viên, tìm theo mã;
/// chi tiết; in lại; hủy theo đúng quy tắc chuyển trạng thái (hóa đơn chưa thanh toán hủy ngay; đã thanh toán cần PIN quản lý). Không sửa số tiền.
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("admin/bills")]
public class BillsController : AppControllerBase
{
    private readonly BillQueryService _query;
    private readonly BillService _bills;
    private readonly ManagerPinService _pins;
    private readonly StatisticsService _stats;
    private readonly CinemaContextService _cinema;

    public BillsController(BillQueryService query, BillService bills, ManagerPinService pins, StatisticsService stats, CinemaContextService cinema)
    {
        _query = query;
        _bills = bills;
        _pins = pins;
        _stats = stats;
        _cinema = cinema;
    }

    /// <summary>Quản lý gắn một rạp chỉ xem, in, hủy hóa đơn của rạp mình; quản lý toàn chuỗi xem mọi rạp.</summary>
    private bool InScope(int cinemaId) => _cinema.BoundCinemaId is not int bound || bound == cinemaId;

    private BillListVm Filter(DateTime? from, DateTime? to, string? status, string? channel, int? staffId, string? q, int? page)
    {
        var range = _stats.Range(null, from, to, StatisticsService.Last7);
        var cinemaId = _cinema.CurrentCinemaId;
        var p = Paging.ClampPage(page);
        var items = _query.Search(range, status, channel, cinemaId, staffId, q, p, Paging.DefaultPageSize, out var total);
        return new BillListVm
        {
            Range = range, Status = status, Channel = channel, StaffId = staffId, Query = q, CinemaName = _cinema.Current?.Name ?? "Toàn chuỗi",
            Result = new PagedResult<Entities.ReadModels.BillListRow>(items, p, Paging.DefaultPageSize, total),
            Staff = BillQueryService.Staff(cinemaId),
            HasFilter = from != null || to != null || status != null || channel != null || staffId != null || !string.IsNullOrWhiteSpace(q),
        };
    }

    /// <summary>Danh sách hóa đơn (nhận ?partial=1 để tải lại vùng bảng).</summary>
    [HttpGet("")]
    public IActionResult Index(DateTime? from, DateTime? to, string? status, string? channel, int? staffId, string? q, int? page)
    {
        var vm = Filter(from, to, status, channel, staffId, q, page);
        return IsPartialRequest ? PartialView("_Table", vm) : View(vm);
    }

    /// <summary>Xuất CSV đúng bộ lọc đang chọn.</summary>
    [HttpGet("export")]
    public IActionResult Export(DateTime? from, DateTime? to, string? status, string? channel, int? staffId, string? q)
    {
        var range = _stats.Range(null, from, to, StatisticsService.Last7);
        var rows = _query.All(range, status, channel, _cinema.CurrentCinemaId, staffId, q);
        var csv = StatisticsService.Csv(
            new[] { "Mã hóa đơn", "Thời gian tạo", "Thời gian thanh toán", "Kênh", "Rạp", "Nhân viên", "Khách hàng", "Số vé", "Số món", "Tổng tiền", "Trạng thái", "Lý do hủy" },
            rows.Select(r => new object?[]
            {
                r.Code, r.CreatedAt, r.PaidAt, StatusDisplay.Of(StatusDisplay.Channel, r.Channel).Label, r.CinemaName, r.StaffName, r.CustomerName, r.TicketCount, r.ItemCount, r.Total,
                StatusDisplay.Of(StatusDisplay.Bill, r.Status).Label, r.CancelReason == null ? null : StatusDisplay.Of(StatusDisplay.CancelReasons, r.CancelReason).Label,
            }));
        return File(csv, "text/csv; charset=utf-8", $"hoa-don_{range.From:yyyyMMdd}-{range.To:yyyyMMdd}.csv");
    }

    /// <summary>Chi tiết hóa đơn: vé, bắp nước, giảm giá, VAT, tổng; nhân viên, ca, thời gian; thanh toán, hoàn tiền và phê duyệt.</summary>
    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        var d = _query.Detail(id);
        if (d == null || !InScope(d.Bill.CinemaId)) return NotFoundPage("Không tìm thấy hóa đơn.");
        return View(new BillDetailsVm { Data = d, Supervisors = BillQueryService.Supervisors(d.Bill.CinemaId), CurrentUserId = CurrentUserId });
    }

    /// <summary>In lại hóa đơn (trang in 80mm).</summary>
    [HttpGet("{id:int}/print")]
    public IActionResult Print(int id)
    {
        var d = _query.Detail(id);
        if (d == null || !InScope(d.Bill.CinemaId)) return NotFoundPage("Không tìm thấy hóa đơn.");
        return View("~/Views/Pos/Print.cshtml", new PrintVm
        {
            Bill = d.Bill, CinemaName = d.Row.CinemaName, CinemaAddress = CinemaDAL.GetById(d.Bill.CinemaId)?.Address, StaffName = d.Row.StaffName, CustomerName = d.CustomerName,
            Tickets = d.Tickets.Where(t => t.CheckInStatus != TicketCheckInStatus.Swapped).ToList(), Lines = d.Lines, Payments = d.Payments, VatTotal = d.VatTotal,
            AutoPrint = true, BackUrl = $"/admin/bills/{id}",
        });
    }

    /// <summary>
    /// Hủy hóa đơn theo quy tắc chuyển trạng thái: chưa thanh toán thì CANCELLED_BEFORE_PAYMENT; đã thanh toán thì VOIDED_BY_POS, cần lý do và PIN của quản lý.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, string? reason, string? pin)
    {
        var bill = BillDAL.GetById(id);
        if (bill == null || !InScope(bill.CinemaId)) return ApiFail(ErrorCodes.NotFound, "Không tìm thấy hóa đơn.");
        if (bill.Status == BillStatus.Waiting)
        {
            var code = await _bills.CancelWaiting(id, Roles.Manager, bill.CinemaId);
            return ApiOk(new { message = $"Đã hủy hóa đơn {code}." });
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) throw FieldErrors.Of("reason", "Bạn ghi lý do hủy (ít nhất 5 ký tự).");
        if (string.IsNullOrEmpty(pin)) return ApiFail(ErrorCodes.ApprovalRequired, "Hủy hóa đơn đã thanh toán cần nhập PIN quản lý để duyệt.");
        var approver = _pins.Verify(CurrentUserId, pin, bill.CinemaId);
        var shift = PosShiftDAL.GetOpenByUser(CurrentUserId);
        var done = await _bills.VoidPaid(id, reason, approver, CurrentUserId, bill.CinemaId, shift?.CinemaId == bill.CinemaId ? shift.Id : null);
        return ApiOk(new { message = $"Đã hủy hóa đơn {done} và hoàn tiền theo phương thức gốc." });
    }
}
