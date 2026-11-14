using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Services;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Danh sách hóa đơn (UI A8).</summary>
public sealed class BillListVm
{
    public DateRange Range { get; init; } = null!;
    public string? Status { get; init; }
    public string? Channel { get; init; }
    public int? StaffId { get; init; }
    public string? Query { get; init; }
    public string CinemaName { get; init; } = string.Empty;
    public PagedResult<BillListRow> Result { get; init; } = null!;
    public List<User> Staff { get; init; } = new();
    public bool HasFilter { get; init; }
}

/// <summary>Chi tiết hóa đơn (UI A8).</summary>
public sealed class BillDetailsVm
{
    public BillDetailData Data { get; init; } = null!;
    public List<SupervisorRow> Supervisors { get; init; } = new();
    public int CurrentUserId { get; init; }
}

/// <summary>Một biểu đồ Chart.js: loại, nhãn trục, các series; màu lấy từ token ở phía trình duyệt (UI U3-04).</summary>
public sealed record ChartSeries(string Label, IReadOnlyList<long> Data, string Tone);

public sealed record ChartVm(string Id, string Type, string Title, IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series, bool Horizontal = false, bool Money = true);

/// <summary>Dashboard quản lý (UI A1).</summary>
public sealed class DashboardVm
{
    public DashboardData Data { get; init; } = null!;

    /// <summary>Khối vận hành (UI A1-05): yêu cầu phê duyệt đang chờ, sĩ số trực tiếp.</summary>
    public OpsDashboardVm Ops { get; init; } = null!;
    public ChartVm Revenue { get; init; } = null!;
    public ChartVm TopMovies { get; init; } = null!;
}

/// <summary>Báo cáo (UI A6): tab đang mở và dữ liệu của tab.</summary>
public sealed class ReportVm
{
    public string Tab { get; init; } = "revenue";
    public DateRange Range { get; init; } = null!;
    public int? CinemaId { get; init; }
    public string ScopeName { get; init; } = string.Empty;
    public RevenueReport? Revenue { get; init; }
    public List<ChartVm> Charts { get; init; } = new();
    public List<VietnameseShareRow> Shares { get; init; } = new();
    public VietnameseShareRow? ChainShare { get; init; }
    public decimal MinShare { get; init; }
    public List<OccupancyRow> Occupancy { get; init; } = new();
    public List<ShiftRow> Shifts { get; init; } = new();
    public List<WastageReportRow> Wastage { get; init; } = new();
    public string ExportUrl { get; init; } = string.Empty;
}

/// <summary>Đối soát nhà phát hành (UI A6).</summary>
public sealed class SettlementVm
{
    public List<Movie> Movies { get; init; } = new();
    public int? MovieId { get; init; }
    public int Week { get; init; } = 1;
    public int MaxWeek { get; init; } = 1;
    public int? CinemaId { get; init; }
    public List<Cinema> Cinemas { get; init; } = new();
    public SettlementReport? Report { get; init; }
    public string? Error { get; init; }
}

/// <summary>Khối tỷ lệ phim Việt có vạch mốc (UI A1, A6).</summary>
public sealed record VietnameseShareVm(string Title, List<VietnameseShareRow> Rows, VietnameseShareRow Chain, decimal MinShare);
