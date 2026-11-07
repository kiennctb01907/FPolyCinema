using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Showtimes: suất chiếu.</summary>
public class Showtime
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public int AuditoriumId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime FeatureStartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime CleaningEndTime { get; set; }
    public string LanguageFormat { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = Domain.Constants.OperationalStatus.Scheduled;
    public bool IsAiGenerated { get; set; }
    public bool IsPrivate { get; set; }
    public string? CancelNote { get; set; }
    public DateTime CreatedAt { get; set; }
}
