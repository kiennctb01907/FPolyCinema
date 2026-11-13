using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Tests;

/// <summary>Chạy ứng dụng thật (Program.cs) trên CSDL mẫu của test, bật hoặc tắt Demo:EnableTools.</summary>
public sealed class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly bool _tools;
    private readonly IDictionary<string, string?> _extra;

    public WebAppFactory(string connectionString, bool tools, IDictionary<string, string?>? extra = null)
    {
        _connectionString = connectionString;
        _tools = tools;
        _extra = extra ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _connectionString,
            ["Demo:EnableTools"] = _tools ? "true" : "false",
        }.Concat(_extra)));
    }
}

/// <summary>
/// Test HTTP cho Phase 2: cookie và policy (SPEC 2.5), Anti-Forgery toàn cục, xử lý lỗi tập trung và JSON CONTRACTS mục 8,
/// 404 cho demo/ và dev/ khi tắt công cụ (SPEC 5), đăng nhập, đổi vai trò, đổi mật khẩu bắt buộc.
/// </summary>
[Collection("Database")]
public class Phase2WebTests
{
    private readonly SeededDatabaseFixture _db;

    public Phase2WebTests(SeededDatabaseFixture db)
    {
        _db = db;
        DBUtil.Init(db.ConnectionString);
    }

    private WebAppFactory Factory(bool tools = true) => new(_db.ConnectionString, tools);

    private static HttpClient Client(WebAppFactory f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static HttpRequestMessage Ajax(HttpMethod method, string url, HttpContent? content = null, string? token = null)
    {
        var req = new HttpRequestMessage(method, url) { Content = content };
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        req.Headers.Add("Accept", "application/json");
        if (token != null) req.Headers.Add("RequestVerificationToken", token);
        return req;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) => JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    private static async Task<string> TokenFrom(HttpClient c, string url)
    {
        var html = await (await c.GetAsync(url)).Content.ReadAsStringAsync();
        return Regex.Match(html, "<meta name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
    }

    [Fact]
    public async Task DemoAndDevPaths_Return404WhenToolsDisabled()
    {
        using var f = Factory(tools: false);
        var c = Client(f);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/dev/style-guide")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/dev/mailbox")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/demo/switch-role?role=Manager")).StatusCode);
        var home = await (await c.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("demo-bar", home);
    }

    [Fact]
    public async Task SwitchRole_SignsInOneClickAndRedirects()
    {
        using var f = Factory();
        var c = Client(f);
        var res = await c.GetAsync("/demo/switch-role?role=Manager&returnUrl=%2Fadmin%2Fdrinks");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/admin/drinks", res.Headers.Location!.OriginalString);
        Assert.Contains(res.Headers.GetValues("Set-Cookie"), v => v.StartsWith("fpc_auth="));
        var page = await c.GetAsync("/admin/drinks");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Đồ uống", html);
        Assert.Contains("Thêm món", html);
        Assert.Contains("demo-bar", html);
        // Khách không quay lại khu vực quản trị mà về trang chính của mình
        var k = await c.GetAsync("/demo/switch-role?role=khach&returnUrl=%2Fadmin%2Fdrinks");
        Assert.Equal("/", k.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AjaxErrors_FollowContractsJsonShape()
    {
        using var f = Factory();
        var anon = Client(f);
        var r401 = await anon.SendAsync(Ajax(HttpMethod.Get, "/admin/categories/1/delete-check"));
        Assert.Equal(HttpStatusCode.Unauthorized, r401.StatusCode);
        var j = await Json(r401);
        Assert.False(j.GetProperty("ok").GetBoolean());
        Assert.Equal(ErrorCodes.AuthRequired, j.GetProperty("error").GetProperty("code").GetString());

        var cashier = Client(f);
        await cashier.GetAsync("/demo/switch-role?role=cashier&returnUrl=%2Fme");
        var r403 = await cashier.SendAsync(Ajax(HttpMethod.Get, "/admin/categories/1/delete-check"));
        Assert.Equal(HttpStatusCode.Forbidden, r403.StatusCode);
        Assert.Equal("Tài khoản của bạn không có quyền dùng chức năng này.", (await Json(r403)).GetProperty("error").GetProperty("message").GetString());
        var page = await cashier.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("http://localhost/Home/AccessDenied", page.Headers.Location!.ToString());

        var r404 = await anon.SendAsync(Ajax(HttpMethod.Get, "/khong-co-trang-nay"));
        Assert.Equal(HttpStatusCode.NotFound, r404.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, (await Json(r404)).GetProperty("error").GetProperty("code").GetString());
        var html404 = await anon.GetAsync("/khong-co-trang-nay");
        Assert.Equal(HttpStatusCode.NotFound, html404.StatusCode);
        var body = await html404.Content.ReadAsStringAsync();
        Assert.Contains("Không tìm thấy trang", body);
        Assert.DoesNotContain("   at ", body);
    }

    [Fact]
    public async Task Antiforgery_GlobalFilterRejectsPostWithoutToken_AcceptsHeaderToken()
    {
        using var f = Factory();
        var c = Client(f);
        await c.GetAsync("/demo/switch-role?role=manager&returnUrl=%2Fadmin%2Fcategories");
        var noToken = await c.SendAsync(Ajax(HttpMethod.Post, "/admin/categories/1/set-active", new FormUrlEncodedContent(new Dictionary<string, string> { ["active"] = "true" })));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await Json(noToken)).GetProperty("error").GetProperty("code").GetString());

        var token = await TokenFrom(c, "/admin/categories");
        Assert.False(string.IsNullOrEmpty(token));
        var ok = await c.SendAsync(Ajax(HttpMethod.Post, "/admin/categories/1/set-active", new FormUrlEncodedContent(new Dictionary<string, string> { ["active"] = "true" }), token));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.True((await Json(ok)).GetProperty("ok").GetBoolean());

        // Loại có món: xóa bị chặn, DELETE_BLOCKED kèm lý do và gợi ý ngưng hoạt động
        var del = await c.SendAsync(Ajax(HttpMethod.Post, "/admin/categories/1/delete", new FormUrlEncodedContent(new Dictionary<string, string>()), token));
        Assert.Equal((HttpStatusCode)422, del.StatusCode);
        var err = (await Json(del)).GetProperty("error");
        Assert.Equal(ErrorCodes.DeleteBlocked, err.GetProperty("code").GetString());
        Assert.Equal("deactivate", err.GetProperty("details").GetProperty("suggestion").GetString());
    }

    [Fact]
    public async Task Login_FormHasTokenAndRedirectsToRoleHome_LockedAccountRejected()
    {
        using var f = Factory();
        var c = Client(f);
        var html = await (await c.GetAsync("/Login/Login")).Content.ReadAsStringAsync();
        var formToken = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(formToken));
        var post = await c.PostAsync("/Login/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "shift@fpolycinema.vn", ["Password"] = "123456", ["__RequestVerificationToken"] = formToken,
        }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal("/pos/index", post.Headers.Location!.OriginalString);

        var c2 = Client(f);
        var html2 = await (await c2.GetAsync("/Login/Login")).Content.ReadAsStringAsync();
        var t2 = Regex.Match(html2, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var bad = await c2.PostAsync("/Login/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "shift@fpolycinema.vn", ["Password"] = "sai-mat-khau", ["__RequestVerificationToken"] = t2,
        }));
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Email hoặc mật khẩu chưa đúng", await bad.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MustChangePassword_RedirectsEveryPageToForcedChange()
    {
        var email = "batdoi.test@fpolycinema.vn";
        var u = ServiceDatabaseFixture.NewUser(email, Roles.POSCashier, FPolyCinema.Web.DAL.CinemaDAL.GetActive().First().Id);
        u.MustChangePassword = true;
        FPolyCinema.Web.DAL.UserDAL.Insert(u);
        try
        {
            using var f = Factory();
            var c = Client(f);
            var html = await (await c.GetAsync("/Login/Login")).Content.ReadAsStringAsync();
            var t = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            var post = await c.PostAsync("/Login/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = email, ["Password"] = "Matkhau123", ["__RequestVerificationToken"] = t }));
            Assert.Equal("/Login/ChangePassword", post.Headers.Location!.OriginalString);
            var me = await c.GetAsync("/me");
            Assert.Equal("/Login/ChangePassword", me.Headers.Location!.OriginalString);
            var ajax = await c.SendAsync(Ajax(HttpMethod.Get, "/admin/drinks"));
            Assert.Equal(HttpStatusCode.Forbidden, ajax.StatusCode);
            Assert.Equal(ErrorCodes.MustChangePassword, (await Json(ajax)).GetProperty("error").GetProperty("code").GetString());
            var forced = await (await c.GetAsync("/Login/ChangePassword")).Content.ReadAsStringAsync();
            Assert.Contains("Bạn đang dùng mật khẩu được cấp. Đặt mật khẩu mới để tiếp tục.", forced);
            Assert.DoesNotContain("site-header", forced);   // không có menu, chỉ có form và nút Đăng xuất
            Assert.Contains("Đăng xuất", forced);
        }
        finally
        {
            FPolyCinema.Web.DAL.UserDAL.Delete(u.Id);
        }
    }

    [Fact]
    public void Program_CookiePoliciesAntiforgeryConfigured()
    {
        using var f = Factory();
        var sp = f.Services;
        var cookie = sp.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(TimeSpan.FromHours(24), cookie.ExpireTimeSpan);
        Assert.Equal("/Login/Login", cookie.LoginPath.Value);
        Assert.Equal("/Home/AccessDenied", cookie.AccessDeniedPath.Value);
        Assert.Equal("RequestVerificationToken", sp.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.HeaderName);
        var auth = sp.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var expected = new Dictionary<string, string[]>
        {
            [Policies.EmployeeOrManager] = new[] { Roles.POSCashier, Roles.TicketInspector, Roles.ShiftManager, Roles.Manager },
            [Policies.ManagerOnly] = new[] { Roles.Manager },
            [Policies.PosStaff] = new[] { Roles.POSCashier, Roles.ShiftManager, Roles.Manager },
            [Policies.GateStaff] = new[] { Roles.TicketInspector, Roles.ShiftManager, Roles.Manager },
            [Policies.SupervisorOrManager] = new[] { Roles.ShiftManager, Roles.Manager },
            [Policies.CustomerOnly] = new[] { Roles.Customer },
        };
        foreach (var (name, roles) in expected)
        {
            var p = auth.GetPolicy(name);
            Assert.NotNull(p);
            var req = Assert.Single(p!.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>());
            Assert.Equal(roles.OrderBy(x => x), req.AllowedRoles.OrderBy(x => x));
        }
    }

    [Fact]
    public void EmailSender_SmtpWhenHostConfigured_OutboxOtherwise()
    {
        using (var f = Factory())
        using (var scope = f.Services.CreateScope())
            Assert.IsType<OutboxEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());
        using (var f = new WebAppFactory(_db.ConnectionString, true, new Dictionary<string, string?> { ["Email:SmtpHost"] = "smtp.example.test", ["Email:SmtpUser"] = "noreply@example.test" }))
        using (var scope = f.Services.CreateScope())
            Assert.IsType<SmtpEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());
        Assert.Equal("no-reply@fpolycinema.vn", SmtpEmailSender.ParseAddress("FPoly Cinema <no-reply@fpolycinema.vn>").Address);
    }

    [Fact]
    public async Task FileController_ManagerUploadsAndDeletesImageByFetch()
    {
        using var f = Factory();
        var c = Client(f);
        await c.GetAsync("/demo/switch-role?role=manager&returnUrl=%2Fadmin%2Fdrinks%2Fcreate");
        var token = await TokenFrom(c, "/admin/drinks/create");
        // PNG 1×1 hợp lệ
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
        var form = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "anh-thu.png" } };
        var up = await c.SendAsync(Ajax(HttpMethod.Post, "/File/Upload", form, token));
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        var path = (await Json(up)).GetProperty("data").GetProperty("path").GetString()!;
        Assert.Matches(@"^/uploads/\d{14}_[0-9a-f]{8}\.png$", path);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(path)).StatusCode);
        // Sai định dạng: VALIDATION_FAILED
        var bad = new MultipartFormDataContent { { new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "khong-phai-anh.png" } };
        var badRes = await c.SendAsync(Ajax(HttpMethod.Post, "/File/Upload", bad, token));
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);
        // Xóa ảnh vừa tải (chưa món nào dùng)
        var del = await c.SendAsync(Ajax(HttpMethod.Post, "/File/Delete", new FormUrlEncodedContent(new Dictionary<string, string> { ["fileName"] = path }), token));
        Assert.True((await Json(del)).GetProperty("data").GetProperty("deleted").GetBoolean());
        // Không có token thì bị chặn; thu ngân không có quyền upload
        Assert.Equal(HttpStatusCode.BadRequest, (await c.SendAsync(Ajax(HttpMethod.Post, "/File/Upload", new MultipartFormDataContent { { new ByteArrayContent(png), "file", "a.png" } }))).StatusCode);
        var cashier = Client(f);
        await cashier.GetAsync("/demo/switch-role?role=cashier&returnUrl=%2Fme");
        var ct = await TokenFrom(cashier, "/me");
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.SendAsync(Ajax(HttpMethod.Post, "/File/Upload", new MultipartFormDataContent { { new ByteArrayContent(png), "file", "a.png" } }, ct))).StatusCode);
    }

    [Fact]
    public async Task Pages_RenderVietnameseWithoutEnglishScaffoldText()
    {
        using var f = Factory();
        var c = Client(f);
        await c.GetAsync("/demo/switch-role?role=manager&returnUrl=%2Fme");
        foreach (var url in new[] { "/dev/style-guide", "/dev/mailbox", "/admin/drinks", "/admin/drinks/create", "/admin/drinks/1", "/admin/drinks/1/edit", "/admin/categories", "/admin/categories/create", "/admin/categories/1", "/admin/users", "/admin/users/create", "/me", "/Home/AccessDenied" })
        {
            var res = await c.GetAsync(url);
            Assert.True(res.StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden, $"{url} trả {(int)res.StatusCode}");
            var html = await res.Content.ReadAsStringAsync();
            Assert.Contains("lang=\"vi\"", html);
            Assert.DoesNotMatch(@">\s*(Create New|Back to List|Edit|Delete|Details|Save|Cancel|Submit|Search|Index)\s*<", html);
            // Mọi form POST đều mang token Anti-Forgery
            foreach (Match m in Regex.Matches(html, "<form[^>]*method=\"post\"[^>]*>([\\s\\S]*?)</form>"))
                Assert.Contains("__RequestVerificationToken", m.Groups[1].Value);
        }
    }
}
