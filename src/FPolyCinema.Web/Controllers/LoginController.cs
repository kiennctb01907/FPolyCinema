using System.Security.Claims;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Đăng nhập, đăng xuất, đăng ký, quên mật khẩu bằng OTP, đổi mật khẩu, hồ sơ (SPEC 2.5, UI C6 và A9; UC01–UC05).
/// Đăng nhập xong chuyển về trang chính của vai trò; tài khoản MustChangePassword = 1 phải đổi mật khẩu trước.
/// </summary>
[Authorize]
public class LoginController : AppControllerBase
{
    private readonly AuthService _auth;
    private readonly PasswordResetService _reset;
    private readonly CinemaContextService _cinemas;

    public LoginController(AuthService auth, PasswordResetService reset, CinemaContextService cinemas)
    {
        _auth = auth;
        _reset = reset;
        _cinemas = cinemas;
    }

    private Task SignInAsync(User user) =>
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthService.BuildPrincipal(user),
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = false });

    /// <summary>Không quay lại các trang đăng nhập, đăng ký sau khi đăng nhập.</summary>
    private string AfterLogin(string? returnUrl, string role)
    {
        var target = SafeReturn(returnUrl, role);
        if (target.StartsWith("/Login/", StringComparison.OrdinalIgnoreCase)) target = RoleRouting.HomePath(role);
        // Khách quay lại đúng trang đang đứng (ví dụ /booking/{suất}), trừ khu vực quản trị, quầy và /me (UI C3-05)
        if (role == Roles.Customer && !RoleRouting.CanReturnTo(target, role)) target = RoleRouting.HomePath(role);
        return target;
    }

    // ---------------------------------------------------------------- đăng nhập, đăng xuất (UC01)
    [AllowAnonymous]
    [HttpGet("/Login/Login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect(AfterLogin(returnUrl, CurrentRole ?? Roles.Customer));
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("/Login/Login")]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var (outcome, user) = _auth.Login(vm.Email, vm.Password);
        if (outcome == LoginOutcome.Locked)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản của bạn đang bị khóa. Bạn liên hệ quản lý rạp để được mở lại.");
            return View(vm);
        }
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu chưa đúng. Bạn kiểm tra lại, hoặc bấm Quên mật khẩu.");
            return View(vm);
        }
        await SignInAsync(user);
        if (user.MustChangePassword) return Redirect("/Login/ChangePassword");
        return Redirect(AfterLogin(vm.ReturnUrl, user.Role));
    }

    [HttpPost("/Login/Logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Toast("info", "Bạn đã đăng xuất.");
        return Redirect("/Login/Login");
    }

    // ---------------------------------------------------------------- đăng ký tài khoản khách (UC05)
    [AllowAnonymous]
    [HttpGet("/Login/Register")]
    public IActionResult Register(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect(RoleRouting.HomePath(CurrentRole));
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterVm());
    }

    [AllowAnonymous]
    [HttpPost("/Login/Register")]
    public async Task<IActionResult> Register(RegisterVm vm, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(vm);
        try
        {
            var user = _auth.Register(new RegisterInput(vm.FullName, vm.Email, vm.Phone, vm.BirthDate, vm.Password, vm.ConfirmPassword));
            await SignInAsync(user);
            Toast("success", $"Chào mừng {user.FullName} đến với FPoly Cinema. Mã thành viên của bạn là {user.MemberCode}.");
            return Redirect(AfterLogin(returnUrl, Roles.Customer));
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return View(vm);
        }
    }

    // ---------------------------------------------------------------- quên mật khẩu bằng OTP (UC02, SPEC 2.5)
    [AllowAnonymous]
    [HttpGet("/Login/ForgotPassword")]
    public IActionResult ForgotPassword() => View();

    /// <summary>Bước 1: nhập email, gửi mã OTP. Luôn trả cùng lời nhắn để không lộ tài khoản.</summary>
    [AllowAnonymous]
    [HttpPost("/Login/ForgotPassword")]
    public IActionResult ForgotPassword(string? email)
    {
        var r = _reset.RequestOtp(email);
        return ApiOk(new { message = PasswordResetService.GenericSentMessage, validSeconds = r.ValidSeconds, resendAfter = r.ResendAfterSeconds });
    }

    /// <summary>Bước 2: nhập mã OTP 6 số. Đúng thì mật khẩu mới được gửi qua email; sai thì OTP_INVALID, hết hạn thì OTP_EXPIRED.</summary>
    [AllowAnonymous]
    [HttpPost("/Login/VerifyOtp")]
    public IActionResult VerifyOtp(string? email, string? otp)
    {
        _reset.VerifyOtp(email, otp);
        Toast("success", "Mật khẩu mới đã được gửi tới email của bạn.");
        return ApiOk(new { message = "Mật khẩu mới đã được gửi tới email của bạn.", redirect = "/Login/Login" });
    }

    // ---------------------------------------------------------------- đổi mật khẩu (UC03; bắt buộc khi MustChangePassword = 1)
    [HttpGet("/Login/ChangePassword")]
    public IActionResult ChangePassword()
    {
        var forced = User.FindFirstValue(AppClaims.MustChangePassword) == "1";
        if (!forced && RoleRouting.IsStaff(CurrentRole)) return Redirect("/me#doi-mat-khau");
        if (forced) ViewData["Chrome"] = "bare";
        return View(new ChangePasswordVm { Forced = forced });
    }

    [HttpPost("/Login/ChangePassword")]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        var forced = User.FindFirstValue(AppClaims.MustChangePassword) == "1";
        vm.Forced = forced;
        if (!ModelState.IsValid)
        {
            if (WantsJson) return ApiValidation();
            if (forced) ViewData["Chrome"] = "bare";
            return View(vm);
        }
        try
        {
            var user = _auth.ChangePassword(CurrentUserId, vm.CurrentPassword, vm.NewPassword, vm.ConfirmPassword);
            // Đăng nhập lại với dấu mật khẩu mới: các phiên khác mang dấu cũ sẽ bị đăng xuất
            await SignInAsync(user);
            const string done = "Đã đổi mật khẩu. Các phiên đăng nhập khác đã được đăng xuất.";
            if (WantsJson) return ApiOk(new { message = done });
            Toast("success", done);
            return Redirect(RoleRouting.HomePath(user.Role));
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed && !WantsJson)
        {
            AddErrors(ex);
            if (forced) ViewData["Chrome"] = "bare";
            return View(vm);
        }
    }

    // ---------------------------------------------------------------- hồ sơ (UC04): /me cho nhân viên (UI A9), /Login/Profile cho khách
    [HttpGet("/Login/Profile")]
    public IActionResult Profile()
    {
        if (RoleRouting.IsStaff(CurrentRole)) return Redirect("/me");
        if (CurrentRole == Roles.Customer) return Redirect("/account?tab=profile");
        var user = _auth.GetUser(CurrentUserId);
        if (user == null) return NotFoundPage();
        return ProfileView(user, new ProfileVm { FullName = user.FullName, Phone = user.Phone, BirthDate = user.BirthDate });
    }

    /// <summary>Trang cá nhân nhân viên (UI A9), policy EmployeeOrManager theo CONTRACTS mục 2.</summary>
    [HttpGet("/me")]
    [Authorize(Policy = Policies.EmployeeOrManager)]
    public IActionResult Me()
    {
        var user = _auth.GetUser(CurrentUserId);
        if (user == null) return NotFoundPage();
        return ProfileView(user, new ProfileVm { FullName = user.FullName, Phone = user.Phone, BirthDate = user.BirthDate });
    }

    [HttpPost("/Login/Profile")]
    public async Task<IActionResult> Profile(ProfileVm vm)
    {
        ModelState.Remove(nameof(ProfileVm.Password) + "." + nameof(ChangePasswordVm.CurrentPassword));
        ModelState.Remove(nameof(ProfileVm.Password) + "." + nameof(ChangePasswordVm.NewPassword));
        ModelState.Remove(nameof(ProfileVm.Password) + "." + nameof(ChangePasswordVm.ConfirmPassword));
        var current = _auth.GetUser(CurrentUserId);
        if (current == null) return NotFoundPage();
        if (!ModelState.IsValid) return WantsJson ? ApiValidation() : ProfileView(current, vm);
        try
        {
            var user = _auth.UpdateProfile(CurrentUserId, vm.FullName, vm.Phone, RoleRouting.IsStaff(current.Role) ? current.BirthDate : vm.BirthDate);
            await SignInAsync(user);
            if (WantsJson) return ApiOk(new { message = "Đã lưu thay đổi", fullName = user.FullName, initials = Fmt.Initials(user.FullName) });
            Toast("success", "Đã lưu thay đổi");
            return Redirect(RoleRouting.IsStaff(user.Role) ? "/me" : "/account?tab=profile");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed && !WantsJson)
        {
            AddErrors(ex);
            return ProfileView(current, vm);
        }
    }

    private IActionResult ProfileView(User user, ProfileVm vm)
    {
        vm.Email = user.Email;
        vm.RoleLabel = StatusDisplay.RoleLabel(user.Role);
        vm.CinemaName = user.CinemaId is int cid ? _cinemas.Cinemas.FirstOrDefault(c => c.Id == cid)?.Name : (user.Role == Roles.Manager ? "Toàn chuỗi" : null);
        vm.Initials = Fmt.Initials(user.FullName);
        vm.IsCustomer = user.Role == Roles.Customer;
        vm.MemberCode = user.MemberCode;
        vm.TierLabel = StatusDisplay.Of(StatusDisplay.Tier, user.Tier).Label;
        vm.LoyaltyPoints = user.LoyaltyPoints;
        if (vm.IsCustomer) return View("Profile", vm);
        ViewData["Layout"] = RoleRouting.LayoutOf(RoleRouting.AreaOfRole(user.Role));
        return View("Me", vm);
    }
}
