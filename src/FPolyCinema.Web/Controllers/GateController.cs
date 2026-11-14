using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Máy soát vé (SPEC 3.8, UI P3; GateStaff): quét QR hóa đơn hoặc QR vé, thẻ kết quả xanh, vàng, đỏ; cho vào toàn bộ hoặc từng ghế; nút "Đã dọn xong"
/// cho phòng đang dọn. Sĩ số phát qua kênh 4, trạng thái phòng qua kênh 6.
/// </summary>
[Authorize(Policy = Policies.GateStaff)]
[Route("pos/gate-inspector")]
public class GateController : AppControllerBase
{
    private readonly GateService _gate;
    private readonly ShowtimeOpsService _ops;
    private readonly CinemaContextService _cinema;

    public GateController(GateService gate, ShowtimeOpsService ops, CinemaContextService cinema)
    {
        _gate = gate;
        _ops = ops;
        _cinema = cinema;
    }

    /// <summary>Rạp của máy soát vé: rạp của nhân viên; quản lý toàn chuỗi dùng rạp đang chọn (hoặc rạp đầu tiên).</summary>
    private int CinemaId => _cinema.CurrentCinemaId ?? _cinema.Cinemas.First().Id;

    [HttpGet("")]
    public IActionResult Index(int? auditoriumId)
    {
        var cid = CinemaId;
        var rooms = AuditoriumDAL.GetActiveByCinema(cid);
        return View(new GateVm
        {
            CinemaId = cid, CinemaName = _cinema.Cinemas.FirstOrDefault(c => c.Id == cid)?.Name ?? string.Empty, Auditoriums = rooms,
            AuditoriumId = rooms.Any(r => r.Id == auditoriumId) ? auditoriumId : null, Cleaning = _ops.Cleaning(cid),
        });
    }

    /// <summary>Quét mã: trả thẻ kết quả (ok, warn, reject) kèm vé của hóa đơn.</summary>
    [HttpPost("scan")]
    public IActionResult Scan(string? code, int? auditoriumId) => ApiOk(_gate.Scan(code, CinemaId, auditoriumId));

    /// <summary>Cho vào: ticketIds rỗng là mọi vé còn chờ; warningsConfirmed khi thẻ vàng.</summary>
    [HttpPost("admit")]
    public async Task<IActionResult> Admit(int billId, [FromForm] int[]? ticketIds, bool warningsConfirmed) =>
        ApiOk(await _gate.Admit(billId, ticketIds ?? Array.Empty<int>(), warningsConfirmed, CurrentUserId, CinemaId));

    /// <summary>Nút "Đã dọn xong": suất CLEANING_IN_PROGRESS chuyển COMPLETED; suất kế tiếp trong phòng chuyển BOARDING nếu đã tới giờ.</summary>
    [HttpPost("cleaned")]
    public async Task<IActionResult> Cleaned(int showtimeId)
    {
        var changes = await _ops.MarkCleaned(showtimeId, CinemaId);
        var next = changes.Skip(1).FirstOrDefault(c => c.To == OperationalStatus.Boarding);
        return ApiOk(new
        {
            message = next != null ? "Đã xác nhận dọn xong. Suất kế tiếp đã mời khách vào rạp." : "Đã xác nhận dọn xong phòng.",
            cleaning = _ops.Cleaning(CinemaId).Select(c => new { c.ShowtimeId, c.AuditoriumName, c.MovieTitle, end = Infrastructure.Fmt.Time(c.EndTime) }),
        });
    }

    /// <summary>Danh sách phòng đang dọn (tải lại sau khi kết nối lại hoặc khi có SHOWTIME_OPS_CHANGED).</summary>
    [HttpGet("cleaning")]
    public IActionResult Cleaning() =>
        ApiOk(_ops.Cleaning(CinemaId).Select(c => new { c.ShowtimeId, c.AuditoriumName, c.MovieTitle, end = Infrastructure.Fmt.Time(c.EndTime) }));
}
