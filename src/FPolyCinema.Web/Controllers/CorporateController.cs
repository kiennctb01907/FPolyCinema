using System.Text;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Bán vé doanh nghiệp (UC40, SPEC 3.10 mục 3; ManagerOnly; UI A2, A3): danh sách hợp đồng; thuê trọn suất (suất chưa bán vé nào, IsPrivate, hóa đơn CORPORATE
/// cho toàn bộ ghế theo giá hợp đồng); phát hành loạt voucher FREE_TICKET kèm danh sách mã và xuất CSV.
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("admin/corporate")]
public class CorporateController : AppControllerBase
{
    private readonly OpsManagementService _mgmt;
    private readonly CinemaContextService _cinema;
    private readonly IClock _clock;

    public CorporateController(OpsManagementService mgmt, CinemaContextService cinema, IClock clock)
    {
        _mgmt = mgmt;
        _cinema = cinema;
        _clock = clock;
    }

    [HttpGet("")]
    public IActionResult Index() => View(new CorporateListVm { Rows = CorporateBookingDAL.Rows(_cinema.CurrentCinemaId, 200) });

    private CorporateFormVm FormVm(CorporateInput input)
    {
        var now = _clock.Now;
        var shows = ShowtimeDAL.ForCinemaRange(_cinema.CurrentCinemaId, now, now.AddDays(14)).Where(s => s.Sold == 0 && s.StartTime > now).ToList();
        return new CorporateFormVm { Input = input, Showtimes = shows, Now = now };
    }

    [HttpGet("create")]
    public IActionResult Create(string? type) => View("Form", FormVm(new CorporateInput
    {
        Type = type == CorporateType.VoucherBatch ? CorporateType.VoucherBatch : CorporateType.PrivateScreening,
        ValidUntil = _clock.Now.Date.AddMonths(3).AddHours(23).AddMinutes(59), VoucherCount = 20,
    }));

    [HttpPost("create")]
    public async Task<IActionResult> Create(CorporateInput input)
    {
        try
        {
            int id = input.Type == CorporateType.VoucherBatch
                ? _mgmt.VoucherBatch(input, CurrentUserId)
                : await _mgmt.PrivateScreening(input, CurrentUserId, CurrentRole ?? Roles.Manager, _cinema.BoundCinemaId);
            Toast("success", input.Type == CorporateType.VoucherBatch ? $"Đã phát hành {input.VoucherCount} voucher." : "Đã tạo hợp đồng thuê trọn suất.");
            return Redirect($"/admin/corporate/{id}");
        }
        catch (BusinessRuleException ex) when (ex.Code is ErrorCodes.ValidationFailed or ErrorCodes.SeatTaken)
        {
            AddErrors(ex);
            return View("Form", FormVm(input));
        }
    }

    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        var row = CorporateBookingDAL.Rows(null, 1000).FirstOrDefault(r => r.Booking.Id == id);
        if (row == null) return NotFoundPage("Không tìm thấy hợp đồng.");
        return View(new CorporateDetailVm { Row = row, Vouchers = VoucherDAL.ByCorporate(id) });
    }

    /// <summary>Xuất danh sách mã voucher của hợp đồng (CSV UTF-8 có BOM để Excel đọc đúng tiếng Việt).</summary>
    [HttpGet("{id:int}/vouchers.csv")]
    public IActionResult Export(int id)
    {
        var cb = CorporateBookingDAL.GetById(id);
        if (cb == null) return NotFoundPage("Không tìm thấy hợp đồng.");
        var sb = new StringBuilder("Mã voucher,Loại,Hạn dùng,Đã dùng\r\n");
        foreach (var v in VoucherDAL.ByCorporate(id))
            sb.Append($"{v.Code},Tặng 1 vé ghế thường hoặc VIP,{Fmt.DateTime(v.ValidUntil)},{(v.UsedCount > 0 ? "Có" : "Chưa")}\r\n");
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", $"voucher-doanh-nghiep-{id}.csv");
    }
}
