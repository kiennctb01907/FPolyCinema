namespace FPolyCinema.Web.Entities.ReadModels;

// Bản ghi đọc (không ứng với một bảng) mà DAL trả về cho các màn hình danh sách của Phase 2.

/// <summary>Một dòng danh sách loại đồ uống kèm số món.</summary>
public sealed record CategoryRow(Category Category, int DrinkCount);

/// <summary>Một dòng danh sách đồ uống kèm tên loại và số dòng định mức.</summary>
public sealed record DrinkRow(Drink Drink, string CategoryName, int RecipeCount);

/// <summary>Một dòng định mức kèm thông tin nguyên liệu (rạp, tên, đơn vị).</summary>
public sealed record RecipeLine(int Id, int DrinkId, int RawMaterialId, decimal Quantity, int CinemaId, string MaterialName, string Unit);

/// <summary>Một yêu cầu phê duyệt đang chờ, kèm mã hóa đơn và tên người yêu cầu (chuông phê duyệt của topbar).</summary>
public sealed record PendingApprovalRow(int Id, string BillCode, string RequesterName, string Reason, DateTime CreatedAt);

// ---------------------------------------------------------------- Phase 3
/// <summary>Một dòng danh sách phim kèm tên nhà phát hành và số suất chiếu.</summary>
public sealed record MovieRow(Movie Movie, string? DistributorName, int ShowtimeCount);

/// <summary>Một dòng danh sách nhà phát hành kèm số phim.</summary>
public sealed record DistributorRow(Distributor Distributor, int MovieCount);

/// <summary>Một dòng danh sách phòng chiếu kèm tên rạp, số ghế và số suất chiếu.</summary>
public sealed record AuditoriumRow(Auditorium Auditorium, string CinemaName, int SeatCount, int ShowtimeCount);

/// <summary>Một KDM kèm tên rạp.</summary>
public sealed record KdmRow(KdmLicense Kdm, string CinemaName);

/// <summary>
/// Một suất trong lịch chiếu kèm phim, phòng, sức chứa và số ghế: BookedSeats (đã bán), ActiveHeldSeats (đang giữ còn hạn),
/// TicketCount (vé còn hiệu lực). Cờ LOCKED_HAS_TICKETS tính từ BookedSeats và ActiveHeldSeats.
/// </summary>
public sealed record ShowtimeRow(Showtime Showtime, int CinemaId, string MovieTitle, string AgeRating, string? ClassificationLicenseNo, bool IsVietnameseFilm,
    string AuditoriumName, string AuditoriumFormat, int Capacity, int BookedSeats, int ActiveHeldSeats);
