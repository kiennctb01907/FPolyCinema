using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Màn hình sảnh, không cần đăng nhập (theme cinema, 1920 × 1080): bảng gọi số /lobby/pickup-board (UI P5, kênh 5 chỉ nhận) và bảng sảnh
/// /lobby/digital-signage (UI P6, kênh 6). Màn hình phụ của quầy /pos/customer-display (UI P2, kênh 2 chỉ nhận). ?cinemaId= chọn rạp (mặc định rạp đang chọn).
/// </summary>
[AllowAnonymous]
public class LobbyController : AppControllerBase
{
    private readonly CinemaContextService _cinema;
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly CustomerDisplayService _display;

    public LobbyController(CinemaContextService cinema, IClock clock, CinemaRules rules, CustomerDisplayService display)
    {
        _cinema = cinema;
        _clock = clock;
        _rules = rules;
        _display = display;
    }

    private int Cinema(int? cinemaId) => cinemaId is int id && _cinema.Cinemas.Any(c => c.Id == id) ? id : _cinema.CurrentCinemaId ?? _cinema.Cinemas.First().Id;

    private string CinemaName(int id) => _cinema.Cinemas.FirstOrDefault(c => c.Id == id)?.Name ?? string.Empty;

    /// <summary>Bảng gọi số (UI P5): "Đang chuẩn bị" và "Mời nhận đồ", kèm đồng hồ và logo.</summary>
    [HttpGet("lobby/pickup-board")]
    public IActionResult PickupBoard(int? cinemaId)
    {
        int id = Cinema(cinemaId);
        return View(new LobbyVm { CinemaId = id, CinemaName = CinemaName(id), Orders = KdsService.Orders(id), Now = _clock.Now });
    }

    [HttpGet("lobby/pickup-board/data")]
    public IActionResult PickupData(int? cinemaId) =>
        ApiOk(KdsService.Orders(Cinema(cinemaId)).Select(o => new { o.BillId, o.PickupNumber, o.FnbStatus }));

    private List<Entities.ReadModels.SignageRow> Signage(int id)
    {
        var now = _clock.Now;
        return ShowtimeDAL.Signage(id, now, PricingCalculator.OperatingDate(now).AddDays(1).Add(PricingCalculator.NightCutoff));
    }

    /// <summary>Bảng sảnh (UI P6): các suất sắp chiếu với giờ, phim, phòng, trạng thái; tự cuộn; dòng chữ chạy nhắc quy định độ tuổi.</summary>
    [HttpGet("lobby/digital-signage")]
    public IActionResult DigitalSignage(int? cinemaId)
    {
        int id = Cinema(cinemaId);
        return View(new LobbyVm
        {
            CinemaId = id, CinemaName = CinemaName(id), Showtimes = Signage(id), Now = _clock.Now,
            Ticker = "Phim T13, T16, T18 chỉ dành cho người xem đủ tuổi – vui lòng mang CCCD hoặc VNeID. Phim K: trẻ dưới 13 tuổi xem cùng cha mẹ hoặc người giám hộ. " +
                     $"Trẻ em dưới 13 tuổi không vào suất kết thúc từ {_rules.ChildU13LatestEnd:hh\\:mm}, dưới 16 tuổi không vào suất kết thúc từ {_rules.ChildU16LatestEnd:hh\\:mm}.",
        });
    }

    /// <summary>Bảng sảnh dạng HTML các dòng (tải lại khi có SHOWTIME_OPS_CHANGED hoặc mỗi phút).</summary>
    [HttpGet("lobby/digital-signage/rows")]
    public IActionResult SignageRows(int? cinemaId) => PartialView("~/Views/Lobby/_SignageRows.cshtml", new LobbyVm { CinemaId = Cinema(cinemaId), Showtimes = Signage(Cinema(cinemaId)), Now = _clock.Now });

    // ------------------------------------------------------------------ màn hình phụ của quầy (UI P2)

    /// <summary>Màn hình phụ của quầy: lúc chờ chạy slideshow poster; theo dõi máy POS qua kênh 2 (tham gia ẩn danh, chỉ nhận).</summary>
    [HttpGet("pos/customer-display")]
    public async Task<IActionResult> CustomerDisplay(string? terminal, int? cinemaId)
    {
        int id = Cinema(cinemaId);
        var code = ShiftService.Terminals().Contains(terminal ?? string.Empty) ? terminal! : ShiftService.Terminals()[0];
        var posters = MovieDAL.GetActive(50).Where(m => m.AgeRating != AgeRating.C).OrderByDescending(m => m.ReleaseDate).Take(8)
            .Select(m => (m.Title, m.PosterUrl, m.AgeRating)).ToList();
        return View(new CustomerDisplayVm
        {
            CinemaId = id, CinemaName = CinemaName(id), Terminal = code, TerminalKey = ShiftService.TerminalKey(id, code), Posters = posters,
            State = await _display.State(id, code),
        });
    }

    /// <summary>Trạng thái hiện tại của máy POS (tải lại sau khi kết nối lại).</summary>
    [HttpGet("pos/customer-display/state")]
    public async Task<IActionResult> CustomerDisplayState(string? terminal, int? cinemaId) => ApiOk(await _display.State(Cinema(cinemaId), terminal ?? string.Empty));

    /// <summary>Sơ đồ ghế lớn chỉ xem của suất; ghế hóa đơn đang chọn sáng lên.</summary>
    [HttpGet("pos/customer-display/seat-map")]
    public IActionResult CustomerDisplaySeatMap(int showtimeId, int? billId, [FromServices] SeatMapService seatMaps)
    {
        var info = SeatMapService.Info(showtimeId);
        if (info == null) return NotFoundPage("Không tìm thấy suất chiếu.");
        return PartialView("~/Views/Lobby/_DisplaySeatMap.cshtml", SeatMapVm.From(seatMaps.Build(showtimeId, billId)));
    }
}
