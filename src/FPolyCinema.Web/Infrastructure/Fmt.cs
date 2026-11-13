using System.Globalization;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Định dạng hiển thị theo UI mục 9 (CultureInfo vi-VN): tiền "125.000 ₫", ngày "06/11/2026", giờ "19:30",
/// ngày trong tuần "T7, 07/11", thời lượng "2 giờ 05 phút". Dùng chung cho mọi view; JavaScript có bản tương ứng trong ui.js.
/// </summary>
public static class Fmt
{
    public static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Số nguyên có dấu chấm phân cách hàng nghìn, ví dụ 1.234.</summary>
    public static string Number(long value) => value.ToString("#,##0", Vi);

    /// <summary>Số thập phân tối đa 3 chữ số lẻ (định mức, tồn kho), ví dụ 0,15.</summary>
    public static string Decimal(decimal value) => value.ToString("#,##0.###", Vi);

    /// <summary>Tiền: "125.000 ₫".</summary>
    public static string Money(long amount) => Number(amount) + " ₫";

    public static string Money(long? amount) => amount.HasValue ? Money(amount.Value) : "–";

    /// <summary>Ngày: "06/11/2026".</summary>
    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Date(DateTime? value) => value.HasValue ? Date(value.Value) : "–";

    /// <summary>Giờ: "19:30".</summary>
    public static string Time(DateTime value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Ngày giờ: "06/11/2026 19:30".</summary>
    public static string DateTime(DateTime value) => Date(value) + " " + Time(value);

    public static string DateTime(DateTime? value) => value.HasValue ? DateTime(value.Value) : "–";

    /// <summary>Ngày trong tuần kèm ngày tháng: "T7, 07/11"; chủ nhật là "CN, 08/11".</summary>
    public static string WeekdayDate(DateTime value) => WeekdayShort(value.DayOfWeek) + ", " + value.ToString("dd/MM", CultureInfo.InvariantCulture);

    /// <summary>Tên ngắn của thứ: T2 … T7, CN.</summary>
    public static string WeekdayShort(DayOfWeek day) => day == DayOfWeek.Sunday ? "CN" : "T" + ((int)day + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Thời lượng theo phút: 125 → "2 giờ 05 phút", 120 → "2 giờ", 45 → "45 phút".</summary>
    public static string Duration(int minutes)
    {
        if (minutes < 0) minutes = 0;
        int h = minutes / 60, m = minutes % 60;
        if (h == 0) return m.ToString(CultureInfo.InvariantCulture) + " phút";
        if (m == 0) return h.ToString(CultureInfo.InvariantCulture) + " giờ";
        return h.ToString(CultureInfo.InvariantCulture) + " giờ " + m.ToString("D2", CultureInfo.InvariantCulture) + " phút";
    }

    /// <summary>Chữ cái đầu cho ảnh đại diện: "Trần Minh Quân" → "TQ"; một từ → một chữ.</summary>
    public static string Initials(string? fullName)
    {
        var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        var first = char.ToUpper(parts[0][0], Vi).ToString();
        return parts.Length == 1 ? first : first + char.ToUpper(parts[^1][0], Vi);
    }
}
