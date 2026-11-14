using FPolyCinema.Web.DAL;
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
/// Quản lý đồ uống và định mức nguyên liệu (UC29, UI A2, A3; Lab 5 bài 3: lọc theo loại, tìm theo tên).
/// Ảnh món tải lên và xóa bằng fetch qua FileController. Nhân viên xem; chỉ quản lý rạp thêm, sửa, xóa (SPEC 3.14).
/// </summary>
[Authorize(Policy = Policies.EmployeeOrManager)]
[Route("admin/drinks")]
public class DrinksController : AppControllerBase
{
    private readonly DrinkService _service;
    private readonly CategoryService _categories;
    private readonly CinemaContextService _cinemas;
    private readonly IClock _clock;
    private readonly RealtimeNotifier _notifier;

    public DrinksController(DrinkService service, CategoryService categories, CinemaContextService cinemas, IClock clock, RealtimeNotifier notifier)
    {
        _service = service;
        _categories = categories;
        _cinemas = cinemas;
        _clock = clock;
        _notifier = notifier;
    }

    private bool CanManage => User.IsInRole(Roles.Manager);

    [HttpGet("")]
    public IActionResult Index(string? q, int? categoryId, string? status, int? page)
    {
        var p = Paging.ClampPage(page);
        var vm = new DrinkListVm
        {
            Query = q, CategoryId = categoryId, Status = status, CanManage = CanManage,
            Categories = _categories.Search(null, null, 1, Paging.MaxPageSize).Items.Select(r => r.Category).ToList(),
            Result = _service.Search(q, categoryId, ParseStatus(status), p, Paging.DefaultPageSize),
        };
        return IsPartialRequest ? PartialView("_Table", vm) : View(vm);
    }

    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        try
        {
            var d = _service.Get(id);
            var lines = _service.Recipes(id);
            var vm = new DrinkDetailsVm
            {
                Drink = d,
                CategoryName = _categories.Get(d.CategoryId).Name,
                CanManage = CanManage,
                RecipesByCinema = _cinemas.Cinemas.Select(c => (c.Name, lines.Where(l => l.CinemaId == c.Id).ToList())).ToList(),
            };
            return View(vm);
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("create")]
    public IActionResult Create(int? categoryId) => FormView(new DrinkFormVm { CategoryId = categoryId ?? 0 });

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("create")]
    public Task<IActionResult> Create(DrinkFormVm vm) => SaveForm(null, vm);

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        try
        {
            var d = _service.Get(id);
            var vm = new DrinkFormVm
            {
                Id = d.Id, CategoryId = d.CategoryId, Name = d.Name, Price = d.Price, Description = d.Description,
                Image = d.Image, IsCombo = d.IsCombo, Active = d.Active,
                Recipes = _service.Recipes(id).Select(l => new RecipeRowVm { CinemaId = l.CinemaId, RawMaterialId = l.RawMaterialId, Quantity = l.Quantity }).ToList(),
            };
            return FormView(vm);
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("{id:int}/edit")]
    public Task<IActionResult> Edit(int id, DrinkFormVm vm) => SaveForm(id, vm);

    private async Task<IActionResult> SaveForm(int? id, DrinkFormVm vm)
    {
        vm.Id = id;
        vm.Recipes = vm.Recipes.Where(r => r.RawMaterialId > 0 || r.Quantity != 0).ToList();
        if (!ModelState.IsValid) return FormView(vm);
        try
        {
            var input = new DrinkInput(vm.CategoryId, vm.Name, vm.Price, vm.Description, vm.Image, vm.IsCombo, vm.Active,
                vm.Recipes.Select(r => new RecipeInput(r.RawMaterialId, r.Quantity)).ToList());
            bool? wasSoldOut = id is int old ? DrinkDAL.GetById(old)?.IsSoldOut : null;
            var saved = _service.Save(id, input, _clock.Now);
            // Định mức đổi thì tính lại cờ "Tạm hết" của món và báo POS, web (PRODUCT_SOLD_OUT / PRODUCT_AVAILABLE, SPEC 3.11-09, 3.11-10)
            InventoryService.SyncSoldOutFlags(new[] { saved.Id });
            var soldOut = DrinkDAL.GetById(saved.Id)?.IsSoldOut ?? false;
            if (wasSoldOut != soldOut)
                foreach (var c in CinemaDAL.GetActive())
                    if (soldOut) await _notifier.ProductSoldOut(c.Id, new[] { saved.Id }); else if (wasSoldOut == true) await _notifier.ProductAvailable(c.Id, new[] { saved.Id });
            Toast("success", id == null ? $"Đã thêm món “{saved.Name}”" : "Đã lưu thay đổi");
            return Redirect("/admin/drinks");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return FormView(vm);
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    private IActionResult FormView(DrinkFormVm vm)
    {
        vm.Categories = _categories.Search(null, null, 1, Paging.MaxPageSize).Items.Select(r => r.Category).Where(c => c.Active || c.Id == vm.CategoryId).ToList();
        vm.CinemaMaterials = _cinemas.Cinemas.Select(c => new CinemaMaterialsVm(c.Id, c.Name, _service.MaterialsOf(c.Id))).ToList();
        return View("Form", vm);
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("{id:int}/delete-check")]
    public IActionResult DeleteCheck(int id)
    {
        var d = _service.CheckDelete(id);
        return ApiOk(new { allowed = d.Allowed, reason = d.Reason, canDeactivate = d.CanDeactivate, consequences = d.Consequences });
    }

    /// <summary>Xóa cứng khi món chưa có trong hóa đơn; bị chặn thì DELETE_BLOCKED kèm gợi ý ngưng bán.</summary>
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
