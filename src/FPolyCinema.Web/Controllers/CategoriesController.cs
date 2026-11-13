using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Quản lý loại đồ uống (UC28, UI A2, A3). Nhân viên xem danh sách và chi tiết (EmployeeOrManager);
/// chỉ quản lý rạp thêm, sửa, xóa, ngưng (ManagerOnly). Xóa theo SPEC 3.14: chỉ khi chưa có đồ uống nào.
/// </summary>
[Authorize(Policy = Policies.EmployeeOrManager)]
[Route("admin/categories")]
public class CategoriesController : AppControllerBase
{
    private readonly CategoryService _service;
    private readonly DrinkService _drinks;

    public CategoriesController(CategoryService service, DrinkService drinks)
    {
        _service = service;
        _drinks = drinks;
    }

    private bool CanManage => User.IsInRole(Roles.Manager);

    [HttpGet("")]
    public IActionResult Index(string? q, string? status, int? page)
    {
        var p = Paging.ClampPage(page);
        var vm = new CategoryListVm { Query = q, Status = status, CanManage = CanManage, Result = _service.Search(q, ParseStatus(status), p, Paging.DefaultPageSize) };
        return IsPartialRequest ? PartialView("_Table", vm) : View(vm);
    }

    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        try
        {
            var category = _service.Get(id);
            var drinks = _drinks.Search(null, id, null, 1, Paging.MaxPageSize);
            return View(new CategoryDetailsVm { Category = category, Drinks = drinks.Items.ToList(), DrinkTotal = drinks.Total, CanManage = CanManage });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("create")]
    public IActionResult Create() => View("Form", new CategoryFormVm());

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("create")]
    public IActionResult Create(CategoryFormVm vm) => SaveForm(null, vm);

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        try
        {
            var c = _service.Get(id);
            return View("Form", new CategoryFormVm { Id = c.Id, Name = c.Name, Active = c.Active });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, CategoryFormVm vm) => SaveForm(id, vm);

    private IActionResult SaveForm(int? id, CategoryFormVm vm)
    {
        vm.Id = id;
        if (!ModelState.IsValid) return View("Form", vm);
        try
        {
            var saved = _service.Save(id, vm.Name, vm.Active);
            Toast("success", id == null ? $"Đã thêm loại “{saved.Name}”" : "Đã lưu thay đổi");
            return Redirect("/admin/categories");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return View("Form", vm);
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    /// <summary>Hỏi trước khi xóa: được xóa không, lý do, hậu quả (cho hộp xác nhận của UI A2).</summary>
    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpGet("{id:int}/delete-check")]
    public IActionResult DeleteCheck(int id)
    {
        var d = _service.CheckDelete(id);
        return ApiOk(new { allowed = d.Allowed, reason = d.Reason, canDeactivate = d.CanDeactivate, consequences = d.Consequences });
    }

    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        _service.Delete(id);
        return ApiOk(new { id });
    }

    /// <summary>Công tắc Active: ngưng hoặc bật lại loại đồ uống.</summary>
    [Authorize(Policy = Policies.ManagerOnly)]
    [HttpPost("{id:int}/set-active")]
    public IActionResult SetActive(int id, bool active)
    {
        _service.SetActive(id, active);
        return ApiOk(new { id, active });
    }
}
