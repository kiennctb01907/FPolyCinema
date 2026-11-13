using System.Globalization;
using System.Security;
using System.Text;
using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>Loại hình vẽ của ảnh món (UI mục 7, phần Ảnh mẫu).</summary>
public enum DrinkArtKind
{
    Popcorn,
    Drink,
    Snack,
    Combo,
}

/// <summary>
/// Sinh ảnh mẫu dạng SVG ở server (UI mục 7, phần Ảnh mẫu). Chỉ ghép chuỗi XML, không dùng thư viện đồ họa
/// chỉ có trên Windows và không tải ảnh từ internet, nên chạy được cả trên Linux. Kết quả xác định: cùng đầu vào luôn ra cùng một file.
/// - Poster 600 × 900: nền gradient hai màu theo thể loại, họa tiết hình học nhẹ, tên phim chữ lớn (đậm 700, tự xuống dòng),
///   dòng phụ ghi thể loại và năm, nhãn tuổi ở góc trên.
/// - Ảnh món 600 × 600: hình phẳng đơn giản (xô bắp, ly nước, đồ ăn vặt, combo) trên nền màu nhạt.
/// </summary>
public static class SvgArtGenerator
{
    public const int PosterWidth = 600;
    public const int PosterHeight = 900;
    public const int DrinkSize = 600;

    private const string FontFamily = "'Be Vietnam Pro', 'Segoe UI', Roboto, Arial, sans-serif";

    // Hai màu gradient theo thể loại chính
    private static readonly Dictionary<string, (string From, string To)> GenrePalette = new()
    {
        [Genre.Action] = ("#7F1D1D", "#EA580C"),
        [Genre.Adventure] = ("#14532D", "#0EA5E9"),
        [Genre.Animation] = ("#7C3AED", "#F472B6"),
        [Genre.Comedy] = ("#B45309", "#FACC15"),
        [Genre.Drama] = ("#1E3A8A", "#6366F1"),
        [Genre.Family] = ("#0F766E", "#84CC16"),
        [Genre.Horror] = ("#0B0F19", "#7F1D1D"),
        [Genre.Romance] = ("#9D174D", "#FB7185"),
        [Genre.SciFi] = ("#0C4A6E", "#22D3EE"),
        [Genre.Thriller] = ("#111827", "#4B5563"),
        [Genre.Documentary] = ("#3F3F46", "#A8A29E"),
    };

    // Màu nhãn tuổi lấy đúng token --age-* của UI mục 2; chữ dùng --age-fg, riêng nhãn C chữ trắng
    private static readonly Dictionary<string, string> AgeColors = new()
    {
        [AgeRating.P] = "#10B981",
        [AgeRating.K] = "#3B82F6",
        [AgeRating.T13] = "#F59E0B",
        [AgeRating.T16] = "#F97316",
        [AgeRating.T18] = "#EF4444",
        [AgeRating.C] = "#6B7280",
    };

    private static readonly Dictionary<string, string> GenreNames = new()
    {
        [Genre.Action] = "Hành động",
        [Genre.Adventure] = "Phiêu lưu",
        [Genre.Animation] = "Hoạt hình",
        [Genre.Comedy] = "Hài",
        [Genre.Drama] = "Tâm lý",
        [Genre.Family] = "Gia đình",
        [Genre.Horror] = "Kinh dị",
        [Genre.Romance] = "Tình cảm",
        [Genre.SciFi] = "Khoa học viễn tưởng",
        [Genre.Thriller] = "Giật gân",
        [Genre.Documentary] = "Tài liệu",
    };

    /// <summary>Tên thể loại tiếng Việt để in trên poster.</summary>
    public static string GenreDisplayName(string genre) => GenreNames.TryGetValue(genre, out var n) ? n : genre;

    /// <summary>Poster phim 600 × 900.</summary>
    public static string Poster(string title, IReadOnlyList<string> genres, int year, string ageRating)
    {
        var main = genres.Count > 0 ? genres[0] : Genre.Drama;
        var (from, to) = GenrePalette.TryGetValue(main, out var p) ? p : GenrePalette[Genre.Drama];
        var seed = StableHash(title);

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{PosterWidth}\" height=\"{PosterHeight}\" viewBox=\"0 0 {PosterWidth} {PosterHeight}\" role=\"img\" aria-label=\"{Esc(title)}\">\n");
        sb.Append("  <defs>\n");
        sb.Append($"    <linearGradient id=\"bg\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop offset=\"0\" stop-color=\"{from}\"/><stop offset=\"1\" stop-color=\"{to}\"/></linearGradient>\n");
        sb.Append("    <linearGradient id=\"shade\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0.45\" stop-color=\"#000000\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#000000\" stop-opacity=\"0.65\"/></linearGradient>\n");
        sb.Append("  </defs>\n");
        sb.Append($"  <rect width=\"{PosterWidth}\" height=\"{PosterHeight}\" fill=\"url(#bg)\"/>\n");

        // Họa tiết hình học nhẹ: vòng tròn và đường chéo, vị trí suy ra từ tên phim
        sb.Append("  <g fill=\"none\" stroke=\"#FFFFFF\" stroke-opacity=\"0.12\" stroke-width=\"2\">\n");
        for (int i = 0; i < 5; i++)
        {
            int cx = (int)((seed >> (i * 3)) % 600);
            int cy = 80 + (int)((seed >> (i * 5 + 1)) % 520);
            int r = 60 + i * 38;
            sb.Append($"    <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\"/>\n");
        }
        for (int i = 0; i < 6; i++)
        {
            int x = -200 + i * 140;
            sb.Append($"    <line x1=\"{x}\" y1=\"0\" x2=\"{x + 400}\" y2=\"{PosterHeight}\"/>\n");
        }
        sb.Append("  </g>\n");
        sb.Append($"  <rect width=\"{PosterWidth}\" height=\"{PosterHeight}\" fill=\"url(#shade)\"/>\n");

        // Nhãn tuổi ở góc trên bên phải
        var ageColor = AgeColors.TryGetValue(ageRating, out var ac) ? ac : AgeColors[AgeRating.C];
        var ageText = ageRating == AgeRating.C ? "#FFFFFF" : "#0B0F19";
        sb.Append($"  <rect x=\"468\" y=\"32\" width=\"100\" height=\"56\" rx=\"10\" fill=\"{ageColor}\"/>\n");
        sb.Append($"  <text x=\"518\" y=\"70\" text-anchor=\"middle\" font-family=\"{FontFamily}\" font-size=\"30\" font-weight=\"700\" fill=\"{ageText}\">{Esc(ageRating)}</text>\n");

        // Thương hiệu nhỏ ở đáy poster (góc trên trái để trống cho nhãn tuổi mà thẻ phim phủ lên, UI U5-13)
        sb.Append($"  <text x=\"36\" y=\"872\" font-family=\"{FontFamily}\" font-size=\"22\" font-weight=\"600\" fill=\"#FFFFFF\" fill-opacity=\"0.85\">FPoly Cinema</text>\n");

        // Tên phim: chữ lớn, đậm, tự xuống dòng; tên dài thì giảm cỡ chữ
        var fontSize = title.Length > 32 ? 46 : title.Length > 20 ? 54 : 64;
        // Chữ hoa đậm rộng trung bình khoảng 0,68 em; vùng chữ rộng 528px (lề trái và phải 36px)
        var maxChars = (int)(528 / (fontSize * 0.68));
        var lines = WrapText(title.ToUpper(CultureInfo.GetCultureInfo("vi-VN")), maxChars, 4);
        var lineHeight = (int)(fontSize * 1.15);
        var subtitleY = 820;
        var firstY = subtitleY - 40 - (lines.Count - 1) * lineHeight;
        sb.Append($"  <g font-family=\"{FontFamily}\" font-weight=\"700\" font-size=\"{fontSize}\" fill=\"#FFFFFF\">\n");
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append($"    <text x=\"36\" y=\"{firstY + i * lineHeight}\">{Esc(lines[i])}</text>\n");
        }
        sb.Append("  </g>\n");

        // Dòng phụ: thể loại và năm
        var genreText = string.Join(" • ", genres.Take(2).Select(GenreDisplayName));
        sb.Append($"  <text x=\"36\" y=\"{subtitleY}\" font-family=\"{FontFamily}\" font-size=\"26\" font-weight=\"500\" fill=\"#FFFFFF\" fill-opacity=\"0.9\">{Esc(genreText)} • {year}</text>\n");
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>Ảnh món 600 × 600 trên nền màu nhạt.</summary>
    public static string DrinkImage(DrinkArtKind kind, string name)
    {
        var bg = kind switch
        {
            DrinkArtKind.Popcorn => "#FEF3C7",
            DrinkArtKind.Drink => "#E0F2FE",
            DrinkArtKind.Snack => "#FFE4E6",
            _ => "#EDE9FE",
        };
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{DrinkSize}\" height=\"{DrinkSize}\" viewBox=\"0 0 {DrinkSize} {DrinkSize}\" role=\"img\" aria-label=\"{Esc(name)}\">\n");
        sb.Append($"  <rect width=\"{DrinkSize}\" height=\"{DrinkSize}\" fill=\"{bg}\"/>\n");
        sb.Append("  <ellipse cx=\"300\" cy=\"520\" rx=\"190\" ry=\"26\" fill=\"#000000\" fill-opacity=\"0.08\"/>\n");
        switch (kind)
        {
            case DrinkArtKind.Popcorn:
                AppendPopcorn(sb, 300, 0, 1.0);
                break;
            case DrinkArtKind.Drink:
                AppendCup(sb, 300, 0, 1.0, "#2563EB");
                break;
            case DrinkArtKind.Snack:
                AppendSnack(sb);
                break;
            default:
                AppendPopcorn(sb, 220, 20, 0.85);
                AppendCup(sb, 410, 40, 0.8, "#DC2626");
                break;
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    private static void AppendPopcorn(StringBuilder sb, int cx, int dy, double s)
    {
        string P(double x, double y) => $"{F(cx + x * s)},{F(dy + 300 + (y - 300) * s)}";
        // Bắp nổ phía trên miệng xô
        sb.Append("  <g fill=\"#FFFBEB\" stroke=\"#F59E0B\" stroke-width=\"4\">\n");
        double[][] puffs = { new[] { -95.0, 205 }, new[] { -45.0, 180 }, new[] { 5.0, 170 }, new[] { 55.0, 182 }, new[] { 100.0, 205 }, new[] { -70.0, 150 }, new[] { -10.0, 135 }, new[] { 50.0, 148 } };
        foreach (var pt in puffs)
        {
            sb.Append($"    <circle cx=\"{F(cx + pt[0] * s)}\" cy=\"{F(dy + 300 + (pt[1] - 300) * s)}\" r=\"{F(38 * s)}\"/>\n");
        }
        sb.Append("  </g>\n");
        // Xô sọc đỏ trắng
        sb.Append($"  <polygon points=\"{P(-130, 220)} {P(130, 220)} {P(100, 500)} {P(-100, 500)}\" fill=\"#FFFFFF\" stroke=\"#B91C1C\" stroke-width=\"6\"/>\n");
        for (int i = 0; i < 3; i++)
        {
            double x0 = -104 + i * 78;
            sb.Append($"  <polygon points=\"{P(x0, 220)} {P(x0 + 36, 220)} {P(x0 + 30, 500)} {P(x0 + 4, 500)}\" fill=\"#DC2626\"/>\n");
        }
        sb.Append($"  <rect x=\"{F(cx - 140 * s)}\" y=\"{F(dy + 300 + (205 - 300) * s)}\" width=\"{F(280 * s)}\" height=\"{F(26 * s)}\" rx=\"{F(8 * s)}\" fill=\"#B91C1C\"/>\n");
    }

    private static void AppendCup(StringBuilder sb, int cx, int dy, double s, string color)
    {
        string P(double x, double y) => $"{F(cx + x * s)},{F(dy + 300 + (y - 300) * s)}";
        // Ống hút, nắp, thân ly và nhãn
        sb.Append($"  <rect x=\"{F(cx + 20 * s)}\" y=\"{F(dy + 300 + (90 - 300) * s)}\" width=\"{F(18 * s)}\" height=\"{F(120 * s)}\" rx=\"{F(6 * s)}\" fill=\"#F59E0B\" transform=\"rotate(12 {F(cx + 29 * s)} {F(dy + 300 + (150 - 300) * s)})\"/>\n");
        sb.Append($"  <rect x=\"{F(cx - 120 * s)}\" y=\"{F(dy + 300 + (185 - 300) * s)}\" width=\"{F(240 * s)}\" height=\"{F(36 * s)}\" rx=\"{F(14 * s)}\" fill=\"#E5E7EB\" stroke=\"#9CA3AF\" stroke-width=\"4\"/>\n");
        sb.Append($"  <polygon points=\"{P(-110, 220)} {P(110, 220)} {P(85, 500)} {P(-85, 500)}\" fill=\"{color}\"/>\n");
        sb.Append($"  <polygon points=\"{P(-100, 300)} {P(100, 300)} {P(93, 380)} {P(-93, 380)}\" fill=\"#FFFFFF\" fill-opacity=\"0.9\"/>\n");
        sb.Append($"  <circle cx=\"{F(cx)}\" cy=\"{F(dy + 300 + (340 - 300) * s)}\" r=\"{F(22 * s)}\" fill=\"{color}\"/>\n");
    }

    private static void AppendSnack(StringBuilder sb)
    {
        // Khay giấy với xúc xích và bánh mì
        sb.Append("  <path d=\"M120 330 L480 330 L440 470 L160 470 Z\" fill=\"#FFFFFF\" stroke=\"#B45309\" stroke-width=\"6\"/>\n");
        sb.Append("  <rect x=\"150\" y=\"250\" width=\"300\" height=\"90\" rx=\"45\" fill=\"#F59E0B\"/>\n");
        sb.Append("  <rect x=\"135\" y=\"265\" width=\"330\" height=\"52\" rx=\"26\" fill=\"#B91C1C\"/>\n");
        sb.Append("  <path d=\"M170 285 Q200 265 230 285 T290 285 T350 285 T410 285 T440 285\" fill=\"none\" stroke=\"#FACC15\" stroke-width=\"8\" stroke-linecap=\"round\"/>\n");
        sb.Append("  <rect x=\"160\" y=\"380\" width=\"280\" height=\"18\" rx=\"9\" fill=\"#FDE68A\"/>\n");
    }

    /// <summary>Tách chữ thành tối đa maxLines dòng, mỗi dòng không quá maxChars ký tự (cắt theo từ).</summary>
    public static List<string> WrapText(string text, int maxChars, int maxLines)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var cur = new StringBuilder();
        foreach (var w in words)
        {
            if (cur.Length > 0 && cur.Length + 1 + w.Length > maxChars)
            {
                lines.Add(cur.ToString());
                cur.Clear();
            }
            if (cur.Length > 0) cur.Append(' ');
            cur.Append(w);
        }
        if (cur.Length > 0) lines.Add(cur.ToString());
        if (lines.Count > maxLines)
        {
            var kept = lines.Take(maxLines).ToList();
            kept[^1] = kept[^1].TrimEnd() + "…";
            return kept;
        }
        return lines;
    }

    private static string Esc(string s) => SecurityElement.Escape(s) ?? string.Empty;

    private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Băm ổn định (không đổi giữa các lần chạy) để chọn vị trí họa tiết.</summary>
    private static ulong StableHash(string s)
    {
        ulong h = 1469598103934665603UL;
        foreach (var ch in s)
        {
            h ^= ch;
            h *= 1099511628211UL;
        }
        return h;
    }
}
