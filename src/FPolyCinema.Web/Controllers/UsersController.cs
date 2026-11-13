using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Quản lý nhân viên (UC36, UI A2, A3), chỉ quản lý rạp (ManagerOnly). Khóa hoặc mở bằng UserDAL.SetActive (ghi AuditLogs);
/// xóa cứng chỉ khi nhân viên chưa lập hóa đơn nào (SPEC 3.14); không ai tự khóa hay xóa tài khoản của mình.
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("admin/users")]
public class UsersController : AppControllerBase
{
    private readonly StaffService _service;
    private readonly CinemaContextService _cinemas;

    public UsersController(StaffService service, CinemaContextService cinemas)
    {
        _service = service;
        _cinemas = cinemas;
    }

    [HttpGet("")]
    public IActionResult Index(string? q, string? role, int? cinemaId, string? status, int? page)
    {
        var p = Paging.ClampPage(page);
        var vm = new StaffListVm
        {
            Query = q, Role = role, CinemaId = cinemaId, Status = status, CurrentUserId = CurrentUserId,
            Cinemas = _cinemas.Cinemas.ToList(),
            Result = _service.Search(q, role, cinemaId, ParseStatus(status), p, Paging.DefaultPageSize),
        };
        return IsPartialRequest ? PartialView("_Table", vm) : View(vm);
    }

    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        try
        {
            var u = _service.Get(id);
            return View(new StaffDetailsVm
            {
                User = u,
                CinemaName = u.CinemaId is int c ? _cinemas.Cinemas.FirstOrDefault(x => x.Id == c)?.Name : "Toàn chuỗi",
                BillsCreated = _service.BillsCreated(id),
                IsSelf = id == CurrentUserId,
            });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [HttpGet("create")]
    public IActionResult Create() => FormView(new StaffFormVm { Role = Roles.POSCashier, CinemaId = _cinemas.CurrentCinemaId });

    [HttpPost("create")]
    public IActionResult Create(StaffFormVm vm) => SaveForm(null, vm);

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        try
        {
            var u = _service.Get(id);
            return FormView(new StaffFormVm { Id = u.Id, Email = u.Email, FullName = u.FullName, Phone = u.Phone, Role = u.Role, CinemaId = u.CinemaId });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, StaffFormVm vm) => SaveForm(id, vm);

    private IActionResult SaveForm(int? id, StaffFormVm vm)
    {
        vm.Id = id;
        if (!ModelState.IsValid) return FormView(vm);
        try
        {
            var input = new StaffInput(vm.Email, vm.FullName, vm.Phone, vm.Role, vm.CinemaId);
            if (id == null)
            {
                var u = _service.Create(input);
                Toast("success", $"Đã thêm nhân viên {u.FullName}. Mật khẩu được cấp đã gửi tới {u.Email}.");
            }
            else
            {
                _service.Update(id.Value, input, CurrentUserId);
                Toast("success", "Đã lưu thay đổi");
            }
            return Redirect("/admin/users");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return FormView(vm);
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    private IActionResult FormView(StaffFormVm vm)
    {
        vm.Cinemas = _cinemas.Cinemas.ToList();
        vm.IsSelf = vm.Id == CurrentUserId;
        return View("Form", vm);
    }

    [HttpGet("{id:int}/delete-check")]
    public IActionResult DeleteCheck(int id)
    {
        var d = _service.CheckDelete(id, CurrentUserId);
        return ApiOk(new { allowed = d.Allowed, reason = d.Reason, canDeactivate = d.CanDeactivate, consequences = d.Consequences });
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        _service.Delete(id, CurrentUserId);
        return ApiOk(new { id });
    }

    /// <summary>Khóa (active = false) hoặc mở (active = true) tài khoản nhân viên; ghi AuditLogs.</summary>
    [HttpPost("{id:int}/set-active")]
    public IActionResult SetActive(int id, bool active)
    {
        _service.SetActive(id, active, CurrentUserId);
        return ApiOk(new { id, active });
    }
}
