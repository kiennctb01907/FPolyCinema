using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Một dòng quy tắc chia doanh thu gửi lên từ form phim: tuần thứ mấy và phần trăm trả nhà phát hành.</summary>
public sealed record RentalRuleInput(int WeekNumber, decimal DistributorSharePercent);

/// <summary>Một dòng KDM gửi lên từ form phim: rạp, hiệu lực từ, đến, ghi chú.</summary>
public sealed record KdmInput(int CinemaId, DateTime? ValidFrom, DateTime? ValidUntil, string? Note);

/// <summary>Dữ liệu form thêm, sửa phim (UC31).</summary>
public sealed record MovieInput(
    string? Title, string? OriginalTitle, IReadOnlyList<string> Genres, int DurationMinutes, string? AgeRating, string? ClassificationLicenseNo,
    IReadOnlyList<string> ContentWarnings, bool IsVietnameseFilm, IReadOnlyList<string> LanguageFormats, DateTime? ReleaseDate, int? DistributorId,
    long MinimumTicketPrice, bool IsBlockbuster, string? PosterUrl, string? TrailerUrl, string? Description, bool Active,
    IReadOnlyList<RentalRuleInput> RentalRules, IReadOnlyList<KdmInput> Kdms);

/// <summary>
/// Quản lý phim (UC31, SPEC 3.1, 3.14): hồ sơ phim với giấy phép phân loại, nhãn tuổi, cảnh báo nội dung, ngôn ngữ, poster
/// (tải bằng fetch qua FileController), quy tắc chia doanh thu theo tuần và KDM theo rạp. Phim C chỉ lưu hồ sơ; phim chưa có
/// số giấy phép vẫn lưu được nhưng chưa tạo suất được (ScheduleConstraintChecker chặn).
/// </summary>
public class MovieService
{
    private readonly UploadService _uploads;

    public MovieService(UploadService uploads) => _uploads = uploads;

    public PagedResult<MovieRow> Search(string? query, bool? active, string? ageRating, int page, int pageSize)
    {
        var rating = AgeRating.All.Contains(ageRating) ? ageRating : null;
        var items = MovieDAL.Search(query, active, rating, Paging.Offset(page, pageSize), pageSize, out int total);
        return new PagedResult<MovieRow>(items, page, pageSize, total);
    }

    public Movie Get(int id) => MovieDAL.GetById(id) ?? throw CategoryService.NotFound();

    public List<FilmRentalRule> RentalRules(int movieId) => FilmRentalRuleDAL.GetByMovie(movieId);

    public List<KdmRow> Kdms(int movieId) => KdmLicenseDAL.GetByMovie(movieId);

    public List<Distributor> DistributorOptions(int? currentId) => DistributorDAL.GetActiveOrId(currentId);

    /// <summary>Lưu phim cùng quy tắc chia doanh thu và KDM trong một transaction. Đổi poster thì xóa poster cũ không còn ai dùng.</summary>
    public Movie Save(int? id, MovieInput input, IReadOnlyCollection<int> cinemaIds, DateTime now)
    {
        var errors = new Dictionary<string, string>();
        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length == 0) errors["Title"] = "Bạn chưa nhập tên phim.";
        else if (title.Length > 200) errors["Title"] = "Tên phim tối đa 200 ký tự.";
        var original = string.IsNullOrWhiteSpace(input.OriginalTitle) ? null : input.OriginalTitle.Trim();
        if (original?.Length > 200) errors["OriginalTitle"] = "Tên gốc tối đa 200 ký tự.";

        var genres = input.Genres.Where(Genre.All.Contains).Distinct().ToList();
        if (genres.Count == 0) errors["Genres"] = "Bạn chọn ít nhất một thể loại.";
        if (input.DurationMinutes < 1 || input.DurationMinutes > 400) errors["DurationMinutes"] = "Thời lượng phải từ 1 đến 400 phút.";
        if (!AgeRating.All.Contains(input.AgeRating)) errors["AgeRating"] = "Bạn chọn nhãn độ tuổi theo giấy phép phân loại.";
        var license = string.IsNullOrWhiteSpace(input.ClassificationLicenseNo) ? null : input.ClassificationLicenseNo.Trim();
        if (license?.Length > 50) errors["ClassificationLicenseNo"] = "Số giấy phép tối đa 50 ký tự.";
        var warnings = input.ContentWarnings.Where(ContentWarning.All.Contains).Distinct().ToList();
        var languages = LanguageFormat.All.Where(input.LanguageFormats.Contains).ToList();
        if (languages.Count == 0) errors["LanguageFormats"] = "Bạn chọn ít nhất một bản ngôn ngữ (phụ đề, lồng tiếng hoặc nguyên bản).";
        if (input.ReleaseDate is null) errors["ReleaseDate"] = "Bạn chọn ngày khởi chiếu.";
        if (input.DistributorId is int did && DistributorDAL.GetById(did) == null) errors["DistributorId"] = "Nhà phát hành không còn tồn tại. Bạn chọn lại.";
        if (input.MinimumTicketPrice < 0) errors["MinimumTicketPrice"] = "Giá sàn không được âm.";
        else if (input.MinimumTicketPrice > 10_000_000) errors["MinimumTicketPrice"] = "Giá sàn quá lớn, bạn kiểm tra lại.";
        var poster = string.IsNullOrWhiteSpace(input.PosterUrl) ? null : input.PosterUrl.Trim();
        if (poster != null && !UploadService.IsUploadPath(poster)) errors["PosterUrl"] = "Poster không hợp lệ. Bạn tải poster lên lại.";
        var trailer = string.IsNullOrWhiteSpace(input.TrailerUrl) ? null : input.TrailerUrl.Trim();
        if (trailer != null && (trailer.Length > 300 || !(trailer.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || trailer.StartsWith("http://", StringComparison.OrdinalIgnoreCase))))
            errors["TrailerUrl"] = "Đường dẫn trailer phải bắt đầu bằng https:// và tối đa 300 ký tự.";
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description?.Length > 2000) errors["Description"] = "Mô tả tối đa 2000 ký tự.";

        var weeks = new HashSet<int>();
        for (int i = 0; i < input.RentalRules.Count; i++)
        {
            var r = input.RentalRules[i];
            if (r.WeekNumber < 1 || r.WeekNumber > 52) errors[$"RentalRules[{i}].WeekNumber"] = "Tuần phải từ 1 đến 52.";
            else if (!weeks.Add(r.WeekNumber)) errors[$"RentalRules[{i}].WeekNumber"] = $"Tuần {r.WeekNumber} bị nhập hai lần. Bạn gộp vào một dòng.";
            if (r.DistributorSharePercent < 0 || r.DistributorSharePercent > 100) errors[$"RentalRules[{i}].DistributorSharePercent"] = "Tỷ lệ phải từ 0 đến 100%.";
        }
        for (int i = 0; i < input.Kdms.Count; i++)
        {
            var k = input.Kdms[i];
            if (!cinemaIds.Contains(k.CinemaId)) errors[$"Kdms[{i}].CinemaId"] = "Bạn chọn rạp cho KDM này.";
            if (k.ValidFrom is null) errors[$"Kdms[{i}].ValidFrom"] = "Bạn nhập thời điểm bắt đầu hiệu lực.";
            if (k.ValidUntil is null) errors[$"Kdms[{i}].ValidUntil"] = "Bạn nhập thời điểm hết hiệu lực.";
            else if (k.ValidFrom is DateTime f && k.ValidUntil <= f) errors[$"Kdms[{i}].ValidUntil"] = "Hết hiệu lực phải sau thời điểm bắt đầu.";
            if ((k.Note ?? string.Empty).Length > 300) errors[$"Kdms[{i}].Note"] = "Ghi chú tối đa 300 ký tự.";
        }
        if (errors.Count > 0) throw FieldErrors.Of(errors);

        string? oldPoster = null;
        var saved = DBUtil.InTransaction((conn, tx) =>
        {
            Movie m;
            if (id is null) m = new Movie { CreatedAt = now };
            else
            {
                m = MovieDAL.GetById(id.Value, conn, tx) ?? throw CategoryService.NotFound();
                oldPoster = m.PosterUrl;
            }
            m.Title = title;
            m.OriginalTitle = original;
            m.Genres = CsvList.Join(genres);
            m.DurationMinutes = input.DurationMinutes;
            m.AgeRating = input.AgeRating!;
            m.ClassificationLicenseNo = license;
            m.ContentWarnings = warnings.Count == 0 ? null : CsvList.Join(warnings);
            m.IsVietnameseFilm = input.IsVietnameseFilm;
            m.LanguageFormats = CsvList.Join(languages);
            m.ReleaseDate = input.ReleaseDate!.Value.Date;
            m.DistributorId = input.DistributorId;
            m.MinimumTicketPrice = input.MinimumTicketPrice;
            m.IsBlockbuster = input.IsBlockbuster;
            m.PosterUrl = poster;
            m.TrailerUrl = trailer;
            m.Description = description;
            m.Active = input.Active;
            if (id is null) MovieDAL.Insert(m, conn, tx);
            else MovieDAL.Update(m, conn, tx);

            FilmRentalRuleDAL.DeleteByMovie(m.Id, conn, tx);
            foreach (var r in input.RentalRules.OrderBy(r => r.WeekNumber))
                FilmRentalRuleDAL.Insert(new FilmRentalRule { MovieId = m.Id, WeekNumber = r.WeekNumber, DistributorSharePercent = Math.Round(r.DistributorSharePercent, 2) }, conn, tx);
            KdmLicenseDAL.DeleteByMovie(m.Id, conn, tx);
            foreach (var k in input.Kdms)
                KdmLicenseDAL.Insert(new KdmLicense
                {
                    MovieId = m.Id, CinemaId = k.CinemaId, ValidFrom = TrimSeconds(k.ValidFrom!.Value), ValidUntil = TrimSeconds(k.ValidUntil!.Value),
                    Note = string.IsNullOrWhiteSpace(k.Note) ? null : k.Note.Trim(),
                }, conn, tx);
            return m;
        });
        if (oldPoster != null && oldPoster != saved.PosterUrl) _uploads.DeleteIfUnused(oldPoster);
        return saved;
    }

    private static DateTime TrimSeconds(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);

    public DeleteDecision CheckDelete(int id)
    {
        var m = Get(id);
        return DeleteRules.Movie(MovieDAL.CountShowtimes(id), MovieDAL.CountTickets(id), KdmLicenseDAL.GetByMovie(id).Count, FilmRentalRuleDAL.GetByMovie(id).Count, m.Active);
    }

    /// <summary>Xóa cứng chỉ khi phim chưa có suất chiếu hay vé (SPEC 3.14); xóa kèm KDM, quy tắc chia doanh thu và poster.</summary>
    public void Delete(int id)
    {
        var poster = DBUtil.InTransaction((conn, tx) =>
        {
            var m = MovieDAL.GetById(id, conn, tx) ?? throw CategoryService.NotFound();
            var d = DeleteRules.Movie(MovieDAL.CountShowtimes(id, conn, tx), MovieDAL.CountTickets(id, conn, tx), 0, 0, m.Active);
            if (!d.Allowed) throw CategoryService.Blocked(d);
            KdmLicenseDAL.DeleteByMovie(id, conn, tx);
            FilmRentalRuleDAL.DeleteByMovie(id, conn, tx);
            MovieDAL.Delete(id, conn, tx);
            return m.PosterUrl;
        });
        if (poster != null) _uploads.DeleteIfUnused(poster);
    }

    public void SetActive(int id, bool active)
    {
        if (MovieDAL.SetActive(id, active) == 0) throw CategoryService.NotFound();
    }
}

/// <summary>Quản lý nhà phát hành (UC31): danh sách, thêm, sửa, ngưng; chỉ xóa khi chưa có phim.</summary>
public class DistributorService
{
    public PagedResult<DistributorRow> Search(string? query, bool? active, int page, int pageSize)
    {
        var items = DistributorDAL.Search(query, active, Paging.Offset(page, pageSize), pageSize, out int total);
        return new PagedResult<DistributorRow>(items, page, pageSize, total);
    }

    public Distributor Get(int id) => DistributorDAL.GetById(id) ?? throw CategoryService.NotFound();

    public Distributor Save(int? id, string? name, string? email, string? phone, bool active)
    {
        var errors = new Dictionary<string, string>();
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0) errors["Name"] = "Bạn chưa nhập tên nhà phát hành.";
        else if (n.Length > 150) errors["Name"] = "Tên tối đa 150 ký tự.";
        else if (DistributorDAL.NameExists(n, id)) errors["Name"] = "Đã có nhà phát hành cùng tên. Bạn đặt tên khác.";
        var e = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (e != null && (e.Length > 256 || !e.Contains('@') || e.StartsWith('@') || e.EndsWith('@'))) errors["Email"] = "Email chưa đúng dạng, ví dụ lienhe@congty.vn.";
        var p = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (p != null && (p.Length > 20 || !p.All(ch => char.IsDigit(ch) || ch is ' ' or '+' or '.' or '-'))) errors["Phone"] = "Số điện thoại chỉ gồm chữ số, tối đa 20 ký tự.";
        if (errors.Count > 0) throw FieldErrors.Of(errors);

        var d = id is null ? new Distributor() : Get(id.Value);
        d.Name = n; d.Email = e; d.Phone = p; d.Active = active;
        if (id is null) DistributorDAL.Insert(d); else DistributorDAL.Update(d);
        return d;
    }

    public DeleteDecision CheckDelete(int id) => DeleteRules.Distributor(DistributorDAL.CountMovies(id), Get(id).Active);

    public void Delete(int id)
    {
        DBUtil.InTransaction((conn, tx) =>
        {
            var d = DistributorDAL.GetById(id, conn, tx) ?? throw CategoryService.NotFound();
            var decision = DeleteRules.Distributor(DistributorDAL.CountMovies(id, conn, tx), d.Active);
            if (!decision.Allowed) throw CategoryService.Blocked(decision);
            DistributorDAL.Delete(id, conn, tx);
            return 0;
        });
    }

    public void SetActive(int id, bool active)
    {
        if (DistributorDAL.SetActive(id, active) == 0) throw CategoryService.NotFound();
    }
}
