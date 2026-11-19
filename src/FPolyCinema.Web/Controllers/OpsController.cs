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
/// Dashboard vận hành của quản lý ca (SupervisorOrManager, UI A7): yêu cầu phê duyệt đang chờ (duyệt hoặc từ chối từ xa), sĩ số trực tiếp các suất đang
/// chạy, suất trễ báo đỏ, phòng đang dọn, nhắc giải phóng ghế nội bộ, phiếu hủy hàng chờ duyệt, hủy suất do sự cố.
/// </summary>
[Authorize(Policy = Policies.SupervisorOrManager)]
[Route("ops")]
public class OpsController : AppControllerBase
{
    private readonly ApprovalService _approvals;
    private readonly ShowtimeOpsService _ops;
    private readonly OpsManagementService _mgmt;
    private readonly InventoryService _inventory;
    private readonly CinemaContextService _cinema;
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly RealtimeNotifier _notifier;

    public OpsController(ApprovalService approvals, ShowtimeOpsService ops, OpsManagementService mgmt, InventoryService inventory, CinemaContextService cinema,
        IClock clock, CinemaRules rules, RealtimeNotifier notifier)
    {
        _approvals = approvals;
        _ops = ops;
        _mgmt = mgmt;
        _inventory = inventory;
        _cinema = cinema;
        _clock = clock;
        _rules = rules;
        _notifier = notifier;
    }

    private int? CinemaId => _cinema.CurrentCinemaId;

    private string Scope => _cinema.Current?.Name ?? "Toàn chuỗi";

    /// <summary>Dashboard vận hành (nhận ?partial=1 để tải lại vùng nội dung khi có sự kiện thời gian thực).</summary>
    [HttpGet("")]
    public IActionResult Index([FromServices] OpsDashboardPages pages)
    {
        var vm = pages.Build();
        return IsPartialRequest ? PartialView("_OpsBody", vm) : View(vm);
    }

    /// <summary>Khối vận hành của dashboard /admin (UI A1-05): yêu cầu phê duyệt đang chờ và sĩ số trực tiếp; ops.js tải lại khi có sự kiện.</summary>
    [HttpGet("live")]
    public IActionResult Live([FromServices] OpsDashboardPages pages) => PartialView("_OpsLive", pages.Build());

    /// <summary>Duyệt yêu cầu hủy hóa đơn từ xa.</summary>
    [HttpPost("approvals/{id:int}/approve")]
    public async Task<IActionResult> Approve(int id) => ApiOk(new { message = await _approvals.Decide(id, true, CurrentUserId) });

    /// <summary>Từ chối yêu cầu hủy hóa đơn từ xa.</summary>
    [HttpPost("approvals/{id:int}/reject")]
    public async Task<IActionResult> Reject(int id) => ApiOk(new { message = await _approvals.Decide(id, false, CurrentUserId) });

    /// <summary>Yêu cầu đang chờ (popup A7 tải lại sau khi kết nối lại).</summary>
    [HttpGet("approvals/pending")]
    public IActionResult Pending() => ApiOk(_approvals.Pending(CinemaId).Select(ApprovalService.Payload));

    /// <summary>Giải phóng ghế nội bộ của một suất (mọi ghế nội bộ còn giữ).</summary>
    [HttpPost("house-seats/{showtimeId:int}/release")]
    public async Task<IActionResult> ReleaseHouseSeats(int showtimeId)
    {
        int n = await _mgmt.ReleaseHouseSeats(showtimeId, null, CurrentUserId, _cinema.BoundCinemaId);
        return ApiOk(new { message = $"Đã giải phóng {n} ghế nội bộ để mở bán." });
    }

    // ------------------------------------------------------------------ phiếu hủy hàng (UC26)

    [HttpGet("wastage")]
    public IActionResult Wastage(string? status)
    {
        status = status is WastageStatus.Approved or WastageStatus.Rejected or WastageStatus.Pending ? status : null;
        return View(new WastageListVm { Logs = WastageLogDAL.Search(CinemaId, status, 100), Status = status, CanDecide = true });
    }

    [HttpGet("wastage/{id:int}")]
    public IActionResult WastageDetail(int id)
    {
        var row = WastageLogDAL.GetRow(id);
        if (row == null || (_cinema.BoundCinemaId != null && row.Log.CinemaId != _cinema.BoundCinemaId)) return NotFoundPage("Không tìm thấy phiếu hủy hàng.");
        return View(new WastageDetailVm { Row = row, Items = WastageLogItemDAL.RowsByLog(id), CanDecide = WastageRules.CanDecide(row.Log.Status) });
    }

    /// <summary>Duyệt phiếu hủy hàng: ActualStock giảm (WASTAGE), ghi AuditLogs, phát cảnh báo kho.</summary>
    [HttpPost("wastage/{id:int}/approve")]
    public async Task<IActionResult> ApproveWastage(int id) => await DecideWastage(id, true);

    [HttpPost("wastage/{id:int}/reject")]
    public async Task<IActionResult> RejectWastage(int id) => await DecideWastage(id, false);

    private async Task<IActionResult> DecideWastage(int id, bool approve)
    {
        var row = WastageLogDAL.GetRow(id) ?? throw CategoryService.NotFound("Không tìm thấy phiếu hủy hàng.");
        var before = InventoryService.Snapshot(row.Log.CinemaId);
        _inventory.DecideWastage(id, approve, CurrentUserId, _cinema.BoundCinemaId, _clock.Now);
        if (approve) await InventoryService.PublishAlerts(_notifier, before);
        return ApiOk(new { message = approve ? $"Đã duyệt phiếu #{id}, tồn kho đã trừ." : $"Đã từ chối phiếu #{id}." });
    }

    // ------------------------------------------------------------------ hủy suất do sự cố (UC27)

    private List<Entities.ReadModels.LiveOccupancyRow> IncidentShowtimes()
    {
        var now = _clock.Now;
        return ShowtimeDAL.ForCinemaRange(CinemaId, now.AddHours(-4), now.AddDays(7)).Where(s => s.EndTime > now).ToList();
    }

    /// <summary>Hủy suất do sự cố: chọn suất, nhập lý do, gõ lại mã suất để xác nhận.</summary>
    [HttpGet("incident")]
    public IActionResult Incident(int? showtimeId) => View(new IncidentVm { Showtimes = IncidentShowtimes(), ShowtimeId = showtimeId });

    [HttpPost("incident")]
    public async Task<IActionResult> Incident(int? showtimeId, string? reason, string? confirmCode)
    {
        if (showtimeId is not int sid) return View(new IncidentVm { Showtimes = IncidentShowtimes(), Reason = reason, Errors = new() { ["showtimeId"] = "Bạn chọn suất cần hủy." } });
        var row = ShowtimeDAL.Occupancy(sid);
        try
        {
            var summary = await _mgmt.CancelShowtime(sid, reason, confirmCode, CurrentUserId, CurrentRole ?? string.Empty, _cinema.BoundCinemaId);
            return View(new IncidentVm { Showtimes = IncidentShowtimes(), Summary = summary, Cancelled = row });
        }
        catch (BusinessRuleException ex) when (ex.HttpStatus < 500)
        {
            // Lỗi theo ô (VALIDATION_FAILED) hiện dưới từng ô; lỗi nghiệp vụ khác (ví dụ SHIFT_NOT_OPEN khi cần hoàn tiền mặt) hiện ở đầu form, giữ nguyên dữ liệu đã nhập
            return View(new IncidentVm
            {
                Showtimes = IncidentShowtimes(), ShowtimeId = sid, Reason = reason,
                Errors = ex.Code == ErrorCodes.ValidationFailed && ex.Details is Dictionary<string, string> fields ? fields : new Dictionary<string, string> { [""] = ex.Message },
            });
        }
    }
}
