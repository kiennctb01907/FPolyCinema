using System.Text.RegularExpressions;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace FPolyCinema.Web.Services;

/// <summary>Tiêu chí tìm suất đã gộp qua các câu của một phiên hội thoại.</summary>
public sealed class AgentCriteria
{
    public List<DateTime> Dates { get; set; } = new();
    public TimeSpan? TimeFrom { get; set; }
    public TimeSpan? TimeTo { get; set; }
    public int? Quantity { get; set; }
    public string? Genre { get; set; }
    public string? LanguageFormat { get; set; }
    public string? Format { get; set; }
    public string? Persona { get; set; }
    public int? MovieId { get; set; }
    public string? MovieTitle { get; set; }
    public bool Earliest { get; set; }
    public bool ForChild { get; set; }
    public List<ParsedItem> Items { get; set; } = new();
    /// <summary>Suất khách đã chọn từ danh sách gợi ý.</summary>
    public int? ShowtimeId { get; set; }
}

/// <summary>Một lượt trong hội thoại (role là user hoặc assistant).</summary>
public sealed record ChatTurn(string Role, string Text);

/// <summary>
/// Phiên hội thoại của trợ lý (SPEC 4.2-15): giữ trong bộ nhớ của máy chủ theo phiên (cookie fpc_agent), không lưu DB.
/// </summary>
public sealed class AgentSession
{
    public AgentSession(string id) => Id = id;
    public string Id { get; }
    public List<ChatTurn> History { get; } = new();
    public AgentCriteria Criteria { get; set; } = new();
    public AgentCard? Card { get; set; }
    /// <summary>Câu hỏi lại đang chờ khách trả lời: "date", "quantity" hoặc null.</summary>
    public string? Pending { get; set; }
    /// <summary>Danh sách suất vừa gợi ý cho khách chọn.</summary>
    public List<AgentShowtime> LastShowtimes { get; set; } = new();
}

/// <summary>Câu trả lời của trợ lý: lời nhắn, thẻ đặt vé (nếu có), danh sách suất để chọn, chip câu hỏi gợi ý, bộ máy đã trả lời.</summary>
public sealed record AgentReply(string Text, AgentCard? Card, List<AgentShowtime> Showtimes, List<string> Chips, string Engine);

/// <summary>Ngữ cảnh người hỏi: tài khoản khách (nếu đã đăng nhập) và rạp đang chọn.</summary>
public sealed record AgentContext(int? UserId, int? CinemaId);

/// <summary>Kho phiên hội thoại trong bộ nhớ (IMemoryCache, hết hạn trượt 30 phút). Không lưu DB.</summary>
public class AgentSessionStore
{
    public const string CookieName = "fpc_agent";
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);
    private readonly IMemoryCache _cache;

    public AgentSessionStore(IMemoryCache cache) => _cache = cache;

    public AgentSession Get(string id) =>
        _cache.GetOrCreate("agent:" + id, e => { e.SlidingExpiration = Idle; return new AgentSession(id); })!;

    public void Clear(string id) => _cache.Remove("agent:" + id);
}

/// <summary>
/// LocalAgent (SPEC 4.2-01): bộ máy mặc định, không cần mạng. NlpParser trích các trường rồi gọi bộ công cụ theo luồng cố định:
/// search_showtimes → check_rules → get_best_seats → find_combo → build_preview. Thiếu thông tin bắt buộc (ngày, số người) thì hỏi lại đúng một câu ngắn.
/// Không giữ ghế, không thanh toán (SPEC 4.2-08).
/// </summary>
public class LocalAgent
{
    public const string EngineName = "local";

    public static readonly List<string> StartChips = new()
    {
        "Tối nay 2 vé phim hành động, ghế đẹp nhất",
        "Cuối tuần này có suất bản gốc không",
        "Đặt 4 vé suất gần nhất, ghế gần lối đi",
        "Cặp đôi muốn ghế đôi riêng tư tối nay",
    };

    public static readonly List<string> CardChips = new() { "Thêm 2 nước suối", "Đổi sang suất sau đó", "Thôi, làm lại" };

    private readonly IBookingTools _tools;

    public LocalAgent(IBookingTools tools) => _tools = tools;

    private AgentReply Reply(string text, AgentCard? card = null, List<AgentShowtime>? list = null, List<string>? chips = null) =>
        new(text, card, list ?? new List<AgentShowtime>(), chips ?? (card != null ? CardChips : StartChips), EngineName);

    public AgentReply Handle(AgentSession session, string? text, int? pickShowtimeId, AgentContext ctx)
    {
        var catalog = _tools.Catalog(ctx.CinemaId);
        var today = PricingCalculator.OperatingDate(_tools.Now);
        var intent = NlpParser.Parse(text, today, _tools.DayStart, catalog.Movies, catalog.Drinks);

        switch (intent.Command)
        {
            case AgentCommand.Reset:
                session.Card = null;
                session.Criteria = new AgentCriteria();
                session.Pending = null;
                session.LastShowtimes.Clear();
                return Reply("Mình đã xóa thẻ đặt vé. Bạn muốn xem phim gì, vào lúc nào?");
            case AgentCommand.AddItems:
                return AddItems(session, intent, ctx);
            case AgentCommand.NextShowtime:
                return NextShowtime(session, ctx);
        }

        // Câu yêu cầu mới đủ ý (có số người và tiêu chí tìm suất) thì bắt đầu tiêu chí mới; câu ngắn bổ sung thì gộp vào tiêu chí đang có
        if (intent.Quantity != null && intent.HasSearchCriteria) session.Criteria = new AgentCriteria();
        var c = session.Criteria;
        // Trả lời câu hỏi lại bằng một con số ("3")
        if (session.Pending == "quantity" && intent.Quantity == null && Regex.Match(text ?? string.Empty, @"^\s*(\d{1,2})\s*$") is { Success: true } bare)
            intent.Quantity = Math.Clamp(int.Parse(bare.Groups[1].Value), 1, 10);

        bool understood = intent.HasSearchCriteria || intent.Quantity != null || intent.Persona != null || intent.Items.Count > 0 || pickShowtimeId != null;
        if (!understood)
            return Reply("Mình chưa hiểu ý bạn. Bạn có thể nói như: \"Tối thứ 7 này 3 người xem phim hành động phụ đề sau 19h, 3 ghế đẹp nhất\".");

        Merge(c, intent);
        if (pickShowtimeId is int picked) c.ShowtimeId = picked;

        // Chỉ tìm suất
        if (intent.SearchOnly && c.ShowtimeId == null)
        {
            session.Pending = null;
            var found = _tools.SearchShowtimes(Search(c, ctx));
            session.LastShowtimes = found.Take(6).ToList();
            if (found.Count == 0) return Reply($"Mình chưa tìm thấy suất nào {Describe(c)}. Bạn thử đổi ngày, giờ hoặc thể loại nhé.");
            return Reply($"Có {found.Count} suất {Describe(c)}. Bạn chọn một suất để mình gợi ý ghế:", list: session.LastShowtimes);
        }

        // Đặt vé: cần ngày (hoặc "suất gần nhất" hay đã chọn suất) và số người
        bool hasWhen = c.Dates.Count > 0 || c.Earliest || c.ShowtimeId != null;
        if (!hasWhen && c.Quantity == null) { session.Pending = "date"; return Reply("Bạn muốn xem ngày nào và đi mấy người?"); }
        if (!hasWhen) { session.Pending = "date"; return Reply("Bạn muốn xem vào ngày nào?"); }
        if (c.Quantity == null) { session.Pending = "quantity"; return Reply("Bạn đi mấy người?"); }
        session.Pending = null;

        List<AgentShowtime> candidates;
        if (c.ShowtimeId is int sid)
        {
            candidates = _tools.SearchShowtimes(new ShowtimeSearch(Array.Empty<DateTime>(), CinemaId: null)).Where(s => s.ShowtimeId == sid).ToList();
            if (candidates.Count == 0) { c.ShowtimeId = null; return Reply("Suất này đã hết giờ bán online. Bạn chọn suất khác nhé."); }
        }
        else candidates = _tools.SearchShowtimes(Search(c, ctx));
        if (candidates.Count == 0 && ctx.CinemaId != null && c.ShowtimeId == null)
            candidates = _tools.SearchShowtimes(Search(c, ctx) with { CinemaId = null });
        if (candidates.Count == 0) return Reply($"Mình chưa tìm thấy suất nào còn đủ {c.Quantity} ghế {Describe(c)}. Bạn thử đổi ngày, giờ hoặc bớt điều kiện nhé.");

        string? blocked = null;
        foreach (var st in candidates.Take(12))
        {
            var card = TryBuild(st, c, ctx, ref blocked);
            if (card == null) continue;
            session.Card = card;
            session.LastShowtimes = candidates.Where(x => x.ShowtimeId != st.ShowtimeId).Take(3).ToList();
            return Reply(CardText(card, c), card);
        }
        return Reply(blocked ?? $"Các suất {Describe(c)} không còn cụm {c.Quantity} ghế liền nhau. Bạn thử suất khác hoặc bớt số người nhé.");
    }

    /// <summary>check_rules → get_best_seats → find_combo → build_preview cho một suất; null nếu suất không phù hợp.</summary>
    private AgentCard? TryBuild(AgentShowtime st, AgentCriteria c, AgentContext ctx, ref string? blocked)
    {
        var rules = _tools.CheckRules(st.ShowtimeId, c.Quantity!.Value, ctx.UserId, c.ForChild);
        if (!rules.Ok) { blocked ??= rules.Message; return null; }
        var seats = _tools.GetBestSeats(st.ShowtimeId, c.Quantity.Value, c.Persona);
        if (seats == null || seats.Seats.Count < c.Quantity.Value) return null;
        var items = c.Items.Select(i => _tools.FindCombo(i.Query, i.Quantity, st.CinemaId)).OfType<AgentItem>().ToList();
        return _tools.BuildPreview(st.ShowtimeId, seats.Seats.Select(x => x.SeatId).ToList(), items, seats.Persona, rules.Notes);
    }

    private AgentReply AddItems(AgentSession session, BookingIntent intent, AgentContext ctx)
    {
        var card = session.Card;
        if (card == null)
        {
            session.Criteria.Items.AddRange(intent.Items);
            return Reply("Bạn chưa có thẻ đặt vé nào. Bạn muốn xem phim gì, ngày nào và đi mấy người?");
        }
        var items = card.Items.ToList();
        var added = new List<string>();
        foreach (var i in intent.Items)
        {
            var f = _tools.FindCombo(i.Query, i.Quantity, ctx.CinemaId);
            if (f == null) continue;
            var idx = items.FindIndex(x => x.DrinkId == f.DrinkId);
            if (idx >= 0) items[idx] = items[idx] with { Quantity = items[idx].Quantity + f.Quantity };
            else items.Add(f);
            added.Add($"{f.Quantity} × {f.Name}");
        }
        if (added.Count == 0) return Reply("Món này hiện không còn bán ở rạp. Bạn chọn món khác nhé.", card);
        var updated = _tools.BuildPreview(card.ShowtimeId, card.Seats.Select(s => s.SeatId).ToList(), items, card.Persona, card.Notes);
        if (updated == null) return Reply("Suất này không còn bán online. Bạn nói \"Thôi, làm lại\" để tìm suất khác nhé.", card);
        session.Card = updated;
        session.Criteria.Items = items.Select(x => new ParsedItem(x.DrinkId, x.Name, x.Quantity)).ToList();
        return Reply($"Đã thêm {string.Join(", ", added)} vào thẻ đặt vé. Tổng tạm tính {Fmt.Money(updated.Total)}.", updated);
    }

    private AgentReply NextShowtime(AgentSession session, AgentContext ctx)
    {
        var card = session.Card;
        if (card == null) return Reply("Bạn chưa có thẻ đặt vé nào để đổi suất. Bạn muốn xem phim gì, ngày nào?");
        var c = session.Criteria;
        c.Quantity ??= card.Quantity;
        c.Persona ??= card.Persona;
        var next = _tools.SearchShowtimes(new ShowtimeSearch(Array.Empty<DateTime>(), MovieId: card.MovieId, Quantity: card.Quantity, After: card.StartTime))
            .Where(s => s.StartTime > card.StartTime).ToList();
        string? blocked = null;
        foreach (var st in next.Take(8))
        {
            var rules = _tools.CheckRules(st.ShowtimeId, card.Quantity, ctx.UserId, c.ForChild);
            if (!rules.Ok) { blocked ??= rules.Message; continue; }
            var seats = _tools.GetBestSeats(st.ShowtimeId, card.Quantity, card.Persona);
            if (seats == null || seats.Seats.Count < card.Quantity) continue;
            var updated = _tools.BuildPreview(st.ShowtimeId, seats.Seats.Select(x => x.SeatId).ToList(), card.Items, seats.Persona, rules.Notes);
            if (updated == null) continue;
            session.Card = updated;
            c.ShowtimeId = st.ShowtimeId;
            return Reply($"Đã đổi sang suất kế tiếp {updated.Time} {updated.Date} tại {updated.Cinema} – {updated.Room}. Ghế đề xuất {updated.Range} ({updated.Score}/100).", updated);
        }
        return Reply(blocked ?? $"Phim {card.Title} không còn suất nào sau {card.Time} {card.Date} đủ {card.Quantity} ghế liền nhau.", card);
    }

    /// <summary>Gộp các trường mới vào tiêu chí của phiên; câu có tiêu chí tìm suất mới thì bỏ suất đã chọn trước đó.</summary>
    public static void Merge(AgentCriteria c, BookingIntent i)
    {
        if (i.HasSearchCriteria) c.ShowtimeId = null;
        if (i.Dates.Count > 0) { c.Dates = i.Dates.ToList(); c.Earliest = false; }
        if (i.Earliest) { c.Earliest = true; c.Dates.Clear(); }
        if (i.TimeFrom != null || i.TimeTo != null) { c.TimeFrom = i.TimeFrom; c.TimeTo = i.TimeTo; }
        if (i.Quantity != null) c.Quantity = i.Quantity;
        if (i.Genre != null) c.Genre = i.Genre;
        if (i.LanguageFormat != null) c.LanguageFormat = i.LanguageFormat;
        if (i.Format != null) c.Format = i.Format;
        if (i.Persona != null) c.Persona = i.Persona;
        if (i.MovieId != null) { c.MovieId = i.MovieId; c.MovieTitle = i.MovieTitle; }
        if (i.ForChild) c.ForChild = true;
        if (i.Items.Count > 0) c.Items = i.Items.ToList();
    }

    public static ShowtimeSearch Search(AgentCriteria c, AgentContext ctx) =>
        new(c.Earliest ? Array.Empty<DateTime>() : c.Dates, c.TimeFrom, c.TimeTo, c.Genre, c.LanguageFormat, c.Format, c.MovieId, null, ctx.CinemaId, c.Quantity);

    /// <summary>Mô tả ngắn tiêu chí, ví dụ "phim Hành động, Phụ đề, thứ Bảy 15/11 từ 19:00".</summary>
    public static string Describe(AgentCriteria c)
    {
        var parts = new List<string>();
        if (c.MovieTitle != null) parts.Add($"phim {c.MovieTitle}");
        else if (c.Genre != null) parts.Add($"phim {StatusDisplay.Of(StatusDisplay.Genres, c.Genre).Label.ToLowerInvariant()}");
        if (c.LanguageFormat != null) parts.Add(StatusDisplay.Of(StatusDisplay.Language, c.LanguageFormat).Label.ToLowerInvariant());
        if (c.Format != null) parts.Add(c.Format);
        if (c.Earliest) parts.Add("gần nhất");
        else if (c.Dates.Count > 0) parts.Add(string.Join(" và ", c.Dates.Select(Fmt.WeekdayDate)));
        if (c.TimeFrom != null && c.TimeTo != null) parts.Add($"{c.TimeFrom:hh\\:mm}–{c.TimeTo:hh\\:mm}");
        return parts.Count == 0 ? "phù hợp" : string.Join(", ", parts);
    }

    /// <summary>Tên sở thích ghế dùng trong câu trả lời (cùng nhãn với segmented persona ở trang đặt vé).</summary>
    public static string PersonaLabel(string? persona) => persona switch
    {
        SeatPersona.NearScreen => "gần màn hình",
        SeatPersona.PrivateCouple => "đôi riêng tư",
        SeatPersona.NearAisle => "gần lối đi",
        _ => "đẹp nhất",
    };

    private static string CardText(AgentCard card, AgentCriteria c)
    {
        var persona = PersonaLabel(card.Persona);
        var text = $"Mình tìm được suất {card.Time} {card.Date} phim {card.Title} tại {card.Cinema} – {card.Room}. {card.Quantity} ghế {persona}: {card.Range} ({card.Score}/100).";
        if (c.Items.Count > 0 && card.Items.Count == 0) text += " Món bạn chọn hiện không còn bán ở rạp này.";
        return text + " Bấm \"Giữ ghế & thanh toán ngay\" để giữ ghế.";
    }
}
