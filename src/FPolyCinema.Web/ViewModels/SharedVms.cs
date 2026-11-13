using Microsoft.AspNetCore.Html;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Trang lỗi và trang từ chối truy cập (UI D3).</summary>
public class ErrorVm
{
    public int StatusCode { get; set; } = 500;
    public string Title { get; set; } = "Đã có lỗi xảy ra";
    public string Message { get; set; } = "Máy chủ đang gặp sự cố nên chưa làm được việc này. Bạn thử lại sau ít phút.";
    public string Icon { get; set; } = "bi-exclamation-octagon";
    public string HomeUrl { get; set; } = "/";
    public string HomeLabel { get; set; } = "Về trang chính";
    /// <summary>Mã nhỏ ở cuối trang để liên hệ hỗ trợ (UI mục 9), không phải mã lỗi kỹ thuật.</summary>
    public string? SupportCode { get; set; }
}

/// <summary>Một mục breadcrumb; Url null là mục hiện tại.</summary>
public sealed record Crumb(string Label, string? Url = null);

/// <summary>Đầu trang (_PageHeader): breadcrumb, tiêu đề 24px, mô tả 14px, các nút bên phải.</summary>
public class PageHeaderVm
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Crumb> Crumbs { get; set; } = new();
    public Func<object?, IHtmlContent>? Actions { get; set; }
}

/// <summary>Trạng thái rỗng hoặc lỗi (_EmptyState): icon 48px trong vòng tròn, tiêu đề, một câu, một nút.</summary>
public class EmptyStateVm
{
    public string Icon { get; set; } = "bi-inbox";
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? ActionLabel { get; set; }
    public string? ActionUrl { get; set; }
    public string ActionIcon { get; set; } = "bi-plus-lg";
    /// <summary>"empty" hoặc "error" (lỗi có nút Thử lại).</summary>
    public string Tone { get; set; } = "empty";
    /// <summary>Nút gọi lại vùng dữ liệu (data-region-retry) thay vì chuyển trang.</summary>
    public bool IsRetry { get; set; }
}

/// <summary>Phân trang (_Pagination) dạng "1–20 trên 135".</summary>
public class PaginationVm
{
    public int Page { get; set; }
    public int PageCount { get; set; }
    public string Label { get; set; } = string.Empty;
    public Func<int, string> UrlFor { get; set; } = p => "?page=" + p;
}

/// <summary>Một ô chọn của thanh lọc.</summary>
public sealed record FilterSelect(string Name, string Label, IReadOnlyList<(string Value, string Text)> Options, string? Selected, string AllText = "Tất cả");

/// <summary>Thanh lọc (_FilterBar): ô tìm có icon và nút xóa, các ô chọn, khoảng ngày có mốc nhanh, nút Xóa bộ lọc.</summary>
public class FilterBarVm
{
    public string SearchName { get; set; } = "q";
    public string? SearchValue { get; set; }
    public string SearchLabel { get; set; } = "Tìm theo tên";
    public string SearchPlaceholder { get; set; } = "Nhập tên cần tìm";
    public List<FilterSelect> Selects { get; set; } = new();
    public bool ShowDateRange { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public string ClearUrl { get; set; } = "?";
    public bool HasFilter { get; set; }
}

/// <summary>Một dòng của khối tổng tiền.</summary>
public sealed record MoneyLine(string Label, long Amount, bool IsDiscount = false);

/// <summary>Khối tổng tiền (_MoneySummary): từng dòng, tạm tính, giảm giá, tổng 24px (POS 32px).</summary>
public class MoneySummaryVm
{
    public List<MoneyLine> Lines { get; set; } = new();
    public long Subtotal { get; set; }
    public long Discount { get; set; }
    public long Total { get; set; }
    public string TotalLabel { get; set; } = "Tổng cộng";
}

/// <summary>Stepper 4 bước (_Stepper).</summary>
public class StepperVm
{
    public List<string> Steps { get; set; } = new();
    /// <summary>Bước hiện tại, đếm từ 1.</summary>
    public int Current { get; set; } = 1;
}

/// <summary>Một ô ghế của sơ đồ (_SeatMap).</summary>
public sealed record SeatCell(int Row, int Col, string Label, string SeatType, string Status, bool IsHouse = false)
{
    public int SeatId { get; init; }
    public int? PairGroupId { get; init; }
    /// <summary>Điểm góc nhìn và âm thanh BaseViewScore (0–100); 0 là không hiện.</summary>
    public int Score { get; init; }
    /// <summary>Ghi chú điểm, ví dụ "tọa độ vàng".</summary>
    public string ScoreNote { get; init; } = string.Empty;
    /// <summary>Giá vé người lớn của ghế; 0 là không hiện.</summary>
    public long Price { get; init; }
    public int? HoldBillId { get; init; }
}

/// <summary>Sơ đồ ghế (_SeatMap, UI mục 8): lưới TotalRows × TotalCols, hàng 1 (A) gần màn hình nhất.</summary>
public class SeatMapVm
{
    public int Rows { get; set; }
    public int Cols { get; set; }
    public List<SeatCell> Seats { get; set; } = new();
    public bool ShowLegend { get; set; } = true;

    /// <summary>Trạng thái hiển thị của ghế thuộc hóa đơn của người đang xem (CONTRACTS C3-08).</summary>
    public const string Selected = FPolyCinema.Web.Services.SeatMapService.Selected;

    /// <summary>Sơ đồ ghế của một suất (có chọn ghế, bàn phím, tooltip giá và điểm, cập nhật thời gian thực).</summary>
    public static SeatMapVm From(FPolyCinema.Web.Services.SeatMapModel m, bool showLegend = true) => new()
    {
        Rows = m.Rows, Cols = m.Cols, ShowtimeId = m.ShowtimeId, HoldBillId = m.HoldBillId, Interactive = true, ShowLegend = showLegend,
        Seats = m.Seats.Select(s => new SeatCell(s.Row, s.Col, s.Label, s.SeatType, s.Status, s.IsHouse)
        {
            SeatId = s.SeatId, PairGroupId = s.PairGroupId, Score = s.Score, ScoreNote = s.ScoreNote, Price = s.Price, HoldBillId = s.HoldBillId,
        }).ToList(),
    };

    /// <summary>Suất của sơ đồ (0 khi là sơ đồ mẫu); sơ đồ có suất thì seatmap.js gắn chọn ghế, bàn phím và cập nhật thời gian thực.</summary>
    public int ShowtimeId { get; set; }
    public bool Interactive { get; set; }
    public int? HoldBillId { get; set; }
}

/// <summary>Nhãn trạng thái (_StatusBadge): nhóm trong StatusDisplay và giá trị gốc.</summary>
public sealed record StatusBadgeVm(string Group, string? Value);

/// <summary>Nhãn tuổi (_AgeBadge): kèm câu giải thích nếu ShowExplanation.</summary>
public sealed record AgeBadgeVm(string Rating, bool ShowExplanation = false, bool Large = false);

/// <summary>Khối QR (_QrBlock): ảnh QR dạng data URI, mã ghi bên dưới, đồng hồ đếm ngược nếu có hạn.</summary>
public sealed record QrBlockVm(string DataUri, string Code, DateTime? ExpiresAt = null, bool Large = false);

/// <summary>Thẻ phim (_MovieCard).</summary>
public sealed record MovieCardVm(int Id, string Title, string? PosterUrl, string AgeRating, string Subline, string? BookUrl, string ActionLabel = "Đặt vé");

/// <summary>Thẻ món bắp nước (_FoodCard).</summary>
public sealed record FoodCardVm(int Id, string Name, string? Image, long Price, bool SoldOut, int Quantity = 0);

/// <summary>Thẻ KPI (_KpiCard).</summary>
public sealed record KpiCardVm(string Icon, string Label, string Value, decimal? ChangePercent, string Tone = "info");
