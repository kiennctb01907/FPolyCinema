using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FPolyCinema.Web.Controllers;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Hubs;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FPolyCinema.Tests;

/// <summary>
/// Test HTTP Phase 5: route và policy của web khách (UI C1–C5), khách vãng lai xem được phim, lịch chiếu, sơ đồ ghế nhưng không giữ ghế được;
/// trang tài khoản cần đăng nhập; webhook thanh toán xác thực bằng chữ ký (401 khi sai, không cần Anti-Forgery); trang mô phỏng cổng thanh toán; kênh 3 JoinBill.
/// </summary>
[Collection("Database")]
public class Phase5WebTests
{
    private readonly SeededDatabaseFixture _db;

    public Phase5WebTests(SeededDatabaseFixture db)
    {
        _db = db;
        DBUtil.Init(db.ConnectionString);
    }

    private static HttpClient Client(WebAppFactory f) => f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> TokenFrom(HttpClient c, string url)
    {
        var html = await (await c.GetAsync(url)).Content.ReadAsStringAsync();
        return Regex.Match(html, "<meta name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
    }

    private static IEnumerable<string> Templates(Type controller)
    {
        var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template;
        foreach (var m in controller.GetMethods())
            foreach (var a in m.GetCustomAttributes<HttpMethodAttribute>())
                yield return a.Template!.StartsWith('/') ? a.Template : "/" + (prefix == null ? a.Template : a.Template == "" ? prefix : prefix + "/" + a.Template);
    }

    [Fact]
    public void Controllers_RoutesOfCustomerScreensMatchUi_PoliciesAndHubChannel3()
    {
        Assert.Contains("/", Templates(typeof(HomeController)));
        Assert.Contains("/movies/{id:int}", Templates(typeof(CustomerController)));
        Assert.Contains("/booking/{showtimeId:int}", Templates(typeof(BookingController)));
        Assert.Contains("/tickets/{billCode}", Templates(typeof(TicketsController)));
        Assert.Contains("/account", Templates(typeof(AccountController)));
        Assert.Contains("/payment-sim/pay", Templates(typeof(PaymentController)));
        Assert.Contains("/payments/webhook", Templates(typeof(PaymentController)));
        string? Policy(MemberInfo m) => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p != null);
        Assert.Equal(Policies.CustomerOnly, Policy(typeof(BookingController)));
        Assert.Equal(Policies.CustomerOnly, Policy(typeof(TicketsController)));
        Assert.Equal(Policies.CustomerOnly, Policy(typeof(AccountController)));
        Assert.NotNull(typeof(BookingController).GetMethod(nameof(BookingController.Index))!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(typeof(PaymentController).GetMethod(nameof(PaymentController.Webhook))!.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>());
        Assert.NotNull(typeof(CinemaHub).GetMethod("JoinBill"));
    }

    [Fact]
    public async Task Guest_SeesHomeMoviesScheduleMenuAndSeatMap_ButCannotHoldSeats()
    {
        using var f = new WebAppFactory(_db.ConnectionString, tools: false);
        var c = Client(f);
        var movie = MovieDAL.GetAll().First(m => m.Active && m.AgeRating != AgeRating.C);
        var show = ShowtimeDAL.GetAll().First(s => s.MovieId != 0 && MovieDAL.GetById(s.MovieId)!.AgeRating != AgeRating.C);
        var pages = new Dictionary<string, string[]>
        {
            ["/"] = new[] { "Mua vé", "Đang chiếu", "Sắp chiếu", "Combo bắp nước nổi bật", "Ưu đãi" },
            ["/movies"] = new[] { "Đang chiếu", "Sắp chiếu" },
            [$"/movies/{movie.Id}"] = new[] { "Lịch chiếu", "Thời lượng", "Số giấy phép", "Hôm nay" },
            ["/showtimes"] = new[] { "Mọi định dạng", "Mọi ngôn ngữ", "Tất cả rạp" },
            ["/menu"] = new[] { "Bắp nước" },
            [$"/booking/{show.Id}"] = new[] { "Bản đồ điểm AI", "Chọn giúp tôi", "Đẹp nhất", "Gần màn hình", "Cặp đôi", "Gần lối đi", "Đăng nhập để chọn ghế", "data-seatmap" },
        };
        foreach (var (url, texts) in pages)
        {
            var res = await c.GetAsync(url);
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"{url} trả {(int)res.StatusCode}");
            var html = await res.Content.ReadAsStringAsync();
            foreach (var t in texts) Assert.Contains(t, html);
        }
        var suggest = await c.GetAsync($"/booking/{show.Id}/suggest?persona=NEAR_SCREEN&qty=2");
        Assert.Equal(HttpStatusCode.OK, suggest.StatusCode);
        Assert.Contains("\"clusters\"", await suggest.Content.ReadAsStringAsync());

        var token = await TokenFrom(c, $"/booking/{show.Id}");
        var req = new HttpRequestMessage(HttpMethod.Post, $"/booking/{show.Id}/hold") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["seatIds"] = "1" }) };
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        req.Headers.Add("Accept", "application/json");
        req.Headers.Add("RequestVerificationToken", token);
        var hold = await c.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, hold.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/account")).StatusCode);
    }

    [Fact]
    public async Task Customer_AccountTabs_ProfileRedirect_StaffCannotBook()
    {
        using var f = new WebAppFactory(_db.ConnectionString, tools: true);
        var c = Client(f);
        await c.GetAsync("/demo/switch-role?role=khach&returnUrl=%2F");
        foreach (var tab in AccountController.Tabs)
        {
            var res = await c.GetAsync("/account?tab=" + tab);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = await res.Content.ReadAsStringAsync();
            Assert.Contains("Hạng thành viên", html);
            Assert.Contains("Vé của tôi", html);
            Assert.Contains("Thông tin cá nhân", html);
        }
        var profile = await c.GetAsync("/Login/Profile");
        Assert.Equal("/account?tab=profile", profile.Headers.Location!.OriginalString);

        var staff = Client(f);
        await staff.GetAsync("/demo/switch-role?role=cashier&returnUrl=%2Fme");
        Assert.NotEqual(HttpStatusCode.OK, (await staff.GetAsync("/account")).StatusCode);
    }

    [Fact]
    public async Task Webhook_WrongSignature401_WithoutAntiForgery_SimPageShowsTwoButtons()
    {
        using var f = new WebAppFactory(_db.ConnectionString, tools: false);
        var c = Client(f);
        var body = new StringContent("{\"billId\":1,\"providerTxnId\":\"SIMX\",\"status\":\"SUCCEEDED\",\"amount\":1}", System.Text.Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, "/payments/webhook") { Content = body };
        req.Headers.Add("X-Signature", "0000");
        var res = await c.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var err = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.False(err.GetProperty("ok").GetBoolean());

        var online = BillDAL.GetAll().FirstOrDefault(b => b.Channel == BillChannel.Online);
        Assert.NotNull(online);
        var sim = await c.GetAsync($"/payment-sim/pay?billId={online!.Id}");
        Assert.Equal(HttpStatusCode.OK, sim.StatusCode);
        var html = await sim.Content.ReadAsStringAsync();
        Assert.Contains("Thanh toán thành công", html);
        Assert.Contains("Thất bại", html);
        Assert.Contains("/js/pages/payment-sim.js", html);
    }

    [Fact]
    public async Task Login_CustomerReturnsToBookingOfSameShowtime_ButNotToStaffArea()
    {
        // UI C3-05: khách chưa đăng nhập bấm ghế → đăng nhập → quay lại đúng suất; returnUrl vào khu quản trị thì về trang chủ
        using var f = new WebAppFactory(_db.ConnectionString, tools: false);
        async Task<string> LoginTo(string returnUrl)
        {
            var c = Client(f);
            var html = await (await c.GetAsync("/Login/Login?returnUrl=" + Uri.EscapeDataString(returnUrl))).Content.ReadAsStringAsync();
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            var post = await c.PostAsync("/Login/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "khach@fpolycinema.vn", ["Password"] = "123456", ["ReturnUrl"] = returnUrl, ["__RequestVerificationToken"] = token,
            }));
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            return post.Headers.Location!.OriginalString;
        }
        Assert.Equal("/booking/42?persona=NEAR_AISLE&qty=3", await LoginTo("/booking/42?persona=NEAR_AISLE&qty=3"));
        Assert.Equal("/movies/3", await LoginTo("/movies/3"));
        Assert.Equal("/", await LoginTo("/admin/bills"));
        Assert.Equal("/", await LoginTo("/pos/index"));
    }

    [Fact]
    public async Task AccountProfile_InvalidShowsErrorsInProfileTab_ValidSavesAndRedirects()
    {
        // UC04: form Thông tin cá nhân của /account gửi /account/profile; dữ liệu sai thì hiện lại tab kèm lỗi ô, đúng thì lưu và quay lại tab
        using var f = new WebAppFactory(_db.ConnectionString, tools: false);
        var c = Client(f);
        var login = await (await c.GetAsync("/Login/Login")).Content.ReadAsStringAsync();
        string FormToken(string html) => Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await c.PostAsync("/Login/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "khach@fpolycinema.vn", ["Password"] = "123456", ["__RequestVerificationToken"] = FormToken(login),
        }));
        var page = await (await c.GetAsync("/account?tab=profile")).Content.ReadAsStringAsync();
        Assert.Contains("action=\"/account/profile\"", page);
        var token = FormToken(page);
        var user = UserDAL.GetByEmail("khach@fpolycinema.vn")!;

        var bad = await c.PostAsync("/account/profile", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = user.FullName, ["Phone"] = "12ab", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        var html = await bad.Content.ReadAsStringAsync();
        Assert.Contains("Số điện thoại gồm 9–11 chữ số", html);
        Assert.Contains("Tài khoản của tôi", html);
        Assert.Contains("aria-current=\"page\"><i class=\"bi bi-person\"", html);

        var ok = await c.PostAsync("/account/profile", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = user.FullName, ["Phone"] = user.Phone ?? "0901234567", ["BirthDate"] = user.BirthDate?.ToString("yyyy-MM-dd") ?? "", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Equal("/account?tab=profile", ok.Headers.Location!.OriginalString);
    }
}
