using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>Chọn rạp ở header khách và topbar quản trị (lưu cookie).</summary>
[AllowAnonymous]
public class CinemaController : AppControllerBase
{
    private readonly CinemaContextService _cinemas;

    public CinemaController(CinemaContextService cinemas) => _cinemas = cinemas;

    [HttpPost("/cinema/select")]
    public IActionResult Select(int? cinemaId, string? returnUrl)
    {
        var ok = _cinemas.Select(cinemaId);
        if (WantsJson) return ok ? ApiOk(new { cinemaId }) : ApiFail(ErrorCodes.ValidationFailed, "Không đổi được rạp. Tài khoản của bạn gắn với một rạp cố định.");
        if (!ok) Toast("warning", "Không đổi được rạp. Tài khoản của bạn gắn với một rạp cố định.");
        return Redirect(SafeReturn(returnUrl));
    }
}

/// <summary>
/// Thanh đổi vai trò (SPEC 5, chỉ khi Demo:EnableTools; tắt thì DemoToolsMiddleware trả 404 cho mọi đường dẫn demo/):
/// đổi phiên sang một trong 5 tài khoản mẫu chỉ bằng một lần bấm.
/// </summary>
[AllowAnonymous]
[Route("demo")]
public class DemoController : AppControllerBase
{
    private readonly AuthService _auth;

    public DemoController(AuthService auth) => _auth = auth;

    /// <summary>/demo/switch-role?role=Manager&amp;returnUrl=/admin/drinks (role nhận tên vai trò hoặc tên ngắn: manager, shift, cashier, gate, khach).</summary>
    [HttpGet("switch-role")]
    public async Task<IActionResult> SwitchRole(string? role, string? returnUrl)
    {
        var account = DemoAccounts.Find(role);
        if (account == null) return NotFoundPage("Không có tài khoản mẫu cho vai trò này.");
        var user = _auth.FindByEmail(account.Email);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (user == null || !user.Active)
        {
            Toast("error", $"Tài khoản mẫu {account.Email} không có hoặc đang bị khóa. Bạn chạy lại với --reset-db để nạp lại dữ liệu mẫu.");
            return Redirect("/Login/Login");
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthService.BuildPrincipal(user),
            new AuthenticationProperties { IsPersistent = true });
        if (user.MustChangePassword) return Redirect("/Login/ChangePassword");
        var target = SafeReturn(returnUrl, user.Role);
        if (!RoleRouting.CanReturnTo(target, user.Role)) target = RoleRouting.HomePath(user.Role);
        return Redirect(target);
    }
}

/// <summary>
/// Trang công cụ phát triển (chỉ khi Demo:EnableTools): style guide (UI D1), hộp thư thử (UI D2), xem trước layout POS và màn hình hiển thị.
/// </summary>
[AllowAnonymous]
[Route("dev")]
public class DevController : AppControllerBase
{
    private readonly DevToolsService _dev;
    private readonly IClock _clock;

    public DevController(DevToolsService dev, IClock clock)
    {
        _dev = dev;
        _clock = clock;
    }

    /// <summary>Mọi thành phần ở UI mục 5 trên cả hai theme, mỗi thành phần đủ trạng thái thường, hover, đang xử lý, lỗi, bị khóa.</summary>
    [HttpGet("style-guide")]
    public IActionResult StyleGuide()
    {
        var movies = _dev.Movies(4).Select(m => new MovieCardVm(m.Id, m.Title, m.PosterUrl, m.AgeRating, DevToolsService.MovieSubline(m), null)).ToList();
        // Ví dụ tên dài khoảng 40 ký tự để thử bố cục (UI U13-15)
        movies.Add(new MovieCardVm(0, "Hành Trình Vượt Qua Những Ngọn Núi Mùa Đông", null, AgeRating.T13, "Phiêu lưu · 2 giờ 18 phút", null));
        var drinkPage = _dev.Drinks(5);
        var foods = drinkPage.Items.Take(3).Select((d, i) => new FoodCardVm(d.Drink.Id, d.Drink.Name, d.Drink.Image, d.Drink.Price, i == 2, i == 0 ? 2 : 0)).ToList();
        var vm = new StyleGuideVm
        {
            Movies = movies,
            Foods = foods,
            SampleTable = drinkPage,
            SeatMap = StyleGuideSamples.SeatMap(),
            Qr = new QrBlockVm(QrSvg.DataUri("HD20261107193000ABCD"), "HD20261107193000ABCD", _clock.Now.AddMinutes(10)),
            Now = _clock.Now,
        };
        return View(vm);
    }

    // Các điểm cuối nhỏ để thử thành phần trên style guide: công tắc thành công hoặc lỗi, vùng tải chậm hoặc lỗi
    [HttpPost("style-guide/ok")]
    public IActionResult SampleOk(bool active) => ApiOk(new { active });

    [HttpPost("style-guide/fail")]
    public IActionResult SampleFail() =>
        ApiFail(ErrorCodes.ValidationFailed, "Ví dụ lỗi: chưa lưu được trạng thái. Công tắc đã trả về như cũ, bạn thử lại sau.");

    [HttpGet("style-guide/slow")]
    public async Task<IActionResult> SampleSlow()
    {
        await Task.Delay(900);
        return Content("<div class=\"alert-ui tone-success\" role=\"status\"><i class=\"bi bi-check-circle\" aria-hidden=\"true\"></i><div>Đã tải xong dữ liệu lúc " + Fmt.Time(_clock.Now) + ".</div></div>", "text/html; charset=utf-8");
    }

    [HttpGet("style-guide/broken")]
    public IActionResult SampleBroken() => StatusCode(503);

    [HttpGet("mailbox")]
    public IActionResult Mailbox(int? id)
    {
        var items = _dev.Mailbox(100);
        var selected = id.HasValue ? _dev.Mail(id.Value) : items.FirstOrDefault();
        return View(new MailboxVm { Items = items, Selected = selected, Total = _dev.MailCount() });
    }

    [HttpPost("mailbox/clear")]
    public IActionResult ClearMailbox()
    {
        var n = _dev.ClearMailbox();
        Toast("success", n > 0 ? $"Đã xóa {n} thư trong hộp thư thử." : "Hộp thư thử đã trống.");
        return Redirect("/dev/mailbox");
    }

    /// <summary>Xem trước _LayoutPos (topbar 48px, cao 100vh) với ca đang mở của người dùng hiện tại.</summary>
    [HttpGet("layouts/pos")]
    public IActionResult LayoutPos() => View();

    /// <summary>Xem trước _LayoutDisplay; ?state=reconnecting hiện lớp phủ "Đang kết nối lại…".</summary>
    [HttpGet("layouts/display")]
    public IActionResult LayoutDisplay(string? state)
    {
        ViewData["ConnState"] = state is "reconnecting" or "lost" ? state : null;
        return View(_clock.Now);
    }
}
