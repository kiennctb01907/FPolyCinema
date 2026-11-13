using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Services;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Màn hình POS (UI P1).</summary>
public sealed class PosIndexVm
{
    public PosContext Context { get; init; } = null!;
    public List<PosShowtimeRow> Showtimes { get; init; } = new();
    public List<Category> Categories { get; init; } = new();
    public List<PosDrink> Drinks { get; init; } = new();
    public List<SupervisorRow> Supervisors { get; init; } = new();
    public List<AudienceDiscount> Audiences { get; init; } = new();
    public Cart Cart { get; init; } = new();
    public DateTime Now { get; init; }
    public int LowSeatThreshold { get; init; }
}

/// <summary>Mở ca (UI P7).</summary>
public sealed class OpenShiftVm
{
    public List<string> Terminals { get; init; } = new();
    public string? Terminal { get; set; }
    public long? OpeningCashFloat { get; set; }
    public string CinemaName { get; init; } = string.Empty;
    public PosShift? OpenElsewhere { get; init; }
    public Dictionary<string, string> Errors { get; init; } = new();
}

/// <summary>Đóng ca mù (UI P7).</summary>
public sealed class CloseShiftVm
{
    public PosShift Shift { get; init; } = null!;
    public long[] Denominations { get; init; } = Array.Empty<long>();
    public int WaitingBills { get; init; }
}

/// <summary>Biên bản ca (UI P7 bước 3).</summary>
public sealed class ShiftReportVm
{
    public ShiftRow Row { get; init; } = null!;
    /// <summary>Biên bản của chính người đang xem (hiện nút mở ca mới).</summary>
    public bool IsOwn { get; init; }
}

/// <summary>Trang in vé khổ 80mm.</summary>
public sealed class PrintVm
{
    public Bill Bill { get; init; } = null!;
    public string CinemaName { get; init; } = string.Empty;
    public string? CinemaAddress { get; init; }
    public string? StaffName { get; init; }
    public string? CustomerName { get; init; }
    public List<TicketRow> Tickets { get; init; } = new();
    public List<BillLineRow> Lines { get; init; } = new();
    public List<PaymentRow> Payments { get; init; } = new();
    public long VatTotal { get; init; }
    public bool AutoPrint { get; init; }
    public string BackUrl { get; init; } = "/pos/index";
}

/// <summary>Hóa đơn của tôi (POS).</summary>
public sealed class MyBillsVm
{
    public DateTime Date { get; init; }
    public List<BillListRow> Bills { get; init; } = new();
    public List<SupervisorRow> Supervisors { get; init; } = new();
    public bool HasShift { get; init; }
    public int? ShiftId { get; init; }
    public long Total => Bills.Where(b => b.Status == Domain.Constants.BillStatus.Finish).Sum(b => b.Total);
}

/// <summary>Vùng sơ đồ ghế của POS: sơ đồ, thông tin suất, giờ hiện tại.</summary>
public sealed class PosSeatMapVm
{
    public SeatMapVm SeatMap { get; init; } = new();
    public ShowtimeInfo Info { get; init; } = null!;
    public DateTime Now { get; init; }
}

/// <summary>Hộp hủy hóa đơn đã thanh toán: danh sách người duyệt; AskCode khi phải nhập mã hóa đơn (POS).</summary>
public sealed class VoidModalVm
{
    public List<SupervisorRow> Supervisors { get; init; } = new();
    public bool AskCode { get; init; }
}
