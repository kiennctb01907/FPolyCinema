using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Tài khoản khách C5 (UC03, UC04, UC12): thẻ hạng có thanh tiến độ và mã thành viên dạng QR; tab Vé của tôi (sắp tới, đã xem),
/// Ví và điểm, Voucher, Thông báo, Thông tin cá nhân (cập nhật hồ sơ qua /Login/Profile, đổi mật khẩu qua /Login/ChangePassword).
/// </summary>
[Authorize(Policy = Policies.CustomerOnly)]
public class AccountController : AppControllerBase
{
    public static readonly string[] Tabs = { "tickets", "wallet", "voucher", "notifications", "profile" };

    private readonly IClock _clock;
    private readonly CinemaRules _rules;

    public AccountController(IClock clock, CinemaRules rules)
    {
        _clock = clock;
        _rules = rules;
    }

    [HttpGet("/account")]
    public IActionResult Index(string? tab)
    {
        var user = UserDAL.GetById(CurrentUserId);
        if (user == null) return NotFoundPage();
        var now = _clock.Now;
        var bills = BillDAL.ForCustomer(user.Id);
        bool upcoming(Entities.ReadModels.CustomerBillRow b) => b.Status == BillStatus.Finish && b.EndTime is DateTime e && e > now;
        var spend = BillDAL.CustomerSpend(user.Id, now.AddMonths(-12), now);
        return View(new AccountVm
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
        });
    }

    /// <summary>Đánh dấu mọi thông báo đã đọc.</summary>
    [HttpPost("/account/notifications/read")]
    public IActionResult ReadAll()
    {
        NotificationDAL.MarkAllRead(CurrentUserId);
        return ApiOk(new { unread = 0 });
    }
}
