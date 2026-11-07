namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Movies: phim.</summary>
public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public string Genres { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string AgeRating { get; set; } = string.Empty;
    public string? ClassificationLicenseNo { get; set; }
    public string? ContentWarnings { get; set; }
    public bool IsVietnameseFilm { get; set; }
    public string LanguageFormats { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public int? DistributorId { get; set; }
    public long MinimumTicketPrice { get; set; }
    public bool IsBlockbuster { get; set; }
    public string? PosterUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string? Description { get; set; }
    public decimal? AiDemandScore { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
