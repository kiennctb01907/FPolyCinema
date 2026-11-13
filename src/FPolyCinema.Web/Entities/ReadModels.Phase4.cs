namespace FPolyCinema.Web.Entities.ReadModels;

/// <summary>Trạng thái một ghế của suất kèm thông tin ghế (đọc sơ đồ, giữ ghế).</summary>
public sealed record SeatStateRow(int SeatId, int RowIndex, int ColIndex, string Label, string SeatType, int? PairGroupId, bool IsHouseSeat,
    bool IsMaintenance, int BaseViewScore, string Status, int? HoldBillId, DateTime? HoldExpiresAt, string? HoldAudienceType, int? TicketId);

/// <summary>Một suất trong danh sách bán của POS.</summary>
public sealed record PosShowtimeRow(int ShowtimeId, int MovieId, string MovieTitle, string AgeRating, string? PosterUrl, int DurationMinutes,
    string AuditoriumName, string AuditoriumFormat, string LanguageFormat, DateTime StartTime, DateTime FeatureStartTime, DateTime EndTime,
    string OperationalStatus, int TotalSeats, int FreeSeats);

/// <summary>Dòng định mức của một món tại rạp kèm khả dụng của nguyên liệu (ActualStock − ReservedStock).</summary>
public sealed record DrinkStockLine(int DrinkId, int RawMaterialId, string MaterialName, decimal PerPortion, decimal Available);

/// <summary>Dòng danh sách hóa đơn (UI A8, hóa đơn của tôi).</summary>
public sealed record BillListRow(int Id, string Code, DateTime CreatedAt, DateTime? PaidAt, string Channel, int CinemaId, string CinemaName,
    string? StaffName, string? CustomerName, int TicketCount, int ItemCount, long Total, string Status, string? CancelReason, string? MovieTitle, DateTime? StartTime);

/// <summary>Vé của hóa đơn kèm suất, phòng, ghế (chi tiết, in vé).</summary>
public sealed record TicketRow(int Id, int SeatId, string SeatLabel, string SeatType, string AudienceType, long ListPrice, long FinalPrice, decimal VatRate,
    string QrCode, string CheckInStatus, string MovieTitle, string AgeRating, DateTime StartTime, DateTime EndTime, string AuditoriumName, string LanguageFormat, string AuditoriumFormat);

/// <summary>Dòng bắp nước của hóa đơn kèm tên món.</summary>
public sealed record BillLineRow(int Id, int DrinkId, string DrinkName, string? Image, bool IsCombo, int Quantity, long Price, decimal VatRate, long LineFinalAmount);

/// <summary>Một lần thanh toán hoặc hoàn tiền.</summary>
public sealed record PaymentRow(int Id, string Method, string Direction, long Amount, string Status, DateTime CreatedAt, int? PosShiftId);

/// <summary>Một lần phê duyệt của hóa đơn.</summary>
public sealed record ApprovalRow(int Id, string Status, string? Method, string Reason, string RequesterName, string? DeciderName, DateTime CreatedAt, DateTime? DecidedAt);

/// <summary>Ca POS kèm tên nhân viên và rạp (biên bản ca).</summary>
public sealed record ShiftRow(PosShift Shift, string UserName, string CinemaName, long CashIn, long CashRefund, int BillCount);

/// <summary>Người duyệt (ShiftManager, Manager) của một rạp.</summary>
public sealed record SupervisorRow(int Id, string FullName, string Role, int? CinemaId);

/// <summary>Số liệu thẻ KPI của dashboard.</summary>
public sealed record DashboardKpi(long Revenue, long FnbRevenue, int Tickets, int Bills, int SoldSeats, int TotalSeats);

/// <summary>Tỷ lệ phim Việt của một rạp (CinemaId null là toàn chuỗi).</summary>
public sealed record VietnameseShareRow(int? CinemaId, string CinemaName, int Vietnamese, int Total, int PrimeVietnamese, int PrimeTotal);

/// <summary>Tỷ lệ lấp đầy một suất.</summary>
public sealed record OccupancyRow(int ShowtimeId, string CinemaName, string AuditoriumName, string MovieTitle, DateTime StartTime, int Sold, int Capacity);

/// <summary>Cảnh báo vận hành trên dashboard.</summary>
public sealed record OpsAlertRow(string Kind, string Title, string Detail, string? Url);
