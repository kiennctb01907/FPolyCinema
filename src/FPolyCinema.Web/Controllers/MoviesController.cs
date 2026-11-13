using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Quản lý phim (UC31, UI A2, A3; SPEC 3.1, 3.14, 8.3): giấy phép phân loại, nhãn tuổi, cảnh báo nội dung, ngôn ngữ, poster 2:3
/// tải bằng fetch qua FileController, quy tắc chia doanh thu theo tuần và KDM theo rạp. Nhân viên xem; chỉ quản lý rạp thêm, sửa, xóa.
/// </summary>
[Authorize(Policy = Policies.EmployeeOrManager)]
[Route("admin/movies")]
public class MoviesController : AppControllerBase
{
    private readonly MovieService _service;
    private readonly CinemaContextService _cinemas;
    private readonly IClock _clock;

    public MoviesController(MovieService service, CinemaContextService cinemas, IClock clock)
    {
        _service = service;
        _cinemas = cinemas;
        _clock = clock;
    }

    private bool CanManage => User.IsInRole(Roles.Manager);

    [HttpGet("")]
    public IActionResult Index(string? q, string? status, string? rating, int? page)
    {
        var vm = new MovieListVm
        {
            Query = q, Status = status, Rating = AgeRating.All.Contains(rating) ? rating : null, CanManage = CanManage,
            Result = _service.Search(q, ParseStatus(status), rating, Paging.ClampPage(page), Paging.DefaultPageSize),
        };
        return IsPartialRequest ? PartialView("_Table", vm) : View(vm);
    }

    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        try
        {
            var m = _service.Get(id);
            return View(new MovieDetailsVm
            {
                Movie = m, CanManage = CanManage, Now = _clock.Now,
                DistributorName = m.DistributorId is int d ? _service.DistributorOptions(d).FirstOrDefault(x => x.Id == d)?.Name : null,
                RentalRules = _service.RentalRules(id), Kdms = _service.Kdms(id),
            });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("create")]
    public IActionResult Create() => FormView(new MovieFormVm
    {
        ReleaseDate = _clock.Today,
        LanguageFormats = new() { LanguageFormat.Subtitled },
        RentalRules = new() { new() { WeekNumber = 1, DistributorSharePercent = 55 }, new() { WeekNumber = 2, DistributorSharePercent = 50 }, new() { WeekNumber = 3, DistributorSharePercent = 40 } },
    });

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("create")]
    public IActionResult Create(MovieFormVm vm) => SaveForm(null, vm);

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        try
        {
            var m = _service.Get(id);
            return FormView(new MovieFormVm
            {
                Id = m.Id, Title = m.Title, OriginalTitle = m.OriginalTitle, Genres = CsvList.Split(m.Genres).ToList(), DurationMinutes = m.DurationMinutes,
                AgeRating = m.AgeRating, ClassificationLicenseNo = m.ClassificationLicenseNo, ContentWarnings = CsvList.Split(m.ContentWarnings).ToList(),
                IsVietnameseFilm = m.IsVietnameseFilm, LanguageFormats = CsvList.Split(m.LanguageFormats).ToList(), ReleaseDate = m.ReleaseDate,
                DistributorId = m.DistributorId, MinimumTicketPrice = m.MinimumTicketPrice, IsBlockbuster = m.IsBlockbuster, PosterUrl = m.PosterUrl,
                TrailerUrl = m.TrailerUrl, Description = m.Description, Active = m.Active,
                RentalRules = _service.RentalRules(id).Select(r => new RentalRuleRowVm { WeekNumber = r.WeekNumber, DistributorSharePercent = r.DistributorSharePercent }).ToList(),
                Kdms = _service.Kdms(id).Select(k => new KdmRowVm { CinemaId = k.Kdm.CinemaId, ValidFrom = k.Kdm.ValidFrom, ValidUntil = k.Kdm.ValidUntil, Note = k.Kdm.Note }).ToList(),
            });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, MovieFormVm vm) => SaveForm(id, vm);

    private IActionResult SaveForm(int? id, MovieFormVm vm)
    {
        vm.Id = id;
        vm.RentalRules = vm.RentalRules.Where(r => r.WeekNumber != 0 || r.DistributorSharePercent != 0).ToList();
        vm.Kdms = vm.Kdms.Where(k => k.CinemaId != 0 || k.ValidFrom != null || k.ValidUntil != null).ToList();
        if (!ModelState.IsValid) return FormView(vm);
        try
        {
            var input = new MovieInput(vm.Title, vm.OriginalTitle, vm.Genres, vm.DurationMinutes, vm.AgeRating, vm.ClassificationLicenseNo, vm.ContentWarnings,
                vm.IsVietnameseFilm, vm.LanguageFormats, vm.ReleaseDate, vm.DistributorId, vm.MinimumTicketPrice, vm.IsBlockbuster, vm.PosterUrl, vm.TrailerUrl,
                vm.Description, vm.Active,
                vm.RentalRules.Select(r => new RentalRuleInput(r.WeekNumber, r.DistributorSharePercent)).ToList(),
                vm.Kdms.Select(k => new KdmInput(k.CinemaId, k.ValidFrom, k.ValidUntil, k.Note)).ToList());
            var saved = _service.Save(id, input, _cinemas.Cinemas.Select(c => c.Id).ToList(), _clock.Now);
            Toast("success", id == null ? $"Đã thêm phim “{saved.Title}”" : "Đã lưu thay đổi");
            if (saved.AgeRating == AgeRating.C) Toast("info", "Phim nhãn C chỉ lưu hồ sơ, không tạo suất chiếu được.");
            else if (string.IsNullOrWhiteSpace(saved.ClassificationLicenseNo)) Toast("warning", "Phim chưa có số giấy phép phân loại nên chưa xếp suất được.");
            return Redirect($"/admin/movies/{saved.Id}");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return FormView(vm);
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    private IActionResult FormView(MovieFormVm vm)
    {
        vm.Distributors = _service.DistributorOptions(vm.DistributorId);
        vm.Cinemas = _cinemas.Cinemas.ToList();
        return View("Form", vm);
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("{id:int}/delete-check")]
    public IActionResult DeleteCheck(int id)
    {
        var d = _service.CheckDelete(id);
        return ApiOk(new { allowed = d.Allowed, reason = d.Reason, canDeactivate = d.CanDeactivate, consequences = d.Consequences });
    }

    /// <summary>Xóa cứng khi phim chưa có suất chiếu hay vé (SPEC 3.14); bị chặn thì DELETE_BLOCKED kèm gợi ý ngưng chiếu.</summary>
    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        _service.Delete(id);
        return ApiOk(new { id });
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("{id:int}/set-active")]
    public IActionResult SetActive(int id, bool active)
    {
        _service.SetActive(id, active);
        return ApiOk(new { id, active });
    }
}

/// <summary>Quản lý nhà phát hành (UC31, UI A2, A3): chỉ quản lý rạp; chỉ xóa khi chưa có phim.</summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("admin/distributors")]
public class DistributorsController : AppControllerBase
{
    private readonly DistributorService _service;

    public DistributorsController(DistributorService service) => _service = service;

    [HttpGet("")]
    public IActionResult Index(string? q, string? status, int? page)
    {
        var vm = new DistributorListVm { Query = q, Status = status, Result = _service.Search(q, ParseStatus(status), Paging.ClampPage(page), Paging.DefaultPageSize) };
        return IsPartialRequest ? PartialView("_Table", vm) : View(vm);
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new DistributorFormVm());

    [HttpPost("create")]
    public IActionResult Create(DistributorFormVm vm) => SaveForm(null, vm);

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        try
        {
            var d = _service.Get(id);
            return View("Form", new DistributorFormVm { Id = d.Id, Name = d.Name, Email = d.Email, Phone = d.Phone, Active = d.Active });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, DistributorFormVm vm) => SaveForm(id, vm);

    private IActionResult SaveForm(int? id, DistributorFormVm vm)
    {
        vm.Id = id;
        if (!ModelState.IsValid) return View("Form", vm);
        try
        {
            var d = _service.Save(id, vm.Name, vm.Email, vm.Phone, vm.Active);
            Toast("success", id == null ? $"Đã thêm nhà phát hành “{d.Name}”" : "Đã lưu thay đổi");
            return Redirect("/admin/distributors");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return View("Form", vm);
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [HttpGet("{id:int}/delete-check")]
    public IActionResult DeleteCheck(int id)
    {
        var d = _service.CheckDelete(id);
        return ApiOk(new { allowed = d.Allowed, reason = d.Reason, canDeactivate = d.CanDeactivate, consequences = d.Consequences });
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        _service.Delete(id);
        return ApiOk(new { id });
    }

    [HttpPost("{id:int}/set-active")]
    public IActionResult SetActive(int id, bool active)
    {
        _service.SetActive(id, active);
        return ApiOk(new { id, active });
    }
}
