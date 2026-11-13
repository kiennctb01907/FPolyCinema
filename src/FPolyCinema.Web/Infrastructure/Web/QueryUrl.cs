using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace FPolyCinema.Web.Infrastructure.Web;

/// <summary>Tạo URL của trang danh sách giữ nguyên bộ lọc trên query string (UI A2), đổi một khóa (ví dụ page).</summary>
public static class QueryUrl
{
    public static string With(HttpRequest request, string key, string? value)
    {
        var q = QueryHelpers.ParseQuery(request.QueryString.Value)
            .Where(kv => kv.Key != "partial" && !string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kv.Value.ToString()))
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());
        if (!string.IsNullOrEmpty(value)) q[key] = value;
        return QueryHelpers.AddQueryString(request.Path.Value ?? "/", q);
    }

    /// <summary>URL hiện tại (bỏ partial=1) để vùng dữ liệu tải lại đúng bộ lọc.</summary>
    public static string Current(HttpRequest request) => With(request, "partial", null);
}
