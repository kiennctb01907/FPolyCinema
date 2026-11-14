using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Tests;

/// <summary>CSDL mẫu riêng cho test Phase 7.</summary>
public sealed class Phase7DatabaseFixture : IDisposable
{
    public static readonly DateTime Now = new(2026, 11, 6, 10, 0, 0);

    public Phase7DatabaseFixture()
    {
        ConnectionString = TestDatabase.NewConnectionString("P7Test");
        WebRoot = Path.Combine(Path.GetTempPath(), "fpc-p7-" + Guid.NewGuid().ToString("N"));
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

/// <summary>
/// Test tích hợp Phase 7 trên CSDL mẫu: bộ công cụ của trợ lý đặt vé (SPEC 4.2-03 → 4.2-07), nút "Giữ ghế &amp; thanh toán ngay" (SPEC 4.2-26),
/// AiDemandScore lưu vào Movies, bộ xếp lịch và "Áp dụng" (SPEC 4.3-01 → 4.3-08), kiểm tra tức thì khi kéo thả (SPEC 4.3-10).
/// </summary>
[Collection("Database")]
public class Phase7ServiceTests : IClassFixture<Phase7DatabaseFixture>
{
    private readonly CinemaRules _rules = new();
    private readonly FakeClock _clock = new(Phase7DatabaseFixture.Now);
    private readonly RecordingHub _hub = new();

    public Phase7ServiceTests(Phase7DatabaseFixture f) => DBUtil.Init(f.ConnectionString);

    private BookingTools Tools() => new(_clock, _rules, new SeatSuggestService(_clock, _rules), new InventoryService());
    private RealtimeNotifier Notifier() => new(_hub, NullLogger<RealtimeNotifier>.Instance);
    private BillService Bills() => new(_clock, _rules, new BillStatusService(_clock), Notifier(), new InventoryService(), new LoyaltyService(_rules));
    private OnlineBookingService Online() => new(_clock, _rules, Bills(), new BillStatusService(_clock), new SeatHoldService(_clock, _rules, Bills(), Notifier()),
        new InventoryService(), new LoyaltyService(_rules), Notifier(), Options.Create(new PaymentOptions { WebhookSecret = "k" }));

    private BookingAgentService Agent(AgentSessionStore store)
    {
        var tools = Tools();
        var noKey = Options.Create(new LlmOptions());
        return new BookingAgentService(new LocalAgent(tools), new LlmAgent(new HttpClient(), noKey, tools), noKey, store, Online(), NullLogger<BookingAgentService>.Instance);
    }

    [Fact]
    public void Tools_SearchShowtimes_FiltersByDateTimeFormatAndSalesOpen()
    {
        var tools = Tools();
        var tomorrow = _clock.Now.Date.AddDays(1);
        var evening = tools.SearchShowtimes(new ShowtimeSearch(new[] { tomorrow }, new TimeSpan(18, 0, 0), NlpParser.DayEnd, CinemaId: 1, Quantity: 2));
        Assert.NotEmpty(evening);
        Assert.All(evening, s => Assert.True(s.StartTime.TimeOfDay >= new TimeSpan(18, 0, 0) && s.CinemaId == 1 && s.FreeSeats >= 2));
        Assert.All(evening, s => Assert.Equal(tomorrow, PricingCalculator.OperatingDate(s.StartTime)));
        var imax = tools.SearchShowtimes(new ShowtimeSearch(Array.Empty<DateTime>(), Format: AuditoriumFormat.IMAX));
        Assert.NotEmpty(imax);
        Assert.All(imax, s => Assert.Equal(AuditoriumFormat.IMAX, s.Format));
        // Không có ngày: từ bây giờ, chỉ suất còn bán online
        Assert.All(imax, s => Assert.True(s.StartTime > _clock.Now.AddMinutes(-_rules.AdsMinutes)));
        // Tên phim gần đúng, không dấu
        var title = MovieDAL.GetById(imax[0].MovieId)!.Title;
        Assert.All(tools.SearchShowtimes(new ShowtimeSearch(Array.Empty<DateTime>(), MovieQuery: NlpParser.Normalize(title))), s => Assert.Equal(imax[0].MovieId, s.MovieId));
    }

    [Fact]
    public void Tools_BestSeats_Combo_Rules_Preview()
    {
        var tools = Tools();
        var show = tools.SearchShowtimes(new ShowtimeSearch(new[] { _clock.Now.Date.AddDays(1) }, CinemaId: 1, Quantity: 3)).First();
        var pick = tools.GetBestSeats(show.ShowtimeId, 3, SeatPersona.BestViewAudio)!;
        Assert.Equal(3, pick.Seats.Count);
        Assert.All(pick.Seats, s => Assert.True(s.Price > 0 && s.Score > 0));

        var combo = tools.FindCombo("combo bap pho mai", 1, 1)!;
        Assert.Equal("Combo bắp phô mai", combo.Name);
        Assert.Equal("Nước suối", tools.FindCombo("nuoc suoi", 2, 1)!.Name);
        Assert.StartsWith("Combo", tools.FindCombo("combo", 1, 1)!.Name);
        Assert.Null(tools.FindCombo("pizza hải sản", 1, 1));

        // check_rules: phim từ T13 không dành cho trẻ em; phim K/P suất muộn có lời nhắc giờ chiếu cho trẻ em
        var all = tools.SearchShowtimes(new ShowtimeSearch(Array.Empty<DateTime>()));
        var adult = all.First(s => s.AgeRating is AgeRating.T16 or AgeRating.T18);
        Assert.False(tools.CheckRules(adult.ShowtimeId, 2, null, forChild: true).Ok);
        var ok = tools.CheckRules(adult.ShowtimeId, 2, null, forChild: false);
        Assert.True(ok.Ok);
        Assert.Contains(ok.Notes, n => n.Contains("xác nhận"));

        var card = tools.BuildPreview(show.ShowtimeId, pick.Seats.Select(s => s.SeatId).ToList(), new List<AgentItem> { combo }, pick.Persona, ok.Notes)!;
        Assert.Equal(pick.Seats.Sum(s => s.Price) + combo.Amount, card.Total);
        Assert.Equal(3, card.Map.SelectMany(r => r.Seats).Count(s => s.State == "selected"));
        Assert.Equal(pick.Range, card.Range);
        // Agent chỉ dựng bản xem trước: ghế vẫn trống
        Assert.All(ShowtimeSeatDAL.GetStates(show.ShowtimeId).Where(s => card.Seats.Any(c => c.SeatId == s.SeatId)), s => Assert.Equal(ShowtimeSeatStatus.Available, s.Status));
    }

    [Fact]
    public async Task Agent_HoldCard_HoldsSeatsViaSeatHoldService_AddsItems_GoesToPayment()
    {
        var store = new AgentSessionStore(new MemoryCache(new MemoryCacheOptions()));
        var agent = Agent(store);
        var reply = await agent.Ask("p7", "Mai 2 vé, ghế đẹp nhất kèm 2 nước suối", null, new AgentContext(null, 1));
        var card = reply.Card!;
        Assert.Equal(2, card.Seats.Count);
        var customer = UserDAL.GetByEmail("khach2@fpolycinema.vn")!;
        var (redirect, notes) = await agent.HoldCard("p7", customer.Id);
        Assert.Equal($"/booking/{card.ShowtimeId}?step=4", redirect);
        Assert.Empty(notes);
        var bill = BillDAL.GetWaitingOnline(customer.Id)!;
        var held = ShowtimeSeatDAL.GetStates(card.ShowtimeId).Where(s => s.HoldBillId == bill.Id).Select(s => s.SeatId).OrderBy(x => x).ToList();
        Assert.Equal(card.Seats.Select(s => s.SeatId).OrderBy(x => x), held);
        Assert.Equal(2, BillDetailDAL.GetByBill(bill.Id).Sum(l => l.Quantity));
        Assert.Null(store.Get("p7").Card);
        // Ghế vừa giữ không còn dựng được thẻ đặt vé
        Assert.Null(Tools().BuildPreview(card.ShowtimeId, card.Seats.Select(s => s.SeatId).ToList(), new List<AgentItem>(), card.Persona));
        await Online().CancelWaiting(customer.Id);
    }

    [Fact]
    public void Scheduler_GenerateSavesDemandScores_ApplyInsertsAiShowtimes_ValidateUsesPending()
    {
        var day = _clock.Now.Date.AddDays(10); // ngoài 7 ngày đã có lịch mẫu
        var ai = new AiSchedulerService(_rules);
        var choices = ai.Choices(1, day, 1);
        Assert.Contains(choices.Movies, m => !m.Eligible); // phim C hoặc thiếu KDM bị khóa kèm lý do
        Assert.All(choices.Movies.Where(m => !m.Eligible), m => Assert.False(string.IsNullOrEmpty(m.Reason)));
        var movieIds = choices.Movies.Where(m => m.Eligible).Select(m => m.Movie.Id).ToList();
        var (result, scores) = ai.Generate(1, day, 1, movieIds, choices.Rooms.Select(r => r.Id).ToList(), _clock.Now);
        Assert.NotEmpty(result.Drafts);
        Assert.All(scores, kv => Assert.Equal(kv.Value, MovieDAL.GetById(kv.Key)!.AiDemandScore));
        Assert.True(VietnameseFilmQuota.Share(result.VietnameseCount, result.TotalCount) >= _rules.VietnameseFilmTargetShare);
        Assert.Equal(0, ShowtimeDAL.CountVietnameseShare(1, day, day).Total); // bản nháp chưa lưu

        var schedule = new ScheduleService(_rules);
        // Kéo thả: suất nháp khác (chưa lưu) cũng chặn chồng giờ
        var first = result.Drafts[0];
        var pending = new List<ScheduleSlot> { AiScheduler.ToSlot(first) };
        var clash = schedule.Check(null, new ShowtimeInput(first.AuditoriumId, first.MovieId, first.StartTime.AddMinutes(10), first.LanguageFormat), _clock.Now, pending);
        Assert.True(clash.Result.Has(ErrorCodes.ShowtimeOverlap));

        var ids = schedule.ApplyAiDrafts(result.Drafts.Select(d => new ShowtimeInput(d.AuditoriumId, d.MovieId, d.StartTime, d.LanguageFormat)).ToList(), 1, _clock.Now);
        Assert.Equal(result.Drafts.Count, ids.Count);
        Assert.All(ids, id => Assert.True(ShowtimeDAL.GetById(id)!.IsAiGenerated));
        Assert.True(ShowtimeSeatDAL.GetStates(ids[0]).Count > 0);
        // Áp dụng lần nữa: chồng giờ với chính các suất vừa lưu, không lưu suất nào
        var again = Assert.Throws<BusinessRuleException>(() => schedule.ApplyAiDrafts(new List<ShowtimeInput> { new(first.AuditoriumId, first.MovieId, first.StartTime, first.LanguageFormat) }, 1, _clock.Now));
        Assert.Equal(ErrorCodes.ShowtimeOverlap, again.Code);
        // Lần chạy sau không động vào suất đã có
        var (next, _) = ai.Generate(1, day, 1, movieIds, choices.Rooms.Select(r => r.Id).ToList(), _clock.Now);
        Assert.All(next.Drafts, d => Assert.DoesNotContain(result.Drafts, x => x.AuditoriumId == d.AuditoriumId && x.StartTime < d.CleaningEndTime && d.StartTime < x.CleaningEndTime));
    }

    [Fact]
    public void Scheduler_RejectsPastDayAndTooManyDays()
    {
        var ai = new AiSchedulerService(_rules);
        Assert.Equal(ErrorCodes.ValidationFailed, Assert.Throws<BusinessRuleException>(() => ai.Generate(1, _clock.Now.Date.AddDays(-1), 1, new List<int> { 1 }, new List<int> { 1 }, _clock.Now)).Code);
        Assert.Equal(ErrorCodes.ValidationFailed, Assert.Throws<BusinessRuleException>(() => ai.Generate(1, _clock.Now.Date, 8, new List<int> { 1 }, new List<int> { 1 }, _clock.Now)).Code);
    }
}
