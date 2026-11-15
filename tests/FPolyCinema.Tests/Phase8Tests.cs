using System.Net;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Infrastructure.Seed;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Tests;

/// <summary>Quy tắc thuần của trung tâm demo (SPEC 5, UI D4): tên miền phụ, chia đôi màn hình, chọn suất cho kịch bản, vé mẫu dùng được.</summary>
public class Phase8RulesTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 19, 0, 0);

    [Fact]
    public void WindowOrigin_UsesLocalhostSubdomainsWithSamePort()
    {
        Assert.Equal("http://cashier.localhost:5080", DemoScreenRules.WindowOrigin("http", "localhost", 5080, DemoScreenRules.Cashier));
        Assert.Equal("http://khach-b.localhost:5083", DemoScreenRules.WindowOrigin("http", "gate.localhost", 5083, DemoScreenRules.KhachB));
        Assert.Equal("https://manager.localhost", DemoScreenRules.WindowOrigin("https", "localhost", null, DemoScreenRules.Manager));
        // Không chạy ở localhost thì không tách được phiên: giữ nguyên host
        Assert.Equal("http://127.0.0.1:5080", DemoScreenRules.WindowOrigin("http", "127.0.0.1", 5080, DemoScreenRules.Gate));
        Assert.Throws<ArgumentException>(() => DemoScreenRules.WindowOrigin("http", "localhost", 5080, "admin"));
        Assert.Equal(new[] { "cashier", "manager", "gate", "khach-a", "khach-b" }, DemoScreenRules.Subdomains);
    }

    [Fact]
    public void IsLocalHost_AcceptsLocalhostAndSubdomainsOnly()
    {
        Assert.True(DemoScreenRules.IsLocalHost("localhost"));
        Assert.True(DemoScreenRules.IsLocalHost("Khach-A.Localhost"));
        Assert.False(DemoScreenRules.IsLocalHost("127.0.0.1"));
        Assert.False(DemoScreenRules.IsLocalHost("fpolycinema.vn"));
        Assert.False(DemoScreenRules.IsLocalHost("notlocalhost"));
        Assert.False(DemoScreenRules.IsLocalHost(null));
    }

    [Fact]
    public void SwitchRolePath_EncodesRoleAndReturnUrl()
    {
        Assert.Equal("/demo/switch-role?role=khach2&returnUrl=%2Fbooking%2F12", DemoScreenRules.SwitchRolePath("khach2", "/booking/12"));
        Assert.Equal("/demo/switch-role?role=cashier&returnUrl=%2Fpos%2Fcustomer-display%3Fterminal%3DPOS-01%26cinemaId%3D1",
            DemoScreenRules.SwitchRolePath("cashier", "/pos/customer-display?terminal=POS-01&cinemaId=1"));
    }

    [Fact]
    public void Slots_SplitScreenInHalves()
    {
        Assert.Empty(DemoScreenRules.Slots(0));
        Assert.Equal(new WindowSlot("full", 0, 0, 1, 1), Assert.Single(DemoScreenRules.Slots(1)));
        var two = DemoScreenRules.Slots(2);
        Assert.Equal(new[] { "left", "right" }, two.Select(s => s.Name));
        Assert.Equal(new WindowSlot("left", 0, 0, 0.5, 1), two[0]);
        Assert.Equal(new WindowSlot("right", 0.5, 0, 0.5, 1), two[1]);
        var three = DemoScreenRules.Slots(3);
        Assert.Equal(new[] { "left", "right-top", "right-bottom" }, three.Select(s => s.Name));
        Assert.Equal(new WindowSlot("right-bottom", 0.5, 0.5, 0.5, 0.5), three[2]);
        // Cửa sổ không chồng nhau và phủ kín màn hình
        Assert.Equal(1.0, three.Sum(s => s.Width * s.Height), 6);
        var four = DemoScreenRules.Slots(4);
        Assert.Equal(1.0, four.Sum(s => s.Width * s.Height), 6);
        Assert.Equal(new[] { "left", "right-top", "right-2", "right-bottom" }, four.Select(s => s.Name));
    }

    private static DemoShowtimeOption Opt(int id, int startMin, string status = OperationalStatus.Scheduled, int free = 50, bool open = true) =>
        new(id, Now.AddMinutes(startMin), Now.AddMinutes(startMin + 15), Now.AddMinutes(startMin + 135), status, free, open);

    [Fact]
    public void PickContest_PrefersEarliestOpenShowtimeAtLeast30MinutesAhead()
    {
        var picked = DemoScreenRules.PickContest(new[] { Opt(1, 10), Opt(2, 90), Opt(3, 45), Opt(4, 40, free: 3), Opt(5, 35, open: false) }, Now);
        Assert.Equal(3, picked!.Id);
        // Không có suất đủ xa thì lấy suất còn bán sớm nhất
        Assert.Equal(1, DemoScreenRules.PickContest(new[] { Opt(1, 10), Opt(2, 20) }, Now)!.Id);
        Assert.Null(DemoScreenRules.PickContest(new[] { Opt(1, 60, open: false), Opt(2, 60, free: 2) }, Now));
    }

    [Fact]
    public void PickEntry_PrefersBoardingThenImHereWindowThenEarliest()
    {
        var all = new[] { Opt(1, 200), Opt(2, 50), Opt(3, 5, OperationalStatus.Boarding), Opt(4, -5, OperationalStatus.AdsPlaying, open: false) };
        Assert.Equal(3, DemoScreenRules.PickEntry(all, Now, 60)!.Id);
        Assert.Equal(2, DemoScreenRules.PickEntry(new[] { Opt(1, 200), Opt(2, 50) }, Now, 60)!.Id);
        Assert.Equal(1, DemoScreenRules.PickEntry(new[] { Opt(1, 200), Opt(2, 300) }, Now, 60)!.Id);
        Assert.Null(DemoScreenRules.PickEntry(new[] { Opt(1, 30, free: 1), Opt(2, 30, open: false) }, Now, 60));
    }

    [Fact]
    public void TicketUsable_NeedsPaidBillWithFnbNotEndedNotFullyCheckedIn()
    {
        DateTime start = Now.AddMinutes(30), end = Now.AddMinutes(150);
        Assert.True(DemoScreenRules.TicketUsable(BillStatus.Finish, FnbStatus.PendingPickup, BillCheckInStatus.NotCheckedIn, start, end, Now));
        Assert.True(DemoScreenRules.TicketUsable(BillStatus.Finish, FnbStatus.ReadyForPickup, BillCheckInStatus.PartiallyCheckedIn, start, end, Now));
        Assert.True(DemoScreenRules.TicketUsable(BillStatus.Finish, FnbStatus.PendingPickup, BillCheckInStatus.NotCheckedIn, Now.AddHours(5), Now.AddHours(7), Now));
        Assert.False(DemoScreenRules.TicketUsable(BillStatus.Cancel, FnbStatus.PendingPickup, BillCheckInStatus.NotCheckedIn, start, end, Now));
        Assert.False(DemoScreenRules.TicketUsable(BillStatus.Finish, FnbStatus.None, BillCheckInStatus.NotCheckedIn, start, end, Now));
        Assert.False(DemoScreenRules.TicketUsable(BillStatus.Finish, FnbStatus.Voided, BillCheckInStatus.NotCheckedIn, start, end, Now));
        Assert.False(DemoScreenRules.TicketUsable(BillStatus.Finish, FnbStatus.PendingPickup, BillCheckInStatus.FullyCheckedIn, start, end, Now));
        Assert.False(DemoScreenRules.TicketUsable(BillStatus.Finish, FnbStatus.PendingPickup, BillCheckInStatus.NotCheckedIn, Now.AddHours(-3), Now.AddMinutes(-1), Now));
    }

    [Fact]
    public void ReadyNow_FromImHereWindowStart()
    {
        Assert.True(DemoScreenRules.ReadyNow(Now.AddMinutes(60), Now, 60));
        Assert.True(DemoScreenRules.ReadyNow(Now.AddMinutes(-10), Now, 60));
        Assert.False(DemoScreenRules.ReadyNow(Now.AddMinutes(61), Now, 60));
    }

    [Fact]
    public void DemoAccounts_FindsSecondCustomerButBarKeepsFiveRoles()
    {
        Assert.Equal(5, DemoAccounts.All.Count);
        Assert.Equal(SeedCatalog.Khach2Email, DemoAccounts.Find("khach2")!.Email);
        Assert.DoesNotContain(DemoAccounts.All, a => a.Slug == "khach2");
    }
}

/// <summary>CSDL mẫu riêng cho test Phase 8 (vé mẫu ghi thêm hóa đơn).</summary>
public sealed class Phase8DatabaseFixture : IDisposable
{
    public static readonly DateTime Now = new(2026, 11, 6, 18, 40, 0);

    public Phase8DatabaseFixture()
    {
        ConnectionString = TestDatabase.NewConnectionString("P8Test");
        WebRoot = Path.Combine(Path.GetTempPath(), "fpc-p8-" + Guid.NewGuid().ToString("N"));
        DBUtil.Init(ConnectionString);
        new DbInitializer(ConnectionString, RepoPaths.File("Database"), new FakeClock(Now), NullLogger.Instance).Initialize(resetDb: true);
        new DbSeeder(new FakeClock(Now), new CinemaRules(), WebRoot, NullLogger.Instance).SeedIfEmpty();
    }

    public string ConnectionString { get; }
    public string WebRoot { get; }

    public void Dispose()
    {
        TestDatabase.Drop(ConnectionString);
        try { Directory.Delete(WebRoot, true); } catch (IOException) { }
    }
}

/// <summary>Trung tâm demo trên CSDL mẫu: 5 kịch bản với địa chỉ cửa sổ và iframe đúng SPEC 5, tạo vé mẫu có bắp nước qua luồng đặt vé online.</summary>
[Collection("Database")]
public class Phase8ServiceTests : IClassFixture<Phase8DatabaseFixture>
{
    private readonly CinemaRules _rules = new();
    private readonly FakeClock _clock = new(Phase8DatabaseFixture.Now);
    private readonly RecordingHub _hub = new();

    public Phase8ServiceTests(Phase8DatabaseFixture f) => DBUtil.Init(f.ConnectionString);

    private RealtimeNotifier Notifier() => new(_hub, NullLogger<RealtimeNotifier>.Instance);
    private BillService Bills() => new(_clock, _rules, new BillStatusService(_clock), Notifier(), new InventoryService(), new LoyaltyService(_rules));
    private OnlineBookingService Online() => new(_clock, _rules, Bills(), new BillStatusService(_clock), new SeatHoldService(_clock, _rules, Bills(), Notifier()),
        new InventoryService(), new LoyaltyService(_rules), Notifier(), Options.Create(new PaymentOptions { WebhookSecret = "k" }));
    private DemoCenterService Center() => new(_clock, _rules, Online(), new SeatSuggestService(_clock, _rules));

    [Fact]
    public void Build_FiveScenariosWithSubdomainWindowsAndIframes()
    {
        var c = Center().Build("http", "localhost", 5080);
        Assert.True(c.SeparateSessions);
        Assert.Equal(5, c.Scenarios.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, c.Scenarios.Select(s => s.No));
        Assert.All(c.Scenarios, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Description));
            Assert.NotEmpty(s.Steps);
            Assert.True(s.Windows.Count >= 2);
            Assert.All(s.Windows, w => Assert.NotNull(w.Slot));
            Assert.Equal("left", s.Windows[0].Slot!.Name);
        });
        // Mọi cửa sổ cần đăng nhập đều mở trên tên miền phụ và tự đăng nhập bằng /demo/switch-role
        var login = c.Scenarios.SelectMany(s => s.Windows).Where(w => w.Account != "Không cần đăng nhập").ToList();
        Assert.All(login, w => Assert.Matches(@"^http://(cashier|manager|gate|khach-a|khach-b)\.localhost:5080/demo/switch-role\?role=", w.Url));
        Assert.Equal(new[] { "cashier", "gate", "khach-a", "khach-b", "manager" },
            login.Select(w => w.Host!.Replace(".localhost", string.Empty)).Distinct().OrderBy(x => x));

        var s1 = c.Scenarios[0];
        Assert.Contains("role=khach&", s1.Windows[0].Url);
        Assert.Contains("role=khach2&", s1.Windows[1].Url);
        Assert.Equal(s1.Windows[0].Url.Split("returnUrl=")[1], s1.Windows[1].Url.Split("returnUrl=")[1]);
        Assert.Contains("%2Fbooking%2F", s1.Windows[0].Url);

        // Màn hình không cần đăng nhập nhúng iframe trong cửa sổ sân khấu ở host hiện tại
        Assert.StartsWith("/pos/customer-display?terminal=POS-01&cinemaId=", Assert.Single(c.Scenarios[1].Frames).Url);
        Assert.StartsWith("/lobby/pickup-board?cinemaId=", Assert.Single(c.Scenarios[2].Frames).Url);
        Assert.StartsWith("/lobby/digital-signage?cinemaId=", Assert.Single(c.Scenarios[3].Frames).Url);
        Assert.Empty(c.Scenarios[0].Frames);
        Assert.Empty(c.Scenarios[4].Frames);
        Assert.Equal("http://localhost:5080/demo/multi-screen/stage?scenario=2", c.Scenarios[1].Windows[1].Url);
        Assert.Contains("%2Fpos%2Ffnb-kds", c.Scenarios[2].Windows[1].Url);
        Assert.Contains("%2Fpos%2Fgate-inspector", c.Scenarios[3].Windows[0].Url);
        Assert.Contains("role=shift&returnUrl=%2Fops", c.Scenarios[4].Windows[1].Url);
        Assert.Contains("%2Fpos%2Fmy-bills", c.Scenarios[4].Windows[0].Url);
    }

    [Fact]
    public async Task PrepareTicket_CreatesPaidTicketWithComboAndScenariosUseIt()
    {
        var center = Center();
        var (code, start) = await center.PrepareTicket();
        var bill = BillDAL.GetByCode(code)!;
        Assert.Equal(BillStatus.Finish, bill.Status);
        Assert.Equal(FnbStatus.PendingPickup, bill.FnbStatus);
        Assert.Equal(2, TicketDAL.RowsByBill(bill.Id).Count);
        Assert.Contains(PaymentDAL.RowsByBill(bill.Id), p => p.Method == PaymentMethod.VietQrSim && p.Status == PaymentStatus.Succeeded);
        Assert.Equal(UserDAL.GetByEmail(SeedCatalog.KhachEmail)!.Id, bill.CustomerId);

        var c = center.Build("http", "localhost", 5080);
        Assert.Equal(code, c.TicketCode);
        Assert.Contains("%2Ftickets%2F" + code, c.Scenarios[2].Windows[0].Url);
        Assert.Contains("%2Ftickets%2F" + code, c.Scenarios[3].Windows[1].Url);
        Assert.Equal(DemoScreenRules.ReadyNow(start, Phase8DatabaseFixture.Now, _rules.ImHereWindowMinutesBeforeStart), c.TicketReady);
        Assert.True(c.TicketReady); // 18:40 còn suất trong 60 phút tới
        Assert.Null(c.Scenarios[2].Notice);
    }
}

/// <summary>HTTP: /demo/multi-screen chỉ có khi bật Demo:EnableTools, có 5 thẻ kịch bản; cửa sổ sân khấu có iframe; thanh demo có nút mở trung tâm demo.</summary>
[Collection("Database")]
public class Phase8WebTests
{
    private readonly SeededDatabaseFixture _db;

    public Phase8WebTests(SeededDatabaseFixture db)
    {
        _db = db;
        DBUtil.Init(db.ConnectionString);
    }

    private static HttpClient Client(WebAppFactory f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    [Fact]
    public async Task MultiScreen_OnlyWhenToolsEnabled()
    {
        using (var off = new WebAppFactory(_db.ConnectionString, false))
        {
            var c = Client(off);
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/demo/multi-screen")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/demo/multi-screen/stage?scenario=2")).StatusCode);
        }
        using var on = new WebAppFactory(_db.ConnectionString, true);
        var client = Client(on);
        var res = await client.GetAsync("/demo/multi-screen");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        for (int i = 1; i <= 5; i++) Assert.Contains($"id=\"kich-ban-{i}\"", html);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(html, "data-open-scenario=").Count);
        Assert.Contains("Mở các màn hình", html);
        Assert.Contains("cashier.localhost", html);
        Assert.Contains("href=\"/demo/multi-screen\"", html); // nút mở trung tâm demo trên thanh demo

        var stage = await client.GetAsync("/demo/multi-screen/stage?scenario=4");
        Assert.Equal(HttpStatusCode.OK, stage.StatusCode);
        var stageHtml = await stage.Content.ReadAsStringAsync();
        Assert.Contains("<iframe class=\"demo-frame\" src=\"/lobby/digital-signage?cinemaId=", stageHtml);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/demo/multi-screen/stage?scenario=1")).StatusCode);
    }

    [Fact]
    public async Task SwitchRole_SignsInSecondCustomerForSeatContest()
    {
        using var f = new WebAppFactory(_db.ConnectionString, true);
        var c = Client(f);
        var res = await c.GetAsync("/demo/switch-role?role=khach2&returnUrl=%2Faccount");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/account", res.Headers.Location!.OriginalString);
        var page = await (await c.GetAsync("/account")).Content.ReadAsStringAsync();
        Assert.Contains("Lê Hoàng Nam", page);
    }

    [Fact]
    public async Task Settings_ManagerSeesReadOnlyRules_CashierDenied()
    {
        using var f = new WebAppFactory(_db.ConnectionString, true);
        var manager = Client(f);
        await manager.GetAsync("/demo/switch-role?role=manager&returnUrl=%2Fadmin");
        var res = await manager.GetAsync("/admin/settings");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Hệ thống rạp", html);
        Assert.Contains("FPoly Cinema Hà Nội", html);
        Assert.Contains(nameof(CinemaRules.SeatHoldMinutes), html);
        Assert.Contains("CinemaRules__", html);
        Assert.DoesNotContain("<form", html.Substring(html.IndexOf("<main", StringComparison.Ordinal)).Split("</main>")[0]);
        Assert.Contains("href=\"/admin/settings\"", html); // mục sidebar đã mở

        var cashier = Client(f);
        await cashier.GetAsync("/demo/switch-role?role=cashier&returnUrl=%2Fpos");
        var denied = await cashier.GetAsync("/admin/settings");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/Home/AccessDenied", denied.Headers.Location!.OriginalString);
    }
}

/// <summary>Demo:ClockOffsetMinutes (CONTRACTS C7-09): lệch đồng hồ ứng dụng chỉ khi bật công cụ demo.</summary>
public class Phase8ClockTests
{
    [Fact]
    public void ClockOffset_AppliesOnlyWhenToolsEnabled()
    {
        Assert.Equal(TimeSpan.Zero, new DemoOptions { EnableTools = false, ClockOffsetMinutes = -300 }.EffectiveClockOffset);
        Assert.Equal(TimeSpan.FromMinutes(-300), new DemoOptions { EnableTools = true, ClockOffsetMinutes = -300 }.EffectiveClockOffset);
        var real = new SystemClock("Asia/Ho_Chi_Minh");
        var shifted = new SystemClock("Asia/Ho_Chi_Minh", TimeSpan.FromMinutes(-300));
        var diff = real.Now - shifted.Now;
        Assert.InRange(diff.TotalMinutes, 299.9, 300.1);
        Assert.Equal(TimeSpan.Zero, real.Offset);
    }
}

/// <summary>Trang Cấu hình rạp (UI A10): danh mục tham số CinemaRules đủ mọi khóa, định dạng giá trị, nhãn "Đã đổi"; trang chỉ quản lý rạp xem được.</summary>
public class Phase8SettingsTests
{
    [Fact]
    public void Catalog_ListsEveryCinemaRulesKeyExactlyOnce()
    {
        var props = typeof(CinemaRules).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.CanWrite).Select(p => p.Name).OrderBy(x => x).ToList();
        var keys = FPolyCinema.Web.Domain.Rules.CinemaRulesCatalog.Keys;
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal(props, keys.OrderBy(x => x).ToList());
        Assert.Equal(7, FPolyCinema.Web.Domain.Rules.CinemaRulesCatalog.Describe(new CinemaRules()).Count);
    }

    [Fact]
    public void Describe_FormatsValues_AndMarksOnlyChangedSettings()
    {
        var defaults = FPolyCinema.Web.Domain.Rules.CinemaRulesCatalog.Describe(new CinemaRules()).SelectMany(g => g.Settings).ToDictionary(s => s.Key);
        Assert.DoesNotContain(defaults.Values, s => s.Changed);
        Assert.Equal("10 phút", defaults[nameof(CinemaRules.SeatHoldMinutes)].Value);
        Assert.Equal("08:00", defaults[nameof(CinemaRules.OperatingDayStart)].Value);
        Assert.Equal("20%", defaults[nameof(CinemaRules.VietnameseFilmMinShare)].Value);
        Assert.Equal("8%", defaults[nameof(CinemaRules.TicketVatPercent)].Value);
        Assert.Equal("50.000 ₫", defaults[nameof(CinemaRules.CompensationVoucherAmount)].Value);
        Assert.Equal("Thành viên 5% · VIP 7% · VVIP 10%", defaults[nameof(CinemaRules.PointEarnPercent)].Value);
        Assert.Equal("75/100", defaults[nameof(CinemaRules.GoodClusterScore)].Value);
        Assert.Equal("20 giây", defaults[nameof(CinemaRules.HoldSweepSeconds)].Value);

        var changed = FPolyCinema.Web.Domain.Rules.CinemaRulesCatalog.Describe(new CinemaRules { SeatHoldMinutes = 12, VietnameseFilmTargetShare = 0.25m })
            .SelectMany(g => g.Settings).Where(s => s.Changed).ToList();
        Assert.Equal(2, changed.Count);
        var hold = changed.Single(s => s.Key == nameof(CinemaRules.SeatHoldMinutes));
        Assert.Equal("12 phút", hold.Value);
        Assert.Equal("10 phút", hold.DefaultValue);
        Assert.Equal("25%", changed.Single(s => s.Key == nameof(CinemaRules.VietnameseFilmTargetShare)).Value);
    }
}
