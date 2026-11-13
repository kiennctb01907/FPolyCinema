namespace FPolyCinema.Web.DAL;

/// <summary>Hỗ trợ tham số tìm kiếm cho DAL (giá trị luôn đi qua tham số @k, không ghép vào câu SQL).</summary>
public static class DalText
{
    /// <summary>Ký tự thoát dùng trong mệnh đề LIKE … ESCAPE '!'.</summary>
    public const char LikeEscape = '!';

    /// <summary>
    /// Mẫu LIKE "chứa chuỗi" cho tham số, thoát các ký tự đặc biệt % _ [ ! (dùng kèm ESCAPE '!' trong câu SQL).
    /// Chuỗi rỗng hoặc chỉ có khoảng trắng trả null để câu SQL bỏ qua điều kiện (@k IS NULL).
    /// </summary>
    public static string? LikeContains(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().Replace("!", "!!").Replace("%", "!%").Replace("_", "!_").Replace("[", "![");
        return "%" + t + "%";
    }
}
