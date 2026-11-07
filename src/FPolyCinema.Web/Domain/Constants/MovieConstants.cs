namespace FPolyCinema.Web.Domain.Constants;

/// <summary>Nhãn phân loại phim theo độ tuổi (CONTRACTS mục 3, cột Movies.AgeRating).</summary>
public static class AgeRating
{
    public const string P = "P";
    public const string K = "K";
    public const string T13 = "T13";
    public const string T16 = "T16";
    public const string T18 = "T18";
    /// <summary>Phim không được phép phổ biến: chỉ lưu hồ sơ, không tạo suất.</summary>
    public const string C = "C";

    public static readonly string[] All = { P, K, T13, T16, T18, C };
}

/// <summary>Cảnh báo nội dung (Thông tư 05/2023). Movies.ContentWarnings lưu danh sách cách nhau dấu phẩy.</summary>
public static class ContentWarning
{
    public const string Theme = "THEME";
    public const string Violence = "VIOLENCE";
    public const string NuditySex = "NUDITY_SEX";
    public const string Drugs = "DRUGS";
    public const string Horror = "HORROR";
    public const string Language = "LANGUAGE";
    public const string DangerousBehavior = "DANGEROUS_BEHAVIOR";

    public static readonly string[] All = { Theme, Violence, NuditySex, Drugs, Horror, Language, DangerousBehavior };
}

/// <summary>Thể loại phim. Movies.Genres lưu danh sách cách nhau dấu phẩy.</summary>
public static class Genre
{
    public const string Action = "ACTION";
    public const string Adventure = "ADVENTURE";
    public const string Animation = "ANIMATION";
    public const string Comedy = "COMEDY";
    public const string Drama = "DRAMA";
    public const string Family = "FAMILY";
    public const string Horror = "HORROR";
    public const string Romance = "ROMANCE";
    public const string SciFi = "SCIFI";
    public const string Thriller = "THRILLER";
    public const string Documentary = "DOCUMENTARY";

    public static readonly string[] All = { Action, Adventure, Animation, Comedy, Drama, Family, Horror, Romance, SciFi, Thriller, Documentary };
}

/// <summary>Bản ngôn ngữ của suất chiếu (cột Showtimes.LanguageFormat; Movies.LanguageFormats là danh sách).</summary>
public static class LanguageFormat
{
    public const string Subtitled = "SUBTITLED";
    public const string Dubbed = "DUBBED";
    public const string Original = "ORIGINAL";

    public static readonly string[] All = { Subtitled, Dubbed, Original };
}

/// <summary>Tách và ghép các cột lưu danh sách cách nhau dấu phẩy (Genres, ContentWarnings, LanguageFormats).</summary>
public static class CsvList
{
    public static string[] Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Join(IEnumerable<string> values) => string.Join(",", values);
}
