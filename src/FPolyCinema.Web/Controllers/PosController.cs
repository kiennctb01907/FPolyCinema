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
/// Màn hình bán hàng tại quầy (SPEC 3.6, UI P1, P7; slide bài 6). Route giữ kiểu slide (CONTRACTS C9-07): /pos/index, /pos/init,
/// /pos/update-quantity, /pos/checkout, /pos/cancel; thêm /pos/select-showtime, /pos/hold-seats, /pos/set-audience, /pos/lookup-member,
/// /pos/apply-voucher, /pos/request-void, /pos/print, /pos/my-bills và các route phụ cùng kiểu chữ thường gạch ngang.
/// Mọi thao tác bán yêu cầu ca OPEN tại máy này (SHIFT_NOT_OPEN). Mọi POST có Anti-Forgery (bộ lọc toàn cục).
/// </summary>
[Authorize(Policy = Policies.PosStaff)]
[Route("pos")]
public class PosController : AppControllerBase
{
    private readonly ShiftService _shifts;
    private readonly BillService _bills;
    private readonly SeatHoldService _holds;
    private readonly SeatMapService _seatMaps;
    private readonly InventoryService _inventory;
    private readonly ManagerPinService _pins;
    private readonly BillQueryService _query;
    private readonly CinemaContextService _cinema;
    private readonly IClock _clock;
    private readonly CinemaRules _rules;

    public PosController(ShiftService shifts, BillService bills, SeatHoldService holds, SeatMapService seatMaps, InventoryService inventory,
        ManagerPinService pins, BillQueryService query, CinemaContextService cinema, IClock clock, CinemaRules rules)
    {
        _shifts = shifts;
        _bills = bills;
        _holds = holds;
        _seatMaps = seatMaps;
        _inventory = inventory;
        _pins = pins;
        _query = query;
        _cinema = cinema;
        _clock = clock;
        _rules = rules;
    }

    private string? TerminalCookie => Request.Cookies[ShiftService.TerminalCookie];

    /// <summary>Ngữ cảnh bán hàng: ca OPEN của người dùng tại đúng máy ghi trong cookie mã máy.</summary>
    private PosContext? TryContext() => _shifts.Resolve(CurrentUserId, CurrentRole ?? string.Empty, TerminalCookie);

    private PosContext Context() => _shifts.Require(CurrentUserId, CurrentRole ?? string.Empty, TerminalCookie);

    private void SetTerminalCookie(string code) => Response.Cookies.Append(ShiftService.TerminalCookie, code,
        new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Expires = _clock.Now.AddYears(1), IsEssential = true });

    private JsonResult CartJson(Cart cart) => ApiOk(new { cart });

    // ------------------------------------------------------------------ màn hình chính

    /// <summary>Màn hình POS 3 cột (UI P1). Chưa mở ca thì chuyển sang Mở ca.</summary>
    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index()
    {
        var ctx = TryContext();
        if (ctx == null) return Redirect("/pos/open-shift");
        var now = _clock.Now;
        var day = PricingCalculator.OperatingDate(now);
        var drinks = DrinkDAL.GetAll().Where(d => d.Active).OrderBy(d => d.IsCombo ? 0 : 1).ThenBy(d => d.Name).ToList();
        var categories = CategoryDAL.GetActive();
        var portions = _inventory.Portions(ctx.CinemaId, drinks);
        var vm = new PosIndexVm
        {
            Context = ctx,
            Now = now,
            Showtimes = ShowtimeDAL.ForSale(ctx.CinemaId, day.Add(PricingCalculator.NightCutoff), day.AddDays(1).Add(PricingCalculator.NightCutoff), now)
                .Where(s => SeatHoldRules.IsSalesOpen(s.FeatureStartTime, s.OperationalStatus, BillChannel.Pos, now, _rules)).ToList(),
            Categories = categories,
            Drinks = drinks.Select(d => new PosDrink(d, categories.FirstOrDefault(c => c.Id == d.CategoryId)?.Name ?? string.Empty, portions[d.Id])).ToList(),
            Supervisors = BillQueryService.Supervisors(ctx.CinemaId),
            Audiences = AudienceDiscountDAL.GetAll(),
            Cart = await _bills.Cart(ctx),
            LowSeatThreshold = 10,
        };
        ViewData["ConnState"] = "reconnecting";
        return View(vm);
    }

    // ------------------------------------------------------------------ mở ca, đóng ca (UI P7)

    [HttpGet("open-shift")]
    public IActionResult OpenShift()
    {
        if (TryContext() != null) return Redirect("/pos/index");
        return View(OpenVm(null, null, new Dictionary<string, string>()));
    }

    private OpenShiftVm OpenVm(string? terminal, long? amount, Dictionary<string, string> errors)
    {
        var cinemaId = PosCinemaId();
        return new OpenShiftVm
        {
            Terminals = ShiftService.Terminals(),
            Terminal = terminal ?? (TerminalCookie != null && ShiftService.Terminals().Contains(TerminalCookie) ? TerminalCookie : ShiftService.Terminals()[0]),
            OpeningCashFloat = amount,
            CinemaName = _cinema.Cinemas.FirstOrDefault(c => c.Id == cinemaId)?.Name ?? string.Empty,
            OpenElsewhere = PosShiftDAL.GetOpenByUser(CurrentUserId),
            Errors = errors,
        };
    }

    /// <summary>Rạp của máy POS: rạp gắn với nhân viên; quản lý toàn chuỗi dùng rạp đang chọn (hoặc rạp đầu tiên).</summary>
    private int PosCinemaId() => _cinema.CurrentCinemaId ?? _cinema.Cinemas.First().Id;

    /// <summary>Mở ca: chọn máy POS, nhập tiền đầu ca; mã máy lưu trong cookie của máy.</summary>
    [HttpPost("open-shift")]
    public IActionResult OpenShift(string? terminal, long? openingCashFloat)
    {
        try
        {
            var shift = _shifts.Open(CurrentUserId, PosCinemaId(), terminal, openingCashFloat);
            SetTerminalCookie(shift.TerminalCode);
            Toast("success", $"Đã mở ca tại {shift.TerminalCode}.");
            return Redirect("/pos/index");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            return View(OpenVm(terminal, openingCashFloat, ex.Details as Dictionary<string, string> ?? new Dictionary<string, string> { [""] = ex.Message }));
        }
    }

    /// <summary>Đóng ca mù 3 bước: đếm theo mệnh giá, xác nhận, biên bản. Không hiện số hệ thống tính trước khi xác nhận.</summary>
    [HttpGet("close-shift")]
    public IActionResult CloseShift()
    {
        var ctx = TryContext() ?? ShiftService.ResolveOwn(CurrentUserId, CurrentRole ?? string.Empty);
        if (ctx == null) return Redirect("/pos/open-shift");
        _bills.ExpireStaleOfShift(ctx.Shift.Id, _clock.Now);
        return View(new CloseShiftVm { Shift = ctx.Shift, Denominations = CashDropCalculator.Denominations, WaitingBills = BillDAL.CountWaitingForShift(ctx.Shift.Id) });
    }

    /// <summary>Chốt ca: counts[mệnh giá] = số tờ. Trả đường dẫn biên bản.</summary>
    [HttpPost("close-shift")]
    public IActionResult CloseShift([FromForm] Dictionary<long, int> counts)
    {
        var ctx = TryContext() ?? ShiftService.ResolveOwn(CurrentUserId, CurrentRole ?? string.Empty)
            ?? throw new BusinessRuleException(ErrorCodes.ShiftNotOpen, "Bạn không có ca nào đang mở.");
        _shifts.Close(ctx, counts ?? new Dictionary<long, int>());
        Response.Cookies.Delete(ShiftService.TerminalCookie);
        return ApiOk(new { reportUrl = $"/pos/shift-report/{ctx.Shift.Id}" });
    }

    /// <summary>Biên bản ca: số đếm được so với số hệ thống tính, chênh lệch thừa hoặc thiếu; in được, không sửa được.</summary>
    [HttpGet("shift-report/{id:int}")]
    public IActionResult ShiftReport(int id)
    {
        var row = PosShiftDAL.GetRow(id);
        if (row == null || row.Shift.Status != ShiftStatus.Closed) return NotFoundPage("Không tìm thấy biên bản ca.");
        bool own = row.Shift.UserId == CurrentUserId;
        if (!own && !Roles.SupervisorOrManager.Contains(CurrentRole)) return NotFoundPage("Không tìm thấy biên bản ca.");
        return View(new ShiftReportVm { Row = row, IsOwn = own });
    }

    // ------------------------------------------------------------------ hóa đơn tạm tính

    /// <summary>Khởi tạo (hoặc lấy lại) hóa đơn waiting của máy POS.</summary>
    [HttpPost("init")]
    public async Task<IActionResult> Init()
    {
        var ctx = Context();
        var bill = await _bills.Ensure(ctx);
        return CartJson(_bills.BuildCart(bill, new List<string>()));
    }

    /// <summary>Tải lại hóa đơn tạm tính (sau khi kết nối lại).</summary>
    [HttpGet("cart")]
    public async Task<IActionResult> CartState() => CartJson(await _bills.Cart(Context()));

    /// <summary>
    /// Chọn suất (hoặc "Chỉ bán bắp nước" khi showtimeId rỗng). Hóa đơn chỉ có một suất: đổi suất thì trả các ghế đang giữ ở suất cũ.
    /// </summary>
    [HttpPost("select-showtime")]
    public async Task<IActionResult> SelectShowtime(int? showtimeId)
    {
        var ctx = Context();
        var bill = await _bills.Current(ctx);
        if (bill?.ShowtimeId is int old && old != showtimeId)
            await _holds.Hold(bill.Id, old, Array.Empty<int>(), new HoldOptions(BillChannel.Pos, ctx.Role, ctx.UserId));
        if (showtimeId is int sid)
        {
            var info = SeatMapService.Info(sid);
            if (info == null || info.Auditorium.CinemaId != ctx.CinemaId) return ApiFail(ErrorCodes.NotFound, "Không tìm thấy suất chiếu ở rạp này.");
        }
        return CartJson(await _bills.Cart(ctx));
    }

    /// <summary>Sơ đồ ghế của suất (HTML của _SeatMap) – tải khi chọn suất và tải lại sau khi kết nối lại.</summary>
    [HttpGet("seat-map")]
    public async Task<IActionResult> SeatMap(int showtimeId)
    {
        var ctx = Context();
        var bill = await _bills.Current(ctx);
        var info = SeatMapService.Info(showtimeId);
        if (info == null || info.Auditorium.CinemaId != ctx.CinemaId) return NotFoundPage("Không tìm thấy suất chiếu.");
        var model = _seatMaps.Build(showtimeId, bill?.Id);
        return PartialView("_SeatMapRegion", new PosSeatMapVm { SeatMap = SeatMapVm.From(model), Info = info, Now = _clock.Now });
    }

    /// <summary>Giữ ghế: seatIds là toàn bộ lựa chọn mới của hóa đơn trong suất (SeatHoldService.Hold).</summary>
    [HttpPost("hold-seats")]
    public async Task<IActionResult> HoldSeats(int showtimeId, [FromForm] int[]? seatIds, bool overrideOrphan, string? overrideReason, bool wheelchairConfirmed)
    {
        var ctx = Context();
        var bill = await _bills.Ensure(ctx);
        var result = await _holds.Hold(bill.Id, showtimeId, seatIds ?? Array.Empty<int>(),
            new HoldOptions(BillChannel.Pos, ctx.Role, ctx.UserId, overrideOrphan, overrideReason, wheelchairConfirmed));
        var notices = new List<string>();
        if (result.OrphanOverridden) notices.Add("Đã bỏ qua luật ghế mồ côi và ghi nhật ký kèm lý do.");
        return CartJson(_bills.BuildCart(result.Bill, notices));
    }

    /// <summary>Gia hạn giữ ghế một lần (SeatHoldExtendMinutes).</summary>
    [HttpPost("extend-hold")]
    public async Task<IActionResult> ExtendHold() => CartJson(await _bills.ExtendHold(Context()));

    /// <summary>Chọn đối tượng vé cho một ghế (seatId) hoặc mọi vé (seatId rỗng).</summary>
    [HttpPost("set-audience")]
    public async Task<IActionResult> SetAudience(int? seatId, string? audienceType) => CartJson(await _bills.SetAudience(Context(), seatId, audienceType));

    /// <summary>Tích hoặc bỏ "Có người giám hộ đi cùng" (phim K).</summary>
    [HttpPost("set-guardian")]
    public async Task<IActionResult> SetGuardian(bool confirmed) => CartJson(await _bills.SetGuardian(Context(), confirmed));

    /// <summary>Bắp nước theo slide: action là incr, decr hoặc delete.</summary>
    [HttpPost("update-quantity")]
    public async Task<IActionResult> UpdateQuantity(int drinkId, string? action) => CartJson(await _bills.UpdateQuantity(Context(), drinkId, action));

    /// <summary>Số phần còn bán được của từng món (cập nhật lưới "Tạm hết").</summary>
    [HttpGet("drink-stock")]
    public IActionResult DrinkStock()
    {
        var ctx = Context();
        var drinks = DrinkDAL.GetAll().Where(d => d.Active).ToList();
        return ApiOk(_inventory.Portions(ctx.CinemaId, drinks).ToDictionary(kv => kv.Key.ToString(), kv => Math.Min(kv.Value, 999)));
    }

    /// <summary>Tra cứu thành viên (F1) theo mã thành viên, số điện thoại hoặc email; query rỗng là bỏ thành viên.</summary>
    [HttpPost("lookup-member")]
    public async Task<IActionResult> LookupMember(string? query) => CartJson(await _bills.LookupMember(Context(), query));

    /// <summary>Áp hoặc gỡ voucher.</summary>
    [HttpPost("apply-voucher")]
    public async Task<IActionResult> ApplyVoucher(string? code) => CartJson(await _bills.ApplyVoucher(Context(), code));

    /// <summary>Đổi điểm thành viên.</summary>
    [HttpPost("apply-points")]
    public async Task<IActionResult> ApplyPoints(int points) => CartJson(await _bills.ApplyPoints(Context(), points));

    /// <summary>QR chuyển khoản mô phỏng (VIETQR_SIM) cho số tiền của hóa đơn.</summary>
    [HttpGet("payment-qr")]
    public async Task<IActionResult> PaymentQr()
    {
        var ctx = Context();
        var bill = await _bills.Current(ctx) ?? throw FieldErrors.Of("bill", "Hóa đơn trống. Bạn chọn ghế hoặc món trước.");
        var payload = $"VIETQR-SIM|FPOLYCINEMA|{bill.Code}|{bill.Total}";
        return ApiOk(new { dataUri = QrSvg.DataUri(payload), code = bill.Code, amount = bill.Total, amountText = Fmt.Money(bill.Total) });
    }

    /// <summary>Thanh toán: method CASH (cashReceived là tiền khách đưa) hoặc VIETQR_SIM (sau khi thu ngân bấm "Đã nhận tiền").</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(string? method, long? cashReceived) => ApiOk(await _bills.Checkout(Context(), method, cashReceived));

    /// <summary>Hủy hóa đơn chưa thanh toán (billId rỗng là hóa đơn tạm tính đang mở) và trả ghế.</summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(int? billId)
    {
        var ctx = Context();
        int id;
        if (billId is int given)
        {
            var b = BillDAL.GetById(given);
            if (b == null || b.PosShiftId != ctx.Shift.Id) return ApiFail(ErrorCodes.NotFound, "Không tìm thấy hóa đơn chưa thanh toán của ca này.");
            id = given;
        }
        else
        {
            var cur = await _bills.Current(ctx);
            if (cur == null) return CartJson(new Cart());
            id = cur.Id;
        }
        var code = await _bills.CancelWaiting(id, ctx.Role, ctx.CinemaId);
        return ApiOk(new { cart = new Cart { Notices = new List<string> { $"Đã hủy hóa đơn {code}." } } });
    }

    /// <summary>
    /// Hủy hóa đơn đã thanh toán: thu ngân nhập lý do, quản lý chọn tên và nhập PIN ngay trên máy (bàn phím PIN UI mục 5).
    /// Không có PIN thì trả APPROVAL_REQUIRED.
    /// </summary>
    [HttpPost("request-void")]
    public async Task<IActionResult> RequestVoid(int? billId, string? billCode, string? reason, int? approverId, string? pin)
    {
        var ctx = Context();
        if (billId == null)
        {
            var found = _query.FindByScan(billCode);
            if (found == null || found.CinemaId != ctx.CinemaId) throw FieldErrors.Of("billCode", "Không tìm thấy hóa đơn của rạp này với mã vừa nhập.");
            billId = found.Id;
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) throw FieldErrors.Of("reason", "Bạn ghi lý do hủy (ít nhất 5 ký tự).");
        if (approverId == null || string.IsNullOrEmpty(pin))
            return ApiFail(ErrorCodes.ApprovalRequired, "Hủy hóa đơn đã thanh toán cần quản lý ca hoặc quản lý rạp nhập PIN để duyệt.");
        var approver = _pins.Verify(approverId.Value, pin, ctx.CinemaId);
        var code = await _bills.VoidPaid(billId.Value, reason, approver, ctx.UserId, ctx.CinemaId, ctx.Shift.Id);
        return ApiOk(new { code, message = $"Đã hủy hóa đơn {code} và hoàn tiền. Người duyệt: {approver.FullName}." });
    }

    /// <summary>Quét QR hóa đơn online (mã hóa đơn hoặc mã vé) tại quầy để in vé giấy.</summary>
    [HttpPost("scan")]
    public IActionResult Scan(string? code)
    {
        var ctx = Context();
        var bill = _query.FindByScan(code);
        if (bill == null || bill.CinemaId != ctx.CinemaId) return ApiFail(ErrorCodes.NotFound, "Không tìm thấy hóa đơn của rạp này với mã vừa quét.");
        if (bill.Status != BillStatus.Finish) return ApiFail(ErrorCodes.ValidationFailed, "Hóa đơn này chưa thanh toán hoặc đã hủy nên không in vé được.");
        return ApiOk(new { printUrl = $"/pos/print?billId={bill.Id}&auto=1", code = bill.Code });
    }

    // ------------------------------------------------------------------ in vé, hóa đơn của tôi

    /// <summary>Trang in vé khổ 80mm (vé có QR, bắp nước và số gọi, tổng tiền, nhãn tuổi).</summary>
    [HttpGet("print")]
    public IActionResult Print(int billId, int auto = 0)
    {
        var ctx = TryContext();
        var d = _query.Detail(billId);
        int? cinema = ctx?.CinemaId ?? _cinema.CurrentCinemaId;
        if (d == null || (cinema != null && d.Bill.CinemaId != cinema)) return NotFoundPage("Không tìm thấy hóa đơn.");
        return View(new PrintVm
        {
            Bill = d.Bill, CinemaName = d.Row.CinemaName, CinemaAddress = CinemaDAL.GetById(d.Bill.CinemaId)?.Address, StaffName = d.Row.StaffName,
            CustomerName = d.CustomerName, Tickets = d.Tickets.Where(t => t.CheckInStatus != TicketCheckInStatus.Swapped).ToList(), Lines = d.Lines, Payments = d.Payments,
            VatTotal = d.VatTotal, AutoPrint = auto == 1, BackUrl = "/pos/index",
        });
    }

    /// <summary>Hóa đơn do chính thu ngân tạo, lọc theo ngày.</summary>
    [HttpGet("my-bills")]
    public IActionResult MyBills(DateTime? date)
    {
        var ctx = TryContext();
        var day = (date ?? _clock.Now).Date;
        return View(new MyBillsVm
        {
            Date = day, Bills = _query.CreatedBy(CurrentUserId, day),
            Supervisors = ctx != null ? BillQueryService.Supervisors(ctx.CinemaId) : new(), HasShift = ctx != null, ShiftId = ctx?.Shift.Id,
        });
    }
}
