using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Web.Services;

/// <summary>
/// BookingAgentService (SPEC 4.2, UC13): trợ lý đặt vé bằng tiếng Việt với hai bộ máy dùng chung một bộ công cụ (IBookingTools).
/// - Có Llm:ApiKey thì dùng LlmAgent; gặp lỗi mạng hoặc hết hạn mức thì tự chuyển sang LocalAgent, khách không thấy lỗi (SPEC 4.2-02).
/// - Không có khóa thì dùng LocalAgent (SPEC 4.2-01).
/// - Hội thoại giữ trong bộ nhớ theo phiên (AgentSessionStore), không lưu DB (SPEC 4.2-15).
/// - Agent không giữ ghế, không thanh toán (SPEC 4.2-08); chỉ HoldCard – gọi khi khách bấm "Giữ ghế &amp; thanh toán ngay" – mới giữ ghế qua
///   OnlineBookingService.Hold (SeatHoldService, giống luồng đặt vé thường) rồi chuyển sang bước thanh toán (SPEC 4.2-26).
/// </summary>
public class BookingAgentService
{
    private readonly LocalAgent _local;
    private readonly LlmAgent _llm;
    private readonly LlmOptions _options;
    private readonly AgentSessionStore _store;
    private readonly OnlineBookingService _booking;
    private readonly ILogger<BookingAgentService> _log;

    public BookingAgentService(LocalAgent local, LlmAgent llm, IOptions<LlmOptions> options, AgentSessionStore store, OnlineBookingService booking,
        ILogger<BookingAgentService> log)
    {
        _local = local;
        _llm = llm;
        _options = options.Value;
        _store = store;
        _booking = booking;
        _log = log;
    }

    public const int MaxMessageLength = 500;

    public AgentSession Session(string sessionId) => _store.Get(sessionId);

    /// <summary>Xử lý một câu của khách (hoặc lần chọn một suất trong danh sách gợi ý).</summary>
    public async Task<AgentReply> Ask(string sessionId, string? text, int? showtimeId, AgentContext ctx, CancellationToken ct = default)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length > MaxMessageLength) text = text[..MaxMessageLength];
        if (text.Length == 0 && showtimeId == null)
            throw FieldErrors.Of("text", "Bạn nhập câu hỏi cho trợ lý.");
        var session = _store.Get(sessionId);
        AgentReply reply;
        lock (session)
        {
            if (showtimeId != null && session.LastShowtimes.All(s => s.ShowtimeId != showtimeId)) showtimeId = null;
        }
        if (_options.Enabled)
        {
            try { reply = await _llm.Handle(session, text, showtimeId, ctx, ct); }
            catch (LlmUnavailableException ex)
            {
                _log.LogWarning(ex, "LlmAgent không dùng được, chuyển sang LocalAgent");
                lock (session) reply = _local.Handle(session, text, showtimeId, ctx);
            }
        }
        else
        {
            lock (session) reply = _local.Handle(session, text, showtimeId, ctx);
        }
        lock (session)
        {
            if (reply.Showtimes.Count > 0) session.LastShowtimes = reply.Showtimes.ToList();
            var asked = text.Length > 0 ? text : showtimeId is int id ? $"Chọn suất {session.LastShowtimes.FirstOrDefault(s => s.ShowtimeId == id)?.Label}" : string.Empty;
            session.History.Add(new ChatTurn("user", asked));
            session.History.Add(new ChatTurn("assistant", reply.Text));
            if (session.History.Count > 40) session.History.RemoveRange(0, session.History.Count - 40);
        }
        return reply;
    }

    /// <summary>Xóa hội thoại và thẻ đặt vé của phiên.</summary>
    public void Reset(string sessionId) => _store.Clear(sessionId);

    /// <summary>
    /// "Giữ ghế &amp; thanh toán ngay": giữ đúng các ghế trên thẻ đặt vé hiện tại của phiên bằng OnlineBookingService.Hold (SeatHoldService, kênh ONLINE),
    /// thêm các món của thẻ vào hóa đơn, rồi trả đường dẫn bước thanh toán của trang đặt vé. Món hết nguyên liệu thì bỏ qua và báo lại.
    /// </summary>
    public async Task<(string Redirect, List<string> Notes)> HoldCard(string sessionId, int userId)
    {
        var session = _store.Get(sessionId);
        var card = session.Card ?? throw new BusinessRuleException(ErrorCodes.ValidationFailed, "Thẻ đặt vé đã hết hạn. Bạn hỏi lại trợ lý để tìm suất nhé.");
        await _booking.Hold(userId, card.ShowtimeId, card.Seats.Select(s => s.SeatId).ToList());
        var notes = new List<string>();
        foreach (var item in card.Items)
        {
            try
            {
                for (int i = 0; i < item.Quantity; i++) await _booking.UpdateQuantity(userId, card.ShowtimeId, item.DrinkId, "incr");
            }
            catch (BusinessRuleException ex) when (ex.Code is ErrorCodes.StockInsufficient or ErrorCodes.ValidationFailed)
            {
                notes.Add(ex.Message);
            }
        }
        lock (session) session.Card = null;
        return ($"/booking/{card.ShowtimeId}?step=4", notes);
    }
}
