namespace FPolyCinema.Web.Entities.ReadModels;

// Bản ghi đọc của web khách (Phase 5): không ứng với bảng nên không tính là Entity.

/// <summary>Một suất cho web khách kèm rạp và số ghế trống hiệu lực.</summary>
public sealed record CustomerShowtimeRow(int ShowtimeId, int MovieId, string MovieTitle, string AgeRating, int CinemaId, string CinemaName, string AuditoriumName,
    string AuditoriumFormat, string LanguageFormat, DateTime StartTime, DateTime FeatureStartTime, DateTime EndTime, string OperationalStatus, int FreeSeats);

/// <summary>Một hóa đơn trong "Vé của tôi": suất, phim, trạng thái thanh toán, vào cửa và bắp nước.</summary>
public sealed record CustomerBillRow(int Id, string Code, string Channel, string Status, string? CancelReason, long Total, DateTime CreatedAt, string FnbStatus,
    string CheckInStatus, int? ShowtimeId, DateTime? StartTime, DateTime? EndTime, string? MovieTitle, string? AgeRating, string? PosterUrl, string? AuditoriumName,
    string CinemaName, int TicketCount);
