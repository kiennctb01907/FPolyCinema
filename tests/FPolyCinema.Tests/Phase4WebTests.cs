using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FPolyCinema.Web.Controllers;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Hubs;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FPolyCinema.Tests;

/// <summary>Test HTTP Phase 4: policy và route của PosController, BillsController, StatisticsController; màn hình POS, quản trị; Anti-Forgery; hub /hubs/cinema.</summary>
[Collection("Database")]
public class Phase4WebTests
{
    private readonly SeededDatabaseFixture _db;

    public Phase4WebTests(SeededDatabaseFixture db)
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

    private static HttpRequestMessage Ajax(string url, Dictionary<string, string> form, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        req.Headers.Add("Accept", "application/json");
        if (token != null) req.Headers.Add("RequestVerificationToken", token);
        return req;
    }

    private static IEnumerable<string> Templates(Type controller)
    {
        var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template;
        foreach (var m in controller.GetMethods())
            foreach (var a in m.GetCustomAttributes<HttpMethodAttribute>())
                yield return "/" + (prefix == null ? a.Template : a.Template == "" ? prefix : prefix + "/" + a.Template);
    }

    [Fact]
    public void Controllers_Phase4PoliciesAndRoutesMatchContracts()
    {
        string? Policy(MemberInfo m) => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p != null);
        Assert.Equal(Policies.PosStaff, Policy(typeof(PosController)));
        Assert.Equal(Policies.ManagerOnly, Policy(typeof(BillsController)));
        Assert.Equal(Policies.ManagerOnly, Policy(typeof(StatisticsController)));
        Assert.Equal("admin/bills", typeof(BillsController).GetCustomAttribute<RouteAttribute>()!.Template);
        var pos = Templates(typeof(PosController)).ToHashSet();
        foreach (var r in new[] { "/pos/index", "/pos/init", "/pos/update-quantity", "/pos/checkout", "/pos/cancel", "/pos/select-showtime", "/pos/hold-seats",
                     "/pos/set-audience", "/pos/lookup-member", "/pos/apply-voucher", "/pos/request-void", "/pos/print", "/pos/my-bills", "/pos/open-shift", "/pos/close-shift" })
            Assert.Contains(r, pos);
        var stats = Templates(typeof(StatisticsController)).ToHashSet();
        Assert.Contains("/admin", stats);
        Assert.Contains("/admin/reports", stats);
        Assert.Contains("/admin/reports/settlement", stats);
        // Hub chỉ có hàm tham gia và rời nhóm
        var hubMethods = typeof(CinemaHub).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name).ToList();
        Assert.All(hubMethods, n => Assert.Matches("^(Join|Leave)", n));
        Assert.Equal("/hubs/cinema", CinemaHub.Path);
    }

    /// <summary>BUG-073: form mở ca gửi bằng submit thường nên phải tự mang token; thiếu token thì bị trang "Phiên làm việc đã hết hạn".</summary>
    [Fact]
    public async Task OpenShift_FormCarriesAntiforgeryToken_PostIsNotRejected()
    {
        using var f = new WebAppFactory(_db.ConnectionString, tools: true);
        var mgr = Client(f);
        await mgr.GetAsync("/demo/switch-role?role=manager&returnUrl=%2Fme");
        var page = await mgr.GetAsync("/pos/open-shift");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        var form = Regex.Match(html, "<form[^>]*data-shift-wizard[\\s\\S]*?</form>").Value;
        var token = Regex.Match(form, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "Form mở ca không có token Anti-Forgery");
        // Gửi như trình duyệt (token trong form, không qua header), thiếu tiền đầu ca: phải về lại form kèm lỗi, không phải 400
        var post = await mgr.PostAsync("/pos/open-shift", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["terminal"] = "POS-01", ["openingCashFloat"] = "", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        Assert.DoesNotContain("Phiên làm việc đã hết hạn", await post.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Pages_RoleAccess_PosForPosStaff_AdminForManager()
    {
        using var f = new WebAppFactory(_db.ConnectionString, tools: true);
        var cashier = Client(f);
        await cashier.GetAsync("/demo/switch-role?role=cashier&returnUrl=%2Fme");
        var pos = await cashier.GetAsync("/pos/index");
        Assert.Equal(HttpStatusCode.OK, pos.StatusCode);
        var html = await pos.Content.ReadAsStringAsync();
        foreach (var text in new[] { "Hóa đơn tạm tính", "Tiền mặt <kbd>F4</kbd>", "Chuyển khoản QR", "Hủy hóa đơn <kbd>Esc</kbd>", "Chỉ bán bắp nước", "<kbd>F1</kbd>", "/js/realtime.js", "Hủy hóa đơn đã thanh toán" })
            Assert.Contains(text, html);
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/pos/my-bills")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/pos/close-shift")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await cashier.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await cashier.GetAsync("/admin/bills")).StatusCode);

        var gate = Client(f);
        await gate.GetAsync("/demo/switch-role?role=gate&returnUrl=%2Fme");
        Assert.Equal(HttpStatusCode.Redirect, (await gate.GetAsync("/pos/index")).StatusCode);

        var mgr = Client(f);
        await mgr.GetAsync("/demo/switch-role?role=manager&returnUrl=%2Fme");
        var pages = new Dictionary<string, string>
        {
            ["/admin"] = "Tỷ lệ phim Việt", ["/admin/bills"] = "Xuất CSV", ["/admin/reports"] = "chart-top-drinks", ["/admin/reports?tab=vietnamese"] = "share-mark",
            ["/admin/reports?tab=occupancy"] = "Tỷ lệ lấp đầy", ["/admin/reports?tab=shifts"] = "Biên bản ca", ["/admin/reports/settlement"] = "Chọn phim",
        };
        foreach (var (url, text) in pages)
        {
            var res = await mgr.GetAsync(url);
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"{url} trả {(int)res.StatusCode}");
            Assert.Contains(text, await res.Content.ReadAsStringAsync());
        }
        var csv = await mgr.GetAsync("/admin/reports/export?tab=revenue&from=2026-11-01&to=2026-11-06");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Doanh thu vé", await csv.Content.ReadAsStringAsync());
        var billId = FPolyCinema.Web.DAL.BillDAL.GetAll().First(b => b.Status == BillStatus.Finish).Id;
        var detail = await (await mgr.GetAsync($"/admin/bills/{billId}")).Content.ReadAsStringAsync();
        Assert.Contains("Lịch sử thanh toán, hoàn tiền và phê duyệt", detail);
        Assert.Contains("In lại hóa đơn", detail);
    }

    [Fact]
    public async Task Ajax_AntiForgeryRequired_VoidNeedsApproval_HubNegotiates()
    {
        using var f = new WebAppFactory(_db.ConnectionString, tools: true);
        var c = Client(f);
        await c.GetAsync("/demo/switch-role?role=cashier&returnUrl=%2Fme");
        var noToken = await c.SendAsync(Ajax("/pos/init", new(), null));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var token = await TokenFrom(c, "/pos/my-bills");
        var billId = FPolyCinema.Web.DAL.BillDAL.GetAll().First(b => b.Status == BillStatus.Finish && b.Channel == BillChannel.Pos).Id;
        var res = await c.SendAsync(Ajax("/pos/request-void", new() { ["billId"] = billId.ToString(), ["reason"] = "Khách đổi ý trước giờ chiếu" }, token));
        Assert.Equal(422, (int)res.StatusCode);
        var err = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("error");
        Assert.Equal(ErrorCodes.ApprovalRequired, err.GetProperty("code").GetString());

        var neg = await f.CreateClient().PostAsync("/hubs/cinema/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, neg.StatusCode);
        Assert.Contains("connectionToken", await neg.Content.ReadAsStringAsync());
    }
}
