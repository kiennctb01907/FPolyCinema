using FPolyCinema.Web.Entities;

namespace FPolyCinema.Web.Services;

// Dữ liệu bán hàng do service trả về cho controller (không phụ thuộc lớp giao diện).

/// <summary>Ngữ cảnh bán hàng tại quầy: người dùng có ca OPEN tại máy này (SPEC 3.6).</summary>
public sealed record PosContext(int UserId, string Role, int CinemaId, PosShift Shift);

/// <summary>Một vé trong hóa đơn tạm tính.</summary>
public sealed record CartTicket(int SeatId, string Label, string SeatType, string SeatTypeLabel, string AudienceType, string AudienceLabel,
    long ListPrice, long FinalPrice, string? WarningCode, string? Warning);

/// <summary>Một dòng bắp nước trong hóa đơn tạm tính.</summary>
public sealed record CartItem(int DrinkId, string Name, string? Image, int Quantity, long Price, long Amount, int MaxQuantity);

/// <summary>Thành viên gắn với hóa đơn.</summary>
public sealed record CartMember(int Id, string FullName, string? MemberCode, string Tier, string TierLabel, int Points, long PointValue, long WalletBalance = 0);

/// <summary>Suất của hóa đơn.</summary>
public sealed record CartShowtime(int Id, string MovieTitle, string AgeRating, string AgeExplanation, DateTime StartTime, DateTime EndTime,
    string AuditoriumName, string Format, string LanguageLabel);

/// <summary>Hóa đơn tạm tính của POS (UI P1 cột 3), trả về sau mỗi thao tác.</summary>
public sealed class Cart
{
    public int? BillId { get; init; }
    public string? Code { get; init; }
    public CartShowtime? Showtime { get; init; }
    public DateTime? HoldExpiresAt { get; init; }
    public bool CanExtend { get; init; }
    public CartMember? Member { get; init; }
    public List<CartTicket> Tickets { get; init; } = new();
    public List<CartItem> Items { get; init; } = new();
    public string? VoucherCode { get; init; }
    public long VoucherDiscount { get; init; }
    public int PointsUsed { get; init; }
    public long PointsDiscount { get; init; }
    public int MaxPoints { get; init; }
    public long TicketSubtotal { get; init; }
    public long FnbSubtotal { get; init; }
    public long DiscountTotal { get; init; }
    public long VatTotal { get; init; }
    public long Total { get; init; }
    public bool GuardianRequired { get; init; }
    public bool GuardianConfirmed { get; init; }
    public List<string> Notices { get; init; } = new();
    public bool IsEmpty => Tickets.Count == 0 && Items.Count == 0;
}

/// <summary>Kết quả thanh toán tại quầy.</summary>
public sealed record CheckoutResult(int BillId, string Code, long Total, long CashReceived, long Change, string PrintUrl);

/// <summary>Một món trên lưới bắp nước của POS.</summary>
public sealed record PosDrink(Drink Drink, string CategoryName, int Available);

/// <summary>Một ghế của sơ đồ ghế theo suất (trạng thái hiệu lực, giá vé người lớn, điểm).</summary>
public sealed record SeatMapSeat(int SeatId, int Row, int Col, string Label, string SeatType, string Status, bool IsHouse, int? PairGroupId,
    int Score, string ScoreNote, long Price, int? HoldBillId);

/// <summary>Sơ đồ ghế của một suất.</summary>
public sealed record SeatMapModel(int ShowtimeId, int Rows, int Cols, int? HoldBillId, List<SeatMapSeat> Seats);
