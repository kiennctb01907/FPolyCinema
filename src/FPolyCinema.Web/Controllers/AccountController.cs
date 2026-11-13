using FPolyCinema.Web.DAL;
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
/// Tài khoản khách C5 (UC03, UC04, UC12): thẻ hạng có thanh tiến độ và mã thành viên dạng QR; tab Vé của tôi (sắp tới, đã xem),
/// Ví và điểm, Voucher, Thông báo, Thông tin cá nhân (cập nhật hồ sơ qua POST /account/profile, dữ liệu sai thì hiện lại đúng tab kèm lỗi từng ô; đổi mật khẩu qua /Login/ChangePassword).
/// </summary>
[Authorize(Policy = Policies.CustomerOnly)]
public class AccountController : AppControllerBase
{
    public static readonly string[] Tabs = { "tickets", "wallet", "voucher", "notifications", "profile" };

    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly AuthService _auth;

    public AccountController(IClock clock, CinemaRules rules, AuthService auth)
    {
        _clock = clock;
        _rules = rules;
        _auth = auth;
    }

    [HttpGet("/account")]
    public IActionResult Index(string? tab)
    {
        var user = UserDAL.GetById(CurrentUserId);
        if (user == null) return NotFoundPage();
        return View(Build(user, tab));
    }

    /// <summary>Cập nhật thông tin cá nhân (UC04). Dữ liệu sai thì hiện lại tab Thông tin cá nhân với lỗi ở từng ô.</summary>
    [HttpPost("/account/profile")]
    public async Task<IActionResult> Profile(ProfileVm vm)
    {
        var user = UserDAL.GetById(CurrentUserId);
        if (user == null) return NotFoundPage();
        foreach (var k in ModelState.Keys.Where(k => k.StartsWith(nameof(ProfileVm.Password) + ".")).ToList()) ModelState.Remove(k);
        if (ModelState.IsValid)
        {
            try
            {
                var updated = _auth.UpdateProfile(user.Id, vm.FullName, vm.Phone, vm.BirthDate);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthService.BuildPrincipal(updated),
                    new AuthenticationProperties { IsPersistent = true, AllowRefresh = false });
                Toast("success", "Đã lưu thay đổi");
                return Redirect("/account?tab=profile");
            }
            catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
            {
                AddErrors(ex);
            }
        }
        var page = Build(user, "profile");
        page.Profile = new ProfileVm { FullName = vm.FullName, Phone = vm.Phone, BirthDate = vm.BirthDate, Email = user.Email };
        return View("Index", page);
    }

    /// <summary>Dữ liệu trang tài khoản: thẻ hạng, vé, ví và điểm, voucher, thông báo, hồ sơ.</summary>
    private AccountVm Build(User user, string? tab)
    {
        var now = _clock.Now;
        var bills = BillDAL.ForCustomer(user.Id);
        bool upcoming(Entities.ReadModels.CustomerBillRow b) => b.Status == BillStatus.Finish && b.EndTime is DateTime e && e > now;
        var spend = BillDAL.CustomerSpend(user.Id, now.AddMonths(-12), now);
        return new AccountVm
        {
            User = user, Tab = Tabs.Contains(tab) ? tab! : "tickets", Now = now,
            Progress = TierProgressCalculator.Of(spend, _rules.TierVipSpend12Months, _rules.TierVvipSpend12Months),
            MemberQr = QrSvg.DataUri(user.MemberCode ?? user.Email),
            Upcoming = bills.Where(upcoming).OrderBy(b => b.StartTime).ToList(),
            Past = bills.Where(b => !upcoming(b)).ToList(),
            Wallet = WalletTransactionDAL.ForUser(user.Id), Points = PointTransactionDAL.ForUser(user.Id),
            Vouchers = VoucherDAL.ForOwner(user.Id).Concat(VoucherDAL.PublicPromos(now)).ToList(),
            Notifications = NotificationDAL.ForUser(user.Id), Unread = NotificationDAL.CountUnread(user.Id),
            PointValueVnd = _rules.PointValueVnd, MaxPointsRedeemPercent = _rules.MaxPointsRedeemPercent,
            Profile = new ProfileVm { FullName = user.FullName, Phone = user.Phone, BirthDate = user.BirthDate, Email = user.Email },
        };
    }

    /// <summary>Đánh dấu mọi thông báo đã đọc.</summary>
    [HttpPost("/account/notifications/read")]
    public IActionResult ReadAll()
    {
        NotificationDAL.MarkAllRead(CurrentUserId);
        return ApiOk(new { unread = 0 });
    }
}
