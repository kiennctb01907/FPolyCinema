using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FPolyCinema.Web.Controllers;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FPolyCinema.Tests;

/// <summary>Test HTTP Phase 3: route và quyền của màn hình phim, nhà phát hành, phòng chiếu, bảng giá, lịch chiếu; AJAX kiểm tra suất và xem trước sơ đồ.</summary>
[Collection("Database")]
public class Phase3WebTests
{
    private readonly SeededDatabaseFixture _db;

    public Phase3WebTests(SeededDatabaseFixture db)
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

    private static HttpRequestMessage Ajax(string url, Dictionary<string, string> form, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        req.Headers.Add("Accept", "application/json");
        req.Headers.Add("RequestVerificationToken", token);
        return req;
    }

    [Fact]
    public void Controllers_Phase3PoliciesMatchContracts()
    {
        string? Policy(MemberInfo m) => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p != null);
        Assert.Equal(Policies.EmployeeOrManager, Policy(typeof(MoviesController)));
        foreach (var name in new[] { "Create", "Edit", "Delete", "DeleteCheck", "SetActive" })
            Assert.All(typeof(MoviesController).GetMethods().Where(m => m.Name == name), m => Assert.Equal(Policies.ManagerOnly, Policy(m)));
        foreach (var t in new[] { typeof(DistributorsController), typeof(AuditoriumsController), typeof(PricingController), typeof(SchedulerController) })
            Assert.Equal(Policies.ManagerOnly, Policy(t));
        Assert.Equal("scheduler", typeof(SchedulerController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public async Task ManagerPages_RenderVietnameseScreens_StaffOnlySeesMovies()
    {
        using var f = new WebAppFactory(_db.ConnectionString, tools: true);
        var c = Client(f);
        await c.GetAsync("/demo/switch-role?role=manager&returnUrl=%2Fme");
        var pages = new Dictionary<string, string>
        {
            ["/admin/movies"] = "Thêm phim", ["/admin/movies/1"] = "Phân loại và pháp lý", ["/admin/movies/1/edit"] = "ratio-2x3",
            ["/admin/movies/create"] = "data-upload-url=\"/File/Upload\"", ["/admin/distributors"] = "Thêm nhà phát hành",
            ["/admin/auditoriums"] = "Thiết kế sơ đồ", ["/admin/auditoriums/1/design"] = "Gợi ý ghế nội bộ",
            ["/admin/pricing"] = "Bảng giá 6 chiều", ["/admin/pricing/audiences"] = "Tối thiểu theo luật", ["/admin/pricing/holidays"] = "Thêm ngày lễ",
            ["/admin/pricing/try?startTime=2026-11-14T19:00&format=IMAX&seatType=VIP&blockbuster=true"] = "Giá vé",
            ["/scheduler?date=2026-11-07"] = "Hôm nay", ["/scheduler/create"] = "Kiểm tra lịch",
        };
        foreach (var (url, text) in pages)
        {
            var res = await c.GetAsync(url);
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"{url} trả {(int)res.StatusCode}");
            var html = await res.Content.ReadAsStringAsync();
            Assert.Contains(text, html);
            Assert.DoesNotContain(">Index<", html);
        }
        var design = await (await c.GetAsync("/admin/auditoriums/1/design")).Content.ReadAsStringAsync();
        foreach (var tool in new[] { "STANDARD", "VIP", "SWEETBOX", "WHEELCHAIR", "EMPTY", "HOUSE", "MAINT" }) Assert.Contains($"data-tool=\"{tool}\"", design);
        Assert.Contains("Tự đánh số", design);
        Assert.Contains("heat-toggle", design);
        var sched = await (await c.GetAsync("/scheduler?date=2026-11-07")).Content.ReadAsStringAsync();
        Assert.Contains("age-badge", sched);
        Assert.Contains("Đã bán", sched);

        var shift = Client(f);
        await shift.GetAsync("/demo/switch-role?role=shift&returnUrl=%2Fme");
        Assert.Equal(HttpStatusCode.OK, (await shift.GetAsync("/admin/movies")).StatusCode);
        Assert.DoesNotContain("Thêm phim", await (await shift.GetAsync("/admin/movies")).Content.ReadAsStringAsync());
        foreach (var url in new[] { "/admin/movies/create", "/admin/pricing", "/scheduler", "/admin/auditoriums", "/admin/distributors" })
            Assert.Equal(HttpStatusCode.Redirect, (await shift.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Ajax_SchedulerCheckAndDesignerPreview()
    {
        using var f = new WebAppFactory(_db.ConnectionString, tools: true);
        var c = Client(f);
        await c.GetAsync("/demo/switch-role?role=manager&returnUrl=%2Fme");
        var token = await TokenFrom(c, "/scheduler");
        var cMovie = MovieDAL.GetAll().Single(m => m.AgeRating == AgeRating.C);
        var aud = AuditoriumDAL.GetAll().First();
        var res = await c.SendAsync(Ajax("/scheduler/check", new() { ["auditoriumId"] = aud.Id.ToString(), ["movieId"] = cMovie.Id.ToString(), ["startTime"] = "2030-01-10T10:00", ["languageFormat"] = LanguageFormat.Subtitled }, token));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.True(data.GetProperty("hasErrors").GetBoolean());
        Assert.Contains(data.GetProperty("issues").EnumerateArray(), i => i.GetProperty("code").GetString() == ErrorCodes.RatingC);

        // Lưu suất phim C: 422 RATING_C kèm details.issues
        var save = await c.SendAsync(Ajax("/scheduler/save", new() { ["auditoriumId"] = aud.Id.ToString(), ["movieId"] = cMovie.Id.ToString(), ["startTime"] = "2030-01-10T10:00", ["languageFormat"] = LanguageFormat.Subtitled }, token));
        Assert.Equal((HttpStatusCode)422, save.StatusCode);
        var err = JsonDocument.Parse(await save.Content.ReadAsStringAsync()).RootElement.GetProperty("error");
        Assert.Equal(ErrorCodes.RatingC, err.GetProperty("code").GetString());
        Assert.True(err.GetProperty("details").GetProperty("issues").GetArrayLength() > 0);

        var layout = "{\"rows\":3,\"cols\":4,\"cells\":[{\"r\":1,\"c\":1,\"t\":\"STANDARD\"},{\"r\":1,\"c\":3,\"t\":\"VIP\"},{\"r\":3,\"c\":1,\"t\":\"SWEETBOX\"},{\"r\":3,\"c\":2,\"t\":\"SWEETBOX\"}]}";
        var prev = await c.SendAsync(Ajax($"/admin/auditoriums/{aud.Id}/preview", new() { ["layout"] = layout }, token));
        Assert.Equal(HttpStatusCode.OK, prev.StatusCode);
        var p = JsonDocument.Parse(await prev.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var seats = p.GetProperty("seats").EnumerateArray().ToList();
        Assert.Equal("A2", seats.Single(s => s.GetProperty("r").GetInt32() == 1 && s.GetProperty("c").GetInt32() == 3).GetProperty("label").GetString());
        Assert.All(seats, s => Assert.InRange(s.GetProperty("heat").GetInt32(), 1, 5));
        Assert.Equal(2, seats.Count(s => s.GetProperty("pair").ValueKind == JsonValueKind.Number));
        Assert.Equal(4, p.GetProperty("stats").GetProperty("total").GetInt32());
    }
}
