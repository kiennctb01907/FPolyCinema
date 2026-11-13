namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một trang kết quả của danh sách (UI A2).</summary>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int total)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        Total = total;
    }

    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Total { get; }
    public int PageCount => Paging.PageCount(Total, PageSize);
    public string Label => Paging.Label(Page, PageSize, Total);
}

/// <summary>Quy tắc thuần của phân trang: kẹp số trang, tính vị trí bắt đầu, nhãn "1–20 trên 135" (UI mục 5).</summary>
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int PageCount(int total, int pageSize) => total <= 0 ? 1 : (total + pageSize - 1) / pageSize;

    public static int ClampPageSize(int? pageSize) => pageSize is null or < 1 ? DefaultPageSize : Math.Min(pageSize.Value, MaxPageSize);

    public static int ClampPage(int? page) => page is null or < 1 ? 1 : page.Value;

    public static int Offset(int page, int pageSize) => (Math.Max(page, 1) - 1) * pageSize;

    /// <summary>Nhãn dạng "1–20 trên 135"; không có dòng nào thì "0 kết quả".</summary>
    public static string Label(int page, int pageSize, int total)
    {
        if (total <= 0) return "0 kết quả";
        int from = Offset(page, pageSize) + 1;
        if (from > total) from = total;
        int to = Math.Min(from + pageSize - 1, total);
        return $"{from:#,##0}–{to:#,##0} trên {total:#,##0}".Replace(',', '.');
    }

    /// <summary>Các số trang cần hiện quanh trang hiện tại (tối đa window số, luôn có trang đầu và cuối); 0 là dấu "…".</summary>
    public static IReadOnlyList<int> Window(int page, int pageCount, int window = 5)
    {
        if (pageCount <= window + 2) return Enumerable.Range(1, pageCount).ToList();
        var list = new List<int> { 1 };
        int start = Math.Max(2, page - window / 2), end = Math.Min(pageCount - 1, start + window - 1);
        start = Math.Max(2, end - window + 1);
        if (start > 2) list.Add(0);
        for (int i = start; i <= end; i++) list.Add(i);
        if (end < pageCount - 1) list.Add(0);
        list.Add(pageCount);
        return list;
    }
}
