using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Services;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Máy soát vé (UI P3).</summary>
public sealed class GateVm
{
    public int CinemaId { get; init; }
    public string CinemaName { get; init; } = string.Empty;
    public List<Auditorium> Auditoriums { get; init; } = new();
    public int? AuditoriumId { get; init; }
    public List<LiveOccupancyRow> Cleaning { get; init; } = new();
    public int ResetSeconds { get; init; } = 6;
}

/// <summary>Màn hình bếp (UI P4).</summary>
public sealed class KdsVm
{
    public int CinemaId { get; init; }
    public string CinemaName { get; init; } = string.Empty;
    public List<KdsOrder> Orders { get; init; } = new();
    public DateTime Now { get; init; }
}

/// <summary>Bảng gọi số (UI P5) và bảng sảnh (UI P6).</summary>
public sealed class LobbyVm
{
    public int CinemaId { get; init; }
    public string CinemaName { get; init; } = string.Empty;
    public List<KdsOrder> Orders { get; init; } = new();
    public List<SignageRow> Showtimes { get; init; } = new();
    public DateTime Now { get; init; }
    public string Ticker { get; init; } = string.Empty;
}

/// <summary>Màn hình phụ của quầy (UI P2).</summary>
public sealed class CustomerDisplayVm
{
    public int CinemaId { get; init; }
    public string CinemaName { get; init; } = string.Empty;
    public string Terminal { get; init; } = string.Empty;
    public string TerminalKey { get; init; } = string.Empty;
    public List<(string Title, string? PosterUrl, string AgeRating)> Posters { get; init; } = new();
    public object? State { get; init; }
}

/// <summary>Đổi hoặc nâng hạng ghế tại quầy.</summary>
public sealed class SwapSeatVm
{
    public PosContext Context { get; init; } = null!;
}

/// <summary>Dashboard vận hành của quản lý ca (/ops): phê duyệt, sĩ số trực tiếp, suất trễ, phòng đang dọn, ghế nội bộ, phiếu hủy hàng chờ duyệt.</summary>
public sealed class OpsDashboardVm
{
    public string ScopeName { get; init; } = string.Empty;
    public int? CinemaId { get; init; }
    public List<ApprovalDetailRow> Approvals { get; init; } = new();
    public OpsBoard Board { get; init; } = null!;
    public List<HouseSeatRow> HouseSeats { get; init; } = new();
    public List<HouseSeatRow> HouseReminders { get; init; } = new();
    public List<WastageRow> PendingWastage { get; init; } = new();
    public int HouseReminderMinutes { get; init; }

    /// <summary>Rạp nhận cảnh báo (JoinManagerAlerts, JoinOps) và phòng nhận sĩ số (JoinAuditorium) trên trang.</summary>
    public List<int> CinemaIds { get; init; } = new();
    public List<int> AuditoriumIds { get; init; } = new();
}

/// <summary>Hủy suất do sự cố (UC27).</summary>
public sealed class IncidentVm
{
    public List<LiveOccupancyRow> Showtimes { get; init; } = new();
    public int? ShowtimeId { get; init; }
    public string? Reason { get; init; }
    public Domain.Rules.IncidentSummary? Summary { get; init; }
    public LiveOccupancyRow? Cancelled { get; init; }
    public Dictionary<string, string> Errors { get; init; } = new();
}

/// <summary>Danh sách phiếu hủy hàng (quản lý duyệt) hoặc phiếu của quầy.</summary>
public sealed class WastageListVm
{
    public List<WastageRow> Logs { get; init; } = new();
    public string? Status { get; init; }
    public bool CanDecide { get; init; }
}

/// <summary>Chi tiết phiếu hủy hàng.</summary>
public sealed class WastageDetailVm
{
    public WastageRow Row { get; init; } = null!;
    public List<WastageItemRow> Items { get; init; } = new();
    public bool CanDecide { get; init; }
}

/// <summary>Lập phiếu hủy hàng hỏng tại quầy (PosStaff) hoặc phiếu nhập kho (Manager).</summary>
public sealed class StockFormVm
{
    public int CinemaId { get; init; }
    public string CinemaName { get; init; } = string.Empty;
    public List<RawMaterial> Materials { get; init; } = new();
    public List<WastageRow> Recent { get; init; } = new();
    public bool IsImport { get; init; }
    public string? Note { get; init; }
}

/// <summary>Danh sách nguyên liệu (UI A2).</summary>
public sealed class MaterialListVm
{
    public List<MaterialRow> Rows { get; init; } = new();
    public string? Q { get; init; }
    public string? Status { get; init; }
    public bool LowOnly { get; init; }
    public string ScopeName { get; init; } = string.Empty;
}

/// <summary>Form nguyên liệu (UI A3).</summary>
public sealed class MaterialFormVm
{
    public int? Id { get; init; }
    public int CinemaId { get; set; }
    public string? Name { get; set; }
    public string? Unit { get; set; }
    public decimal MinThreshold { get; set; }
    public bool Active { get; set; } = true;
    public decimal ActualStock { get; init; }
    public decimal ReservedStock { get; init; }
    public List<Cinema> Cinemas { get; init; } = new();
    public bool CanChooseCinema { get; init; }
    public List<InventoryTransaction> History { get; init; } = new();
}

/// <summary>Danh sách hợp đồng doanh nghiệp.</summary>
public sealed class CorporateListVm
{
    public List<CorporateRow> Rows { get; init; } = new();
}

/// <summary>Form hợp đồng doanh nghiệp: thuê trọn suất hoặc phát hành loạt voucher.</summary>
public sealed class CorporateFormVm
{
    public CorporateInput Input { get; init; } = new();
    public List<LiveOccupancyRow> Showtimes { get; init; } = new();
    public DateTime Now { get; init; }
}

/// <summary>Chi tiết hợp đồng doanh nghiệp.</summary>
public sealed class CorporateDetailVm
{
    public CorporateRow Row { get; init; } = null!;
    public List<Voucher> Vouchers { get; init; } = new();
}
