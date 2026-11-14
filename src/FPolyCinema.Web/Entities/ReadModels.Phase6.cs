namespace FPolyCinema.Web.Entities.ReadModels;

// Bản ghi đọc của Phase 6 (vận hành thời gian thực). Không ứng với bảng nên không tính là Entity.

/// <summary>Suất cho ShowtimeClockService: mốc giờ, phòng, rạp, trạng thái vận hành.</summary>
public sealed record ClockShowtimeRow(int Id, int AuditoriumId, int CinemaId, DateTime StartTime, DateTime FeatureStartTime, DateTime EndTime, string OperationalStatus);

/// <summary>Một vé của hóa đơn khi soát vé, đổi ghế hoặc xem vé điện tử.</summary>
public sealed record GateTicketRow(int TicketId, int SeatId, string SeatLabel, string SeatType, int? PairGroupId, string AudienceType, string CheckInStatus,
    DateTime? CheckedInAt, bool RequiresDocument, long ListPrice, long FinalPrice, string QrCode);

/// <summary>Sĩ số một suất: đã bán (vé còn hiệu lực), đã vào cửa.</summary>
public sealed record LiveOccupancyRow(int ShowtimeId, int AuditoriumId, string AuditoriumName, int CinemaId, string CinemaName, string MovieTitle, string AgeRating,
    DateTime StartTime, DateTime FeatureStartTime, DateTime EndTime, string OperationalStatus, int Sold, int CheckedIn, int Capacity);

/// <summary>Một dòng bảng sảnh: suất sắp chiếu hoặc đang chiếu.</summary>
public sealed record SignageRow(int ShowtimeId, DateTime StartTime, DateTime EndTime, string MovieTitle, string AgeRating, string Format, string LanguageFormat,
    string AuditoriumName, string OperationalStatus);

/// <summary>Một đơn trên màn hình bếp.</summary>
public sealed record KdsOrderRow(int BillId, string Code, string Channel, int? PickupNumber, string FnbStatus, DateTime QueuedAt);

/// <summary>Một món của đơn trên màn hình bếp.</summary>
public sealed record KdsLineRow(int BillId, string DrinkName, int Quantity);

/// <summary>Suất sắp chiếu còn ghế nội bộ (nhắc giải phóng).</summary>
public sealed record HouseSeatRow(int ShowtimeId, string MovieTitle, string AuditoriumName, DateTime StartTime, DateTime EndTime, string OperationalStatus, int HouseSeats, string Labels);

/// <summary>Nguyên liệu kèm tên rạp.</summary>
public sealed record MaterialRow(RawMaterial Material, string CinemaName);

/// <summary>Phiếu hủy hàng kèm tên rạp, người lập, người quyết định, số dòng.</summary>
public sealed record WastageRow(WastageLog Log, string CinemaName, string CreatorName, string? DeciderName, int ItemCount);

/// <summary>Một dòng phiếu hủy hàng.</summary>
public sealed record WastageItemRow(int Id, int RawMaterialId, string MaterialName, string Unit, decimal Quantity, string? Reason);

/// <summary>Một dòng báo cáo hủy hàng (phiếu đã duyệt).</summary>
public sealed record WastageReportRow(int LogId, DateTime DecidedAt, string CinemaName, string MaterialName, string Unit, decimal Quantity, string? Reason,
    string CreatorName, string? DeciderName);

/// <summary>Hợp đồng doanh nghiệp kèm suất và hóa đơn.</summary>
public sealed record CorporateRow(CorporateBooking Booking, string? MovieTitle, DateTime? StartTime, string? AuditoriumName, string? BillCode, string CreatorName, int VouchersUsed);

/// <summary>Yêu cầu phê duyệt kèm hóa đơn và người yêu cầu (popup A7, danh sách chờ duyệt).</summary>
public sealed record ApprovalDetailRow(int Id, int BillId, string BillCode, int CinemaId, long Total, string Reason, int RequestedBy, string RequesterName,
    string Status, string? Method, DateTime CreatedAt, DateTime ExpiresAt);
