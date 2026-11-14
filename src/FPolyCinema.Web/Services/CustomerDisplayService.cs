using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Services;

/// <summary>
/// Màn hình phụ của quầy (SPEC 3.6-11, 3.6-17; UI P2; kênh 2): dựng nội dung cho màn hình phụ từ hóa đơn tạm tính của máy POS và phát
/// POS_SHOWTIME_SELECTED, POS_CART_UPDATED, POS_PAYMENT_QR, POS_PAYMENT_DONE, POS_RESET tới pos_terminal_{khóa máy}.
/// </summary>
public class CustomerDisplayService
{
    private readonly RealtimeNotifier _notifier;
    private readonly SeatMapService _seatMaps;
    private readonly BillService _bills;
    private readonly IClock _clock;
    private readonly CinemaRules _rules;

    public CustomerDisplayService(RealtimeNotifier notifier, SeatMapService seatMaps, BillService bills, IClock clock, CinemaRules rules)
    {
        _notifier = notifier;
        _seatMaps = seatMaps;
        _bills = bills;
        _clock = clock;
        _rules = rules;
    }

    /// <summary>Nội dung giỏ cho màn hình phụ: suất, ghế đang chọn kèm điểm, vé, món, giảm giá, tổng tiền.</summary>
    public object Payload(Cart cart)
    {
        var scores = new Dictionary<int, int>();
        if (cart.Showtime != null && cart.BillId is int bid)
            foreach (var s in _seatMaps.Build(cart.Showtime.Id, bid).Seats) scores[s.SeatId] = s.Score;
        return new
        {
            billId = cart.BillId,
            code = cart.Code,
            showtime = cart.Showtime == null ? null : new
            {
                id = cart.Showtime.Id, title = cart.Showtime.MovieTitle, ageRating = cart.Showtime.AgeRating, ageExplanation = cart.Showtime.AgeExplanation,
                time = $"{Fmt.WeekdayDate(cart.Showtime.StartTime)} · {Fmt.Time(cart.Showtime.StartTime)}–{Fmt.Time(cart.Showtime.EndTime)}",
                room = $"{cart.Showtime.AuditoriumName} · {cart.Showtime.LanguageLabel}",
            },
            tickets = cart.Tickets.Select(t => new
            {
                seatId = t.SeatId, seat = t.Label, seatType = t.SeatTypeLabel, audience = t.AudienceLabel, price = Fmt.Money(t.FinalPrice),
                score = scores.TryGetValue(t.SeatId, out var sc) ? sc : (int?)null,
            }),
            items = cart.Items.Select(i => new { name = i.Name, quantity = i.Quantity, amount = Fmt.Money(i.Amount) }),
            discount = cart.DiscountTotal > 0 ? "−" + Fmt.Money(cart.DiscountTotal) : null,
            total = Fmt.Money(cart.Total),
            member = cart.Member == null ? null : $"{cart.Member.FullName} · {cart.Member.TierLabel}",
            empty = cart.IsEmpty,
        };
    }

    /// <summary>Trạng thái hiện tại của một máy POS (màn hình phụ tải lại sau khi kết nối lại): hóa đơn waiting của ca đang mở tại máy.</summary>
    public async Task<object?> State(int cinemaId, string terminal)
    {
        var shift = PosShiftDAL.GetOpenByTerminal(cinemaId, terminal);
        if (shift == null) return null;
        var cart = await _bills.Cart(new PosContext(shift.UserId, Roles.POSCashier, cinemaId, shift));
        return cart.IsEmpty ? null : Payload(cart);
    }

    public Task CartUpdated(string terminalKey, Cart cart) => _notifier.PosCartUpdated(terminalKey, Payload(cart));

    /// <summary>POS_SHOWTIME_SELECTED { showtimeId, showtime, billId, cart }: màn hình phụ mở sơ đồ ghế lớn của suất (showtimeId null là chỉ bán bắp nước).</summary>
    public Task ShowtimeSelected(string terminalKey, Cart cart, int? showtimeId)
    {
        var info = showtimeId is int sid ? SeatMapService.Info(sid) : null;
        return _notifier.PosShowtimeSelected(terminalKey, new
        {
            showtimeId,
            billId = cart.BillId,
            showtime = info == null ? null : new
            {
                title = info.Movie.Title, ageRating = info.Movie.AgeRating, ageExplanation = StatusDisplay.AgeExplanation(info.Movie.AgeRating),
                time = $"{Fmt.WeekdayDate(info.Showtime.StartTime)} · {Fmt.Time(info.Showtime.StartTime)}–{Fmt.Time(info.Showtime.EndTime)}",
                room = $"{info.Auditorium.Name} · {StatusDisplay.Of(StatusDisplay.Language, info.Showtime.LanguageFormat).Label}",
            },
            cart = Payload(cart),
        });
    }

    /// <summary>QR chuyển khoản mô phỏng hiện thêm trên màn hình phụ (SPEC 3.6-11): QR, số tiền, hạn đếm ngược (hạn giữ ghế hoặc SeatHoldMinutes).</summary>
    public Task PaymentQr(string terminalKey, string dataUri, string code, long amount, DateTime? holdExpiresAt) =>
        _notifier.PosPaymentQr(terminalKey, new
        {
            dataUri, code, amount = Fmt.Money(amount),
            expiresAt = (holdExpiresAt ?? _clock.Now.AddMinutes(_rules.SeatHoldMinutes)).ToString("yyyy-MM-ddTHH:mm:ss"),
        });

    /// <summary>POS_PAYMENT_DONE (SPEC 3.6-17): "Cảm ơn quý khách" kèm mã vé và QR vé (QR của mã hóa đơn, quét được ở cửa).</summary>
    public Task PaymentDone(string terminalKey, int billId, string code)
    {
        var tickets = TicketDAL.RowsByBill(billId).Where(t => t.CheckInStatus == TicketCheckInStatus.NotCheckedIn).Select(t => t.SeatLabel).ToList();
        var bill = BillDAL.GetById(billId);
        return _notifier.PosPaymentDone(terminalKey, new
        {
            code, qr = QrSvg.DataUri(code), seats = tickets,
            pickupNumber = bill?.PickupNumber is int n ? Domain.Rules.CodeGenerator.FormatPickupNumber(n) : null,
        });
    }

    public Task Reset(string terminalKey) => _notifier.PosReset(terminalKey);
}
