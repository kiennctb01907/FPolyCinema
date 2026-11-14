using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Web khách xem phim, lịch chiếu, thực đơn (UC06; UI C1, C2): danh sách phim, chi tiết phim kèm lịch chiếu 7 ngày, trang lịch chiếu lọc theo rạp,
/// ngày, định dạng, ngôn ngữ; thực đơn bắp nước; dữ liệu cho thanh mua vé nhanh. Khách vãng lai xem được.
/// </summary>
[AllowAnonymous]
public class CustomerController : AppControllerBase
{
    private readonly CustomerCatalogPages _catalog;
    private readonly CinemaContextService _cinema;

    public CustomerController(CustomerCatalogPages catalog, CinemaContextService cinema)
    {
        _catalog = catalog;
        _cinema = cinema;
    }

    /// <summary>Danh sách phim: tab Đang chiếu, Sắp chiếu.</summary>
    [HttpGet("/movies")]
    public IActionResult Movies(string? tab)
    {
        var (now, soon) = _catalog.Movies();
        return View(new MoviesVm
        {
            Tab = tab == "soon" ? "soon" : "now",
            NowShowing = now.Select(m => CustomerCatalogPages.Card(m, true)).ToList(),
            ComingSoon = soon.Select(m => CustomerCatalogPages.Card(m, false) with { Subline = $"Khởi chiếu {Fmt.Date(m.ReleaseDate)}" }).ToList(),
        });
    }

    /// <summary>Chi tiết phim C2: thông tin, nhãn tuổi kèm câu giải thích, cảnh báo nội dung, lịch chiếu 7 ngày lọc theo định dạng và ngôn ngữ.</summary>
    [HttpGet("/movies/{id:int}")]
    public IActionResult Movie(int id, DateTime? date, string? format, string? lang, int? cinemaId)
    {
        var vm = _catalog.MovieDetail(id, date, format, lang, CinemaFilter(cinemaId));
        if (vm == null) return NotFoundPage("Không tìm thấy phim.");
        return IsPartialRequest ? PartialView("_Schedule", vm.Groups) : View(vm);
    }

    /// <summary>Bộ lọc rạp: 0 là tất cả rạp, không gửi thì dùng rạp đang chọn ở đầu trang.</summary>
    private int? CinemaFilter(int? cinemaId) => cinemaId == 0 ? null : cinemaId ?? _cinema.CurrentCinemaId;

    /// <summary>Lịch chiếu của mọi phim theo rạp, ngày, định dạng, ngôn ngữ.</summary>
    [HttpGet("/showtimes")]
    public IActionResult Showtimes(DateTime? date, string? format, string? lang, int? cinemaId)
    {
        var vm = _catalog.Showtimes(date, format, lang, CinemaFilter(cinemaId));
        return View(vm);
    }

    /// <summary>Thực đơn bắp nước theo rạp đang chọn (món hết nguyên liệu hiện "Tạm hết").</summary>
    [HttpGet("/menu")]
    public IActionResult Menu(int? categoryId) => View(_catalog.Menu(_cinema.CurrentCinemaId, categoryId));

    /// <summary>Dữ liệu thanh mua vé nhanh (JSON): phim theo rạp, ngày có suất, suất theo ngày.</summary>
    [HttpGet("/quick-buy")]
    public IActionResult QuickBuy(int? cinemaId, int? movieId, DateTime? date) => ApiOk(_catalog.QuickBuy(cinemaId, movieId, date));
}

/// <summary>
/// Đặt vé online C3 (UC07, UC08, UC09): xem sơ đồ ghế không cần đăng nhập; giữ ghế, bắp nước, ưu đãi, thanh toán cần tài khoản khách hàng.
/// </summary>
[Authorize(Policy = Policies.CustomerOnly)]
[Route("booking")]
public class BookingController : AppControllerBase
{
    private readonly OnlineBookingService _booking;
    private readonly SeatSuggestService _suggest;
    private readonly CustomerCatalogPages _catalog;
    private readonly SeatMapService _seatMaps;
    private readonly BillService _bills;
    private readonly IClock _clock;
    private readonly CinemaRules _rules;

    public BookingController(OnlineBookingService booking, SeatSuggestService suggest, CustomerCatalogPages catalog, SeatMapService seatMaps, BillService bills,
        IClock clock, CinemaRules rules)
    {
        _booking = booking;
        _suggest = suggest;
        _catalog = catalog;
        _seatMaps = seatMaps;
        _bills = bills;
        _clock = clock;
        _rules = rules;
    }

    private bool IsCustomer => CurrentRole == Roles.Customer;

    /// <summary>Trang đặt vé 4 bước của một suất; khách chưa đăng nhập vẫn xem được sơ đồ ghế.</summary>
    [AllowAnonymous]
    [HttpGet("{showtimeId:int}")]
    public async Task<IActionResult> Index(int showtimeId, string? persona, int? qty)
    {
        var info = OnlineBookingService.OnlineInfo(showtimeId);
        if (info == null || info.Movie.AgeRating == AgeRating.C) return NotFoundPage("Không tìm thấy suất chiếu.");
        var bill = IsCustomer ? await _booking.CurrentFor(CurrentUserId, showtimeId) : null;
        var user = IsCustomer ? FPolyCinema.Web.DAL.UserDAL.GetById(CurrentUserId) : null;
        var cart = bill == null ? null : _bills.BuildCart(bill, new List<string>());
        var now = _clock.Now;
        var vm = new BookingVm
        {
            Info = info,
            CinemaName = FPolyCinema.Web.DAL.CinemaDAL.GetById(info.Auditorium.CinemaId)?.Name ?? string.Empty,
            SeatMap = SeatMapVm.From(_seatMaps.Build(showtimeId, bill?.Id), showLegend: true),
            Cart = cart,
            LoggedIn = User.Identity?.IsAuthenticated == true,
            IsCustomer = IsCustomer,
            Drinks = _catalog.BookingDrinks(info.Auditorium.CinemaId, cart),
            Confirmations = OnlineAgeRules.Confirmations(info.Movie.AgeRating, info.Showtime.StartTime, info.Showtime.EndTime, _rules.ChildU13LatestEnd, _rules.ChildU16LatestEnd),
            CurfewNotice = OnlineAgeRules.CurfewNotice(info.Showtime.StartTime, info.Showtime.EndTime, _rules.ChildU13LatestEnd, _rules.ChildU16LatestEnd),
            WalletBalance = user?.WalletBalance ?? 0,
            LoyaltyPoints = user?.LoyaltyPoints ?? 0,
            PointValueVnd = _rules.PointValueVnd,
            Persona = SeatPersona.All.Contains(persona) ? persona! : SeatPersona.BestViewAudio,
            Quantity = Math.Clamp(qty ?? 2, 1, 10),
            OtherTimes = _catalog.OtherTimes(info),
            SalesOpen = SeatHoldRules.IsSalesOpen(info.Showtime.FeatureStartTime, info.Showtime.OperationalStatus, BillChannel.Online, now, _rules),
            GoodClusterScore = _rules.GoodClusterScore,
            SeatHoldMinutes = _rules.SeatHoldMinutes,
        };
        return View(vm);
    }

    /// <summary>Gợi ý ghế AI (SPEC 4.1): điểm theo persona, tối đa 3 cụm, gợi ý chuyển suất. Khách chưa đăng nhập cũng xem được.</summary>
    [AllowAnonymous]
    [HttpGet("{showtimeId:int}/suggest")]
    public async Task<IActionResult> Suggest(int showtimeId, string? persona, int qty = 2)
    {
        var bill = IsCustomer ? await _booking.CurrentFor(CurrentUserId, showtimeId) : null;
        return ApiOk(_suggest.Suggest(showtimeId, persona, qty, bill?.Id));
    }

    /// <summary>Sơ đồ ghế hiện tại (tải lại sau khi kết nối lại).</summary>
    [AllowAnonymous]
    [HttpGet("{showtimeId:int}/seat-map")]
    public async Task<IActionResult> SeatMap(int showtimeId)
    {
        // Suất thuê trọn của doanh nghiệp không hiện trên web khách (SPEC 3.10-08)
        if (!_seatMaps.ShowtimeExists(showtimeId) || OnlineBookingService.OnlineInfo(showtimeId) == null) return NotFoundPage("Không tìm thấy suất chiếu.");
        var bill = IsCustomer ? await _booking.CurrentFor(CurrentUserId, showtimeId) : null;
        return PartialView("_SeatMap", SeatMapVm.From(_seatMaps.Build(showtimeId, bill?.Id)));
    }

    private JsonResult CartJson(Cart? cart, int cinemaId) => ApiOk(new { cart = cart ?? new Cart(), stock = _catalog.Portions(cinemaId) });

    private int CinemaOf(int showtimeId) => OnlineBookingService.OnlineInfo(showtimeId)?.Auditorium.CinemaId ?? throw CustomerCatalogPages.ShowtimeNotFound();

    [HttpGet("{showtimeId:int}/cart")]
    public async Task<IActionResult> Cart(int showtimeId) => CartJson(await _booking.Cart(CurrentUserId, showtimeId), CinemaOf(showtimeId));

    /// <summary>Đặt lựa chọn ghế (seatIds) của khách trong suất.</summary>
    [HttpPost("{showtimeId:int}/hold")]
    public async Task<IActionResult> Hold(int showtimeId, [FromForm] List<int>? seatIds, bool wheelchairConfirmed = false)
    {
        var cart = await _booking.Hold(CurrentUserId, showtimeId, seatIds ?? new List<int>(), wheelchairConfirmed);
        return CartJson(cart, CinemaOf(showtimeId));
    }

    [HttpPost("{showtimeId:int}/quantity")]
    public async Task<IActionResult> Quantity(int showtimeId, int drinkId, string? action) =>
        CartJson(await _booking.UpdateQuantity(CurrentUserId, showtimeId, drinkId, action), CinemaOf(showtimeId));

    [HttpPost("{showtimeId:int}/voucher")]
    public async Task<IActionResult> Voucher(int showtimeId, string? code) =>
        CartJson(await _booking.ApplyVoucher(CurrentUserId, showtimeId, code), CinemaOf(showtimeId));

    [HttpPost("{showtimeId:int}/points")]
    public async Task<IActionResult> Points(int showtimeId, int points) =>
        CartJson(await _booking.ApplyPoints(CurrentUserId, showtimeId, points), CinemaOf(showtimeId));

    [HttpPost("{showtimeId:int}/confirm")]
    public async Task<IActionResult> Confirm(int showtimeId, bool age, bool guardian) =>
        CartJson(await _booking.Confirm(CurrentUserId, showtimeId, age, guardian), CinemaOf(showtimeId));

    /// <summary>Thanh toán: ví thì xong ngay (trả ticketUrl); VietQR thì trả payUrl của màn hình QR.</summary>
    [HttpPost("{showtimeId:int}/checkout")]
    public async Task<IActionResult> Checkout(int showtimeId, string? method) => ApiOk(await _booking.Checkout(CurrentUserId, showtimeId, method));

    /// <summary>Bỏ đơn đang dở và trả ghế.</summary>
    [HttpPost("{showtimeId:int}/cancel")]
    public async Task<IActionResult> Cancel(int showtimeId)
    {
        await _booking.CancelWaiting(CurrentUserId);
        return ApiOk(new { message = "Đã bỏ đơn và trả ghế." });
    }

    /// <summary>Màn hình QR chờ thanh toán: QR trỏ tới trang mô phỏng cổng, đồng hồ giữ ghế, trạng thái cập nhật trực tiếp qua kênh 3.</summary>
    [HttpGet("pay/{code}")]
    public IActionResult Pay(string code)
    {
        var bill = OnlineBookingService.OwnBill(CurrentUserId, code);
        if (bill == null) return NotFoundPage("Không tìm thấy đơn đặt vé.");
        if (bill.Status == BillStatus.Finish) return Redirect(OnlineBookingService.TicketUrl(bill.Code));
        var sim = $"{Request.Scheme}://{Request.Host}{OnlineBookingService.SimUrl(bill.Id)}";
        return View(new PayVm
        {
            Bill = bill, Info = bill.ShowtimeId is int sid ? SeatMapService.Info(sid) : null, SimUrl = OnlineBookingService.SimUrl(bill.Id),
            QrDataUri = QrSvg.DataUri(sim), ExpiresAt = bill.Status == BillStatus.Waiting ? bill.HoldExpiresAt : null,
        });
    }

    /// <summary>Trạng thái đơn (dự phòng khi mất kết nối thời gian thực).</summary>
    [HttpGet("pay/{code}/status")]
    public IActionResult PayStatus(string code)
    {
        var bill = OnlineBookingService.OwnBill(CurrentUserId, code);
        if (bill == null) return ApiFail(ErrorCodes.NotFound, "Không tìm thấy đơn đặt vé.");
        return ApiOk(new { status = bill.Status, cancelReason = bill.CancelReason, ticketUrl = OnlineBookingService.TicketUrl(bill.Code) });
    }
}

/// <summary>
/// Trang mô phỏng cổng thanh toán và webhook (SPEC 3.7). Không tích hợp cổng thật: trang /payment-sim/pay hiện QR và hai nút
/// "Thanh toán thành công", "Thất bại"; trang gọi POST /payments/webhook giống cổng thật, kèm chữ ký HMAC-SHA256 (Payment:WebhookSecret) và ProviderTxnId.
/// </summary>
[AllowAnonymous]
public class PaymentController : AppControllerBase
{
    private readonly OnlineBookingService _booking;

    public PaymentController(OnlineBookingService booking) => _booking = booking;

    [HttpGet("/payment-sim/pay")]
    public IActionResult Sim(int billId)
    {
        var bill = FPolyCinema.Web.DAL.BillDAL.GetById(billId);
        if (bill == null || bill.Channel != BillChannel.Online) return NotFoundPage("Không tìm thấy giao dịch.");
        var (ok, okSig, fail, failSig) = _booking.SimPayloads(bill);
        ViewData["Layout"] = "_LayoutCustomer";
        return View(new PaymentSimVm
        {
            Bill = bill, QrDataUri = QrSvg.DataUri($"FPOLYCINEMA|{bill.Code}|{bill.Total}"), SuccessPayload = ok, SuccessSignature = okSig, FailPayload = fail, FailSignature = failSig,
            ReturnUrl = $"/booking/pay/{bill.Code}",
        });
    }

    /// <summary>
    /// Webhook của cổng thanh toán: xác thực bằng chữ ký HMAC-SHA256 ở header X-Signature thay cho Anti-Forgery (cổng thanh toán không có cookie
    /// của trang); sai chữ ký trả 401; webhook lặp cùng ProviderTxnId trả OK mà không xử lý lần nữa.
    /// </summary>
    [HttpPost("/payments/webhook")]
    [IgnoreAntiforgeryToken] // webhook của cổng thanh toán xác thực bằng chữ ký HMAC-SHA256 (SPEC 3.7) thay cho Anti-Forgery
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var result = await _booking.HandleWebhook(body, Request.Headers[WebhookSignature.Header].FirstOrDefault());
        return new JsonResult(result.Status == 200 ? new { ok = true, data = new { message = result.Message } } : (object)new { ok = false, error = new { code = result.Status == 401 ? ErrorCodes.AuthRequired : ErrorCodes.ValidationFailed, message = result.Message } })
        {
            StatusCode = result.Status,
        };
    }
}

/// <summary>Vé điện tử C4 và tự hủy vé (UC11).</summary>
[Authorize(Policy = Policies.CustomerOnly)]
public class TicketsController : AppControllerBase
{
    private readonly OnlineBookingService _booking;
    private readonly CinemaRules _rules;
    private readonly KdsService _kds;
    private readonly IClock _clock;

    public TicketsController(OnlineBookingService booking, CinemaRules rules, KdsService kds, IClock clock)
    {
        _booking = booking;
        _rules = rules;
        _kds = kds;
        _clock = clock;
    }

    [HttpGet("/tickets/{billCode}")]
    public IActionResult Show(string billCode)
    {
        var bill = OnlineBookingService.OwnBill(CurrentUserId, billCode);
        if (bill == null) return NotFoundPage("Không tìm thấy vé.");
        if (bill.Status == BillStatus.Waiting && bill.ShowtimeId is int sid) return Redirect($"/booking/{sid}");
        var payments = FPolyCinema.Web.DAL.PaymentDAL.RowsByBill(bill.Id).Where(p => p.Status == PaymentStatus.Succeeded);
        return View(new TicketPageVm
        {
            Bill = bill, Info = bill.ShowtimeId is int s ? SeatMapService.Info(s) : null,
            CinemaName = FPolyCinema.Web.DAL.CinemaDAL.GetById(bill.CinemaId)?.Name ?? string.Empty,
            Tickets = FPolyCinema.Web.DAL.TicketDAL.RowsByBill(bill.Id), Lines = FPolyCinema.Web.DAL.BillDetailDAL.RowsByBill(bill.Id),
            Cancel = _booking.SelfCancelCheck(bill, CurrentUserId), MaxPerMonth = _rules.SelfCancelMaxPerMonth, MinMinutesBeforeStart = _rules.SelfCancelMinMinutesBeforeStart,
            Paid = payments.Where(p => p.Direction == PaymentDirection.In).Sum(p => p.Amount),
            Now = _clock.Now, ImHereWindowMinutes = _rules.ImHereWindowMinutesBeforeStart,
        });
    }

    /// <summary>Tự hủy vé: hoàn 100% vào ví.</summary>
    [HttpPost("/tickets/{billCode}/cancel")]
    public async Task<IActionResult> Cancel(string billCode)
    {
        var refund = await _booking.SelfCancel(CurrentUserId, billCode);
        return ApiOk(new { message = $"Đã hủy vé và hoàn {Fmt.Money(refund)} vào ví FPoly của bạn." });
    }

    /// <summary>
    /// "Tôi đã tới rạp – làm bắp nước ngay" (SPEC 3.11-04): chỉ trong khung StartTime − ImHereWindowMinutesBeforeStart đến EndTime; bếp bắt đầu làm món
    /// (RELEASE + CONSUME, PREPARING), số gọi được cấp và cập nhật trực tiếp trên vé (FNB_STATUS_CHANGED).
    /// </summary>
    [HttpPost("/tickets/{billCode}/im-here")]
    public async Task<IActionResult> ImHere(string billCode)
    {
        var bill = OnlineBookingService.OwnBill(CurrentUserId, billCode) ?? throw CategoryService.NotFound("Không tìm thấy vé.");
        var order = await _kds.StartOnline(bill.Id, CurrentUserId, null, null);
        return ApiOk(new { pickupNumber = order.PickupNumber, message = $"Bếp đã nhận đơn. Số gọi của bạn là {order.PickupNumber}." });
    }
}
