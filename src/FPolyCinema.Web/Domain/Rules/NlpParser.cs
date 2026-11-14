using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Lệnh điều khiển hội thoại của trợ lý đặt vé (SPEC 4.2 câu 8–10).</summary>
public enum AgentCommand
{
    None,
    /// <summary>"Thôi, làm lại": xóa thẻ đặt vé hiện tại.</summary>
    Reset,
    /// <summary>"Đổi sang suất sau đó": suất kế tiếp của cùng phim.</summary>
    NextShowtime,
    /// <summary>"Thêm 2 nước suối" (câu nói tiếp): thêm món vào thẻ đặt vé hiện tại.</summary>
    AddItems,
}

/// <summary>Một mục trong từ điển tên riêng (phim hoặc món bắp nước) để so khớp không dấu.</summary>
public sealed record NlpCatalogItem(int Id, string Name, string? AltName = null);

/// <summary>Một món khách nhắc tới: món đã nhận ra (DrinkId) hoặc chỉ là chữ chung như "combo" (DrinkId null), kèm số lượng.</summary>
public sealed record ParsedItem(int? DrinkId, string Query, int Quantity);

/// <summary>Các trường trích được từ một câu của khách (SPEC 4.2 "Các trường cần trích").</summary>
public sealed class BookingIntent
{
    /// <summary>Các ngày (ngày vận hành) khách muốn xem; "cuối tuần" cho 2 ngày. Rỗng là chưa nói ngày.</summary>
    public List<DateTime> Dates { get; } = new();
    public TimeSpan? TimeFrom { get; set; }
    public TimeSpan? TimeTo { get; set; }
    public int? Quantity { get; set; }
    public string? Genre { get; set; }
    public string? LanguageFormat { get; set; }
    public string? Format { get; set; }
    public string? Persona { get; set; }
    public int? MovieId { get; set; }
    public string? MovieTitle { get; set; }
    public List<ParsedItem> Items { get; } = new();
    /// <summary>"Suất gần nhất", "suất sớm nhất": lấy suất sớm nhất còn bán, không cần ngày.</summary>
    public bool Earliest { get; set; }
    /// <summary>Câu hỏi "có … không", "phim nào": chỉ tìm suất, không dựng thẻ đặt vé.</summary>
    public bool SearchOnly { get; set; }
    /// <summary>Khách nhắc tới trẻ em ("cho bé", "trẻ em"): nhắc quy định giờ chiếu cho trẻ em.</summary>
    public bool ForChild { get; set; }
    public AgentCommand Command { get; set; }

    /// <summary>Câu có ít nhất một tiêu chí tìm suất (ngày, giờ, phim, thể loại, ngôn ngữ, định dạng, "suất gần nhất").</summary>
    public bool HasSearchCriteria => Dates.Count > 0 || TimeFrom != null || TimeTo != null || Genre != null || LanguageFormat != null
                                     || Format != null || MovieId != null || Earliest;
}

/// <summary>
/// NlpParser của LocalAgent (SPEC 4.2-01, 4.2-09 → 4.2-14): dựa trên luật và từ điển tiếng Việt, so khớp không dấu. Logic thuần, có unit test
/// (SPEC 7 S7-05, ít nhất 20 câu). Các từ trùng nhau khi bỏ dấu ("sáng"/"sang", "tối"/"tôi", "mốt"/"một", "hài"/"hai") được xét trên chữ còn dấu.
/// - Ngày: hôm nay, mai, mốt, tối nay, "thứ 2…chủ nhật này" (gần nhất kể cả hôm nay, "tuần sau" cộng 7 ngày), cuối tuần (thứ Bảy và Chủ nhật gần nhất), dd/MM.
/// - Khung giờ: sáng từ OperatingDayStart đến 12:00, chiều 12:00–18:00, tối 18:00–23:59; "sau 19h" từ 19:00; "trước 20h" đến 20:00;
///   "8 giờ tối" là 20:00 ± 60 phút.
/// - Số lượng: "3 người", "2 vé", "4 ghế"; "cặp đôi" là 2.
/// - Thể loại, ngôn ngữ (phụ đề, lồng tiếng, bản gốc), định dạng (2D, 3D, IMAX, 4DX), sở thích ghế (persona CONTRACTS C3-29).
/// - Tên phim (so khớp gần đúng, không dấu) và món bắp nước kèm số lượng.
/// </summary>
public static class NlpParser
{
    public static readonly TimeSpan Noon = new(12, 0, 0);
    public static readonly TimeSpan EveningStart = new(18, 0, 0);
    public static readonly TimeSpan DayEnd = new(23, 59, 0);
    /// <summary>"8 giờ tối" hiểu là 20:00 ± 60 phút.</summary>
    public const int AroundMinutes = 60;

    private static readonly Dictionary<string, int> NumberWords = new()
    {
        ["mot"] = 1, ["hai"] = 2, ["ba"] = 3, ["bon"] = 4, ["tu"] = 4, ["nam"] = 5, ["sau"] = 6, ["bay"] = 7, ["tam"] = 8, ["chin"] = 9, ["muoi"] = 10,
    };

    private const string NumberPattern = @"(\d{1,2}|mot|hai|ba|bon|nam|sau|bay|tam|chin|muoi)";

    private static readonly (string Pattern, string Genre)[] Genres =
    {
        (@"hanh dong|action", Genre.Action),
        (@"phieu luu", Genre.Adventure),
        (@"hoat hinh|anime|cartoon", Genre.Animation),
        (@"hai huoc|vui nhon", Genre.Comedy),
        (@"tam ly|chinh kich", Genre.Drama),
        (@"gia dinh", Genre.Family),
        (@"kinh di|ma quai", Genre.Horror),
        (@"tinh cam|lang man|tinh yeu", Genre.Romance),
        (@"vien tuong|khoa hoc", Genre.SciFi),
        (@"giat gan|ly ky|trinh tham|hinh su", Genre.Thriller),
        (@"tai lieu", Genre.Documentary),
    };

    private static readonly (string Pattern, string Persona)[] Personas =
    {
        (@"gan loi di|canh loi di|sat loi di|ngoi loi di|dau day|dau hang", SeatPersona.NearAisle),
        (@"gan man hinh|bi can|can thi|hang dau|ngoi truoc|sat man hinh", SeatPersona.NearScreen),
        (@"rieng tu|ghe doi|sweetbox|ghe cap doi|kin dao", SeatPersona.PrivateCouple),
        (@"giua phong|dep nhat|trung tam|chinh giua|tot nhat|view dep|ngoi giua", SeatPersona.BestViewAudio),
    };

    private static readonly Dictionary<string, DayOfWeek> Weekdays = new()
    {
        ["2"] = DayOfWeek.Monday, ["hai"] = DayOfWeek.Monday,
        ["3"] = DayOfWeek.Tuesday, ["ba"] = DayOfWeek.Tuesday,
        ["4"] = DayOfWeek.Wednesday, ["tu"] = DayOfWeek.Wednesday,
        ["5"] = DayOfWeek.Thursday, ["nam"] = DayOfWeek.Thursday,
        ["6"] = DayOfWeek.Friday, ["sau"] = DayOfWeek.Friday,
        ["7"] = DayOfWeek.Saturday, ["bay"] = DayOfWeek.Saturday,
    };

    /// <summary>Chữ thường, bỏ dấu (đ → d), chỉ giữ chữ, số, "/" và ":"; khoảng trắng gộp một.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lower = text.ToLowerInvariant().Replace('đ', 'd');
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower.Normalize(NormalizationForm.FormD))
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) || ch == '/' || ch == ':' ? ch : ' ');
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>Độ giống nhau 0–1 của hai chuỗi đã chuẩn hóa (1 − khoảng cách Levenshtein / độ dài lớn hơn).</summary>
    public static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return 1.0 - (double)d[a.Length, b.Length] / Math.Max(a.Length, b.Length);
    }

    /// <summary>
    /// Phân tích một câu. today là ngày vận hành hiện tại; dayStart là OperatingDayStart (đầu khung "sáng").
    /// movies và drinks là từ điển tên phim và món đang bán để so khớp gần đúng, không dấu.
    /// </summary>
    public static BookingIntent Parse(string? text, DateTime today, TimeSpan dayStart, IReadOnlyList<NlpCatalogItem> movies, IReadOnlyList<NlpCatalogItem> drinks)
    {
        var intent = new BookingIntent();
        var lower = (text ?? string.Empty).ToLowerInvariant().Normalize(NormalizationForm.FormC);
        var norm = " " + Normalize(text) + " ";
        today = today.Date;

        // Lệnh điều khiển trước
        if (Has(norm, @"lam lai|bat dau lai|xoa the|xoa het|huy the|huy het|khong dat nua|reset") || Regex.IsMatch(norm, @"^\s*thoi\s*$"))
        {
            intent.Command = AgentCommand.Reset;
            return intent;
        }

        // Tên phim trước để chữ trong tên phim (ví dụ "Mai") không bị hiểu nhầm là ngày, giờ, thể loại
        norm = MatchMovie(norm, movies, intent);
        norm = MatchItems(norm, drinks, intent);

        if (Has(norm, @"suat (sau|ke tiep|tiep theo|muon hon|ke)(?!\s*\d)|suat sau do|doi suat khac")) intent.Command = AgentCommand.NextShowtime;
        if (Has(norm, @"suat (gan|som) nhat|som nhat|gan nhat|suat sap chieu")) intent.Earliest = true;

        ParseDates(lower, norm, today, intent);
        ParseTimes(lower, norm, dayStart, intent);
        ParseQuantity(norm, intent);

        foreach (var (p, g) in Genres)
            if (Has(norm, p)) { intent.Genre = g; break; }
        if (intent.Genre == null && Regex.IsMatch(lower, @"\bhài\b")) intent.Genre = Genre.Comedy;

        if (Has(norm, @"phu de|vietsub|sub viet")) intent.LanguageFormat = LanguageFormat.Subtitled;
        else if (Has(norm, @"long tieng|thuyet minh")) intent.LanguageFormat = LanguageFormat.Dubbed;
        else if (Has(norm, @"ban goc|tieng goc|nguyen ban|khong phu de")) intent.LanguageFormat = LanguageFormat.Original;

        if (Has(norm, "imax")) intent.Format = AuditoriumFormat.IMAX;
        else if (Has(norm, "4dx")) intent.Format = AuditoriumFormat.F4DX;
        else if (Has(norm, "3d")) intent.Format = AuditoriumFormat.F3D;
        else if (Has(norm, "2d")) intent.Format = AuditoriumFormat.F2D;

        foreach (var (p, persona) in Personas)
            if (Has(norm, p)) { intent.Persona = persona; break; }

        if (Has(norm, @"cho be|cho con|tre em|tre con|thieu nhi|em be|cac be|chau be")) intent.ForChild = true;

        if (intent.Command == AgentCommand.None && intent.Items.Count > 0 && Regex.IsMatch(norm, @"^\s*(them|lay them|cho them|goi them|them cho)\b")
            && !intent.HasSearchCriteria && intent.Quantity == null && intent.Persona == null)
            intent.Command = AgentCommand.AddItems;

        bool question = Has(norm, @"(co|con) .*(khong|ko)\s*$|nao khong|\bkhong\s*$|lich chieu|co suat|nhung suat|xem suat|tim suat|phim nao|suat nao|may gio");
        if (question && intent.Quantity == null && intent.Persona == null && intent.Items.Count == 0 && !Has(norm, @"\bdat\b|\bmua\b|\blay\b"))
            intent.SearchOnly = true;
        return intent;
    }

    private static bool Has(string norm, string pattern) => Regex.IsMatch(norm, @"(?<![a-z0-9])(" + pattern + @")(?![a-z0-9])");

    private static string Blank(string norm, int index, int length) => norm[..index] + new string(' ', length) + norm[(index + length)..];

    // ------------------------------------------------------------------ phim
    private static string MatchMovie(string norm, IReadOnlyList<NlpCatalogItem> movies, BookingIntent intent)
    {
        (NlpCatalogItem Item, int Index, int Length, double Score)? best = null;
        foreach (var m in movies)
        {
            foreach (var name in new[] { m.Name, m.AltName })
            {
                var key = Normalize(name);
                if (key.Length < 2) continue;
                bool shortTitle = !key.Contains(' ') && key.Length <= 4;
                // Tên rất ngắn (ví dụ "Mai") chỉ nhận khi đứng sau chữ "phim"
                var pattern = (shortTitle ? @"(?<=phim )" : @"(?<![a-z0-9])") + Regex.Escape(key) + @"(?![a-z0-9])";
                var exact = Regex.Match(norm, pattern);
                if (exact.Success)
                {
                    if (best == null || best.Value.Score < 1 || exact.Length > best.Value.Length) best = (m, exact.Index, exact.Length, 1);
                    continue;
                }
                if (shortTitle || key.Length < 5) continue;
                // Gần đúng: cửa sổ cùng số từ, độ giống từ 0,8
                var words = Regex.Matches(norm, @"[a-z0-9/:]+").Cast<Match>().ToList();
                int n = key.Split(' ').Length;
                for (int i = 0; i + n <= words.Count; i++)
                {
                    int start = words[i].Index, end = words[i + n - 1].Index + words[i + n - 1].Length;
                    var window = norm[start..end];
                    var sim = Similarity(window, key);
                    if (sim >= 0.8 && (best == null || sim > best.Value.Score)) best = (m, start, end - start, sim);
                }
            }
        }
        if (best == null) return norm;
        intent.MovieId = best.Value.Item.Id;
        intent.MovieTitle = best.Value.Item.Name;
        return Blank(norm, best.Value.Index, best.Value.Length);
    }

    // ------------------------------------------------------------------ bắp nước
    private static string MatchItems(string norm, IReadOnlyList<NlpCatalogItem> drinks, BookingIntent intent)
    {
        var keys = drinks.SelectMany(d => new[] { (d, Key: Normalize(Regex.Replace(d.Name, @"\(.*?\)", " "))), (d, Key: Normalize(d.Name)) })
            .Where(k => k.Key.Length >= 3).DistinctBy(k => (k.d.Id, k.Key)).OrderByDescending(k => k.Key.Length).ToList();
        var found = new List<(int Index, ParsedItem Item)>();
        foreach (var (d, key) in keys)
        {
            Match m;
            while ((m = Regex.Match(norm, @"(?<![a-z0-9])" + Regex.Escape(key) + @"(?![a-z0-9])")).Success)
            {
                var (qty, qIndex, qLength) = QuantityBefore(norm, m.Index);
                found.Add((m.Index, new ParsedItem(d.Id, d.Name, qty)));
                norm = Blank(norm, m.Index, m.Length);
                if (qLength > 0) norm = Blank(norm, qIndex, qLength);
            }
        }
        // "1 combo" chưa nói tên combo: để find_combo chọn
        Match g;
        while ((g = Regex.Match(norm, @"(?<![a-z0-9])combo(?![a-z0-9])")).Success)
        {
            var (qty, qIndex, qLength) = QuantityBefore(norm, g.Index);
            found.Add((g.Index, new ParsedItem(null, "combo", qty)));
            norm = Blank(norm, g.Index, g.Length);
            if (qLength > 0) norm = Blank(norm, qIndex, qLength);
        }
        foreach (var f in found.OrderBy(f => f.Index))
        {
            var same = intent.Items.FindIndex(i => i.DrinkId != null && i.DrinkId == f.Item.DrinkId);
            if (same >= 0) intent.Items[same] = intent.Items[same] with { Quantity = intent.Items[same].Quantity + f.Item.Quantity };
            else intent.Items.Add(f.Item);
        }
        return norm;
    }

    /// <summary>Số lượng ngay trước tên món ("2 nước suối", "một combo"); không có thì 1.</summary>
    private static (int Qty, int Index, int Length) QuantityBefore(string norm, int index)
    {
        var m = Regex.Match(norm[..index], NumberPattern + @"\s*(phan|ly|chai|xo|cai|hop|goi|suat)?\s*$");
        if (!m.Success || (m.Index > 0 && char.IsLetterOrDigit(norm[m.Index - 1]))) return (1, 0, 0);
        return (Math.Clamp(ToNumber(m.Groups[1].Value), 1, 20), m.Index, m.Length);
    }

    private static int ToNumber(string s) => int.TryParse(s, out var n) ? n : NumberWords.TryGetValue(s, out var w) ? w : 1;

    // ------------------------------------------------------------------ ngày
    private static void ParseDates(string lower, string norm, DateTime today, BookingIntent intent)
    {
        void Add(DateTime d) { if (!intent.Dates.Contains(d.Date)) intent.Dates.Add(d.Date); }

        bool nextWeek = Has(norm, @"tuan (sau|toi)");
        // "tuần sau": ngày đó của tuần kế tiếp (tuần tính từ thứ Hai đến Chủ nhật)
        var thisSunday = today.AddDays((7 - (int)today.DayOfWeek) % 7);
        DateTime Day(DayOfWeek d)
        {
            var x = Next(today, d);
            return nextWeek && x <= thisSunday ? x.AddDays(7) : x;
        }
        if (Has(norm, @"cuoi tuan"))
        {
            Add(Day(DayOfWeek.Saturday));
            Add(Day(DayOfWeek.Sunday));
            intent.Dates.Sort();
        }
        foreach (Match m in Regex.Matches(norm, @"(?<![a-z0-9])(?:thu\s*(2|3|4|5|6|7|hai|ba|tu|nam|sau|bay)|t([2-7]))(?![a-z0-9:])"))
        {
            var key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            Add(Day(Weekdays[key]));
        }
        if (Has(norm, @"chu nhat|cn")) Add(Day(DayOfWeek.Sunday));

        foreach (Match m in Regex.Matches(norm, @"(?<![0-9])(\d{1,2})/(\d{1,2})(?:/(\d{4}))?(?![0-9])"))
        {
            int day = int.Parse(m.Groups[1].Value), month = int.Parse(m.Groups[2].Value);
            int year = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : today.Year;
            if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) continue;
            var d = new DateTime(year, month, day);
            if (!m.Groups[3].Success && d < today) d = d.AddYears(1);
            Add(d);
        }

        if (intent.Dates.Count > 0) return;
        if (Has(norm, @"hom nay|toi nay|chieu nay|sang nay|trua nay|dem nay|bua nay")) Add(today);
        else if (Regex.IsMatch(lower, @"(?<![\p{L}\p{N}])mốt(?![\p{L}\p{N}])") || Has(norm, @"ngay mot|ngay kia")) Add(today.AddDays(2));
        else if (Has(norm, @"ngay mai|mai")) Add(today.AddDays(1));
    }

    /// <summary>Ngày gần nhất (kể cả hôm nay) rơi vào thứ đó.</summary>
    public static DateTime Next(DateTime today, DayOfWeek day) => today.Date.AddDays(((int)day - (int)today.DayOfWeek + 7) % 7);

    // ------------------------------------------------------------------ giờ
    private static void ParseTimes(string lower, string norm, TimeSpan dayStart, BookingIntent intent)
    {
        bool Word(string w) => Regex.IsMatch(lower, @"(?<![\p{L}\p{N}])" + w + @"(?![\p{L}\p{N}])");
        string? part = null;
        if (Word("sáng") || Has(norm, @"buoi sang|sang (nay|mai|som|chu nhat|cn|thu \w+|t[2-7])")) part = "morning";
        else if (Has(norm, @"chieu")) part = "afternoon";
        else if (Word("tối") || Has(norm, @"buoi toi|toi nay|toi mai|toi (thu \w+|chu nhat|cn|t[2-7])")) part = "evening";
        else if (Word("đêm") || Word("khuya") || Has(norm, @"dem nay|nua dem")) part = "night";

        (TimeSpan From, TimeSpan To)? range = part switch
        {
            "morning" => (dayStart, Noon),
            "afternoon" => (Noon, EveningStart),
            "evening" => (EveningStart, DayEnd),
            "night" => (new TimeSpan(21, 0, 0), DayEnd),
            _ => null,
        };

        var m = Regex.Match(norm, @"(?<![a-z0-9])(sau|tu|truoc|khoang|luc|tam)?\s*(\d{1,2})\s*(?:(?:h|g|gio)\s*(\d{1,2})?|:(\d{2}))(?:\s*(?:phut|p))?(?:\s*(sang|trua|chieu|toi|dem))?(?![a-z0-9])");
        if (m.Success)
        {
            int h = int.Parse(m.Groups[2].Value);
            int min = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0;
            var suffix = m.Groups[5].Value;
            bool pm = suffix is "chieu" or "toi" or "dem" || part is "afternoon" or "evening" or "night";
            if (h < 12 && (pm || (suffix != "sang" && part != "morning" && h < 8))) h += 12;
            if (h <= 23 && min < 60)
            {
                var t = new TimeSpan(h, min, 0);
                switch (m.Groups[1].Value)
                {
                    case "sau":
                    case "tu":
                        intent.TimeFrom = t;
                        intent.TimeTo = range is { } r1 && r1.To > t ? r1.To : DayEnd;
                        return;
                    case "truoc":
                        intent.TimeFrom = range is { } r2 && r2.From < t ? r2.From : dayStart;
                        intent.TimeTo = t;
                        return;
                    default:
                        intent.TimeFrom = Max(t - TimeSpan.FromMinutes(AroundMinutes), TimeSpan.Zero);
                        intent.TimeTo = Min(t + TimeSpan.FromMinutes(AroundMinutes), DayEnd);
                        return;
                }
            }
        }
        if (range is { } r)
        {
            intent.TimeFrom = r.From;
            intent.TimeTo = r.To;
        }
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    // ------------------------------------------------------------------ số lượng
    private static void ParseQuantity(string norm, BookingIntent intent)
    {
        var m = Regex.Match(norm, @"(?<![a-z0-9:/])" + NumberPattern + @"\s*(nguoi|ve|ghe|ban|dua|khach|cho ngoi|suat ve)(?![a-z0-9])");
        if (m.Success) intent.Quantity = Math.Clamp(ToNumber(m.Groups[1].Value), 1, 10);
        else if (Has(norm, @"cap doi|hai dua|vo chong|nguoi yeu|2 dua")) intent.Quantity = 2;
        else if (Has(norm, @"mot minh|1 minh|di le")) intent.Quantity = 1;
    }
}
