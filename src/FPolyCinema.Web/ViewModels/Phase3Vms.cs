using System.ComponentModel.DataAnnotations;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Services;

namespace FPolyCinema.Web.ViewModels;

// ================================================================ Phim (UC31, A2, A3)

/// <summary>Danh sách phim (A2): lọc theo trạng thái và nhãn tuổi, tìm theo tên.</summary>
public class MovieListVm
{
    public PagedResult<MovieRow> Result { get; set; } = new(Array.Empty<MovieRow>(), 1, Paging.DefaultPageSize, 0);
    public string? Query { get; set; }
    public string? Status { get; set; }
    public string? Rating { get; set; }
    public bool CanManage { get; set; }
    public bool HasFilter => !string.IsNullOrWhiteSpace(Query) || !string.IsNullOrEmpty(Status) || !string.IsNullOrEmpty(Rating);
}

/// <summary>Một dòng quy tắc chia doanh thu trên form phim.</summary>
public class RentalRuleRowVm
{
    public int WeekNumber { get; set; }
    public decimal DistributorSharePercent { get; set; }
}

/// <summary>Một dòng KDM trên form phim.</summary>
public class KdmRowVm
{
    public int CinemaId { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? Note { get; set; }
}

/// <summary>Form thêm, sửa phim (A3): giấy phép, nhãn tuổi, cảnh báo nội dung, ngôn ngữ, poster 2:3, chia doanh thu, KDM.</summary>
public class MovieFormVm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Bạn chưa nhập tên phim.")]
    [StringLength(200, ErrorMessage = "Tên phim tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Tên gốc tối đa 200 ký tự.")]
    public string? OriginalTitle { get; set; }

    public List<string> Genres { get; set; } = new();

    [Range(1, 400, ErrorMessage = "Thời lượng phải từ 1 đến 400 phút.")]
    public int DurationMinutes { get; set; } = 100;

    public string AgeRating { get; set; } = Domain.Constants.AgeRating.P;

    [StringLength(50, ErrorMessage = "Số giấy phép tối đa 50 ký tự.")]
    public string? ClassificationLicenseNo { get; set; }

    public List<string> ContentWarnings { get; set; } = new();
    public bool IsVietnameseFilm { get; set; }
    public List<string> LanguageFormats { get; set; } = new();
    public DateTime? ReleaseDate { get; set; }
    public int? DistributorId { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Giá sàn từ 0 đến 10.000.000 ₫.")]
    public long MinimumTicketPrice { get; set; }

    public bool IsBlockbuster { get; set; }
    public string? PosterUrl { get; set; }

    [StringLength(300, ErrorMessage = "Đường dẫn trailer tối đa 300 ký tự.")]
    public string? TrailerUrl { get; set; }

    [StringLength(2000, ErrorMessage = "Mô tả tối đa 2000 ký tự.")]
    public string? Description { get; set; }

    public bool Active { get; set; } = true;
    public List<RentalRuleRowVm> RentalRules { get; set; } = new();
    public List<KdmRowVm> Kdms { get; set; } = new();

    // Dữ liệu cho ô chọn
    public List<Distributor> Distributors { get; set; } = new();
    public List<Cinema> Cinemas { get; set; } = new();
}

/// <summary>Chi tiết phim: hồ sơ pháp lý, nhà phát hành, chia doanh thu, KDM theo rạp.</summary>
public class MovieDetailsVm
{
    public Movie Movie { get; set; } = new();
    public string? DistributorName { get; set; }
    public List<FilmRentalRule> RentalRules { get; set; } = new();
    public List<KdmRow> Kdms { get; set; } = new();
    public bool CanManage { get; set; }
    public DateTime Now { get; set; }
}

// ================================================================ Nhà phát hành

public class DistributorListVm
{
    public PagedResult<DistributorRow> Result { get; set; } = new(Array.Empty<DistributorRow>(), 1, Paging.DefaultPageSize, 0);
    public string? Query { get; set; }
    public string? Status { get; set; }
    public bool HasFilter => !string.IsNullOrWhiteSpace(Query) || !string.IsNullOrEmpty(Status);
}

public class DistributorFormVm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Bạn chưa nhập tên nhà phát hành.")]
    [StringLength(150, ErrorMessage = "Tên tối đa 150 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string? Phone { get; set; }

    public bool Active { get; set; } = true;
}

// ================================================================ Phòng chiếu (UC32, A4)

public class AuditoriumListVm
{
    public List<AuditoriumRow> Rows { get; set; } = new();
    public List<Cinema> Cinemas { get; set; } = new();
    public int? CinemaId { get; set; }
}

/// <summary>Form thông tin phòng: khi tạo có thêm số hàng, số cột.</summary>
public class AuditoriumFormVm
{
    public int? Id { get; set; }
    public int CinemaId { get; set; }

    [Required(ErrorMessage = "Bạn chưa nhập tên phòng, ví dụ Phòng 3.")]
    [StringLength(100, ErrorMessage = "Tên phòng tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    public string Format { get; set; } = Domain.Constants.AuditoriumFormat.F2D;

    [Range(1, AuditoriumLayout.MaxRows, ErrorMessage = "Số hàng phải từ 1 đến 26.")]
    public int TotalRows { get; set; } = 10;

    [Range(1, AuditoriumLayout.MaxCols, ErrorMessage = "Số cột phải từ 1 đến 40.")]
    public int TotalCols { get; set; } = 14;

    [Range(0.05, 1.0, ErrorMessage = "Tỷ lệ hàng lý tưởng từ 0,05 đến 1.")]
    public decimal IdealRowRatio { get; set; } = SeatScoring.DefaultIdealRowRatio;

    public bool Active { get; set; } = true;
    public string? CinemaName { get; set; }
    public List<Cinema> Cinemas { get; set; } = new();
}

/// <summary>Một ô của công cụ thiết kế gửi cho JavaScript.</summary>
public sealed record DesignCellVm(int Row, int Col, string Type, bool House, bool Maint, string Label, int Score, int Heat);

/// <summary>Công cụ thiết kế phòng (A4): công cụ tô, lưới, thống kê, heatmap, khóa cấu trúc.</summary>
public class AuditoriumDesignVm
{
    public Auditorium Auditorium { get; set; } = new();
    public string CinemaName { get; set; } = string.Empty;
    public List<DesignCellVm> Cells { get; set; } = new();
    public bool StructureLocked { get; set; }
    public int HouseSeatsPerAuditorium { get; set; }
    public LayoutStats Stats { get; set; } = new(0, new Dictionary<string, int>(), 0, 0, 0);
}

// ================================================================ Bảng giá (UC33)

public class PricingVm
{
    public List<Cinema> Cinemas { get; set; } = new();
    public int CinemaId { get; set; }
    public List<PriceCell> Cells { get; set; } = new();
}

public class AudienceDiscountsVm
{
    public List<AudienceDiscount> Discounts { get; set; } = new();
    /// <summary>Giá trị đã gửi lên khi lưu lỗi, để giữ nguyên trên form.</summary>
    public Dictionary<string, decimal> Posted { get; set; } = new();
}

public class HolidaysVm
{
    public int Year { get; set; }
    public List<int> Years { get; set; } = new();
    public List<Holiday> Holidays { get; set; } = new();
    public DateTime? NewDate { get; set; }
    public string? NewName { get; set; }
}

public class PriceTryVm
{
    public List<Cinema> Cinemas { get; set; } = new();
    public int CinemaId { get; set; }
    public DateTime StartTime { get; set; }
    public string Format { get; set; } = Domain.Constants.AuditoriumFormat.F2D;
    public string SeatType { get; set; } = Domain.Constants.SeatType.Standard;
    public bool IsBlockbuster { get; set; }
    public string AudienceType { get; set; } = Domain.Constants.AudienceType.Adult;
    public List<AudienceDiscount> Discounts { get; set; } = new();
    public PriceBreakdown? Result { get; set; }
}

// ================================================================ Lịch chiếu (UC34, A5)

public class SchedulerVm
{
    public List<Cinema> Cinemas { get; set; } = new();
    public int CinemaId { get; set; }
    public DateTime Date { get; set; }
    public DateTime Today { get; set; }
    public ScheduleDay Day { get; set; } = new(0, DateTime.MinValue, new(), 0, 0, 0, 0, 0, 0);
}

/// <summary>Form suất chiếu: dữ liệu ô chọn và giá trị đang sửa.</summary>
public class ShowtimeFormVm
{
    public int? Id { get; set; }
    public int CinemaId { get; set; }
    public string CinemaName { get; set; } = string.Empty;
    public int AuditoriumId { get; set; }
    public int MovieId { get; set; }
    public DateTime? StartTime { get; set; }
    public string? LanguageFormat { get; set; }
    public bool IsLocked { get; set; }
    public int BookedSeats { get; set; }
    public List<Auditorium> Auditoriums { get; set; } = new();
    public List<Movie> Movies { get; set; } = new();
    public DateTime BackDate { get; set; }
}
