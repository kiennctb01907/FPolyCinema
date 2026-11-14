using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Tests;

/// <summary>
/// Test luồng của trợ lý đặt vé (SPEC 4.2) với bộ công cụ giả (không cần DB): LocalAgent theo luồng cố định, hỏi lại một câu, câu nói tiếp
/// (thêm món, đổi suất, làm lại), LlmAgent theo chuẩn OpenAI Chat Completions có function calling và tự chuyển sang LocalAgent khi lỗi.
/// </summary>
public class BookingAgentTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 15, 0, 0); // thứ Năm
    private static readonly DateTime T0 = Now;

    /// <summary>Bộ công cụ giả: 4 suất của 2 phim, cụm ghế lấy theo thứ tự mã ghế.</summary>
    private sealed class FakeTools : IBookingTools
    {
        public readonly List<AgentShowtime> Shows = new()
        {
            new(101, 1, "Phim hành động", AgeRating.T13, 1, "Rạp 1", "Phòng 1", AuditoriumFormat.F2D, LanguageFormat.Subtitled, T0.AddHours(1), T0.AddHours(3), 100),
            new(102, 1, "Phim hành động", AgeRating.T13, 1, "Rạp 1", "Phòng 2", AuditoriumFormat.F2D, LanguageFormat.Subtitled, T0.AddHours(4), T0.AddHours(6), 100),
            new(103, 2, "Phim kinh dị", AgeRating.T18, 1, "Rạp 1", "Phòng 3", AuditoriumFormat.IMAX, LanguageFormat.Subtitled, T0.Date.AddDays(1).AddHours(19).AddMinutes(30), T0.Date.AddDays(1).AddHours(21), 50),
            new(104, 1, "Phim hành động", AgeRating.T13, 1, "Rạp 1", "Phòng 1", AuditoriumFormat.F2D, LanguageFormat.Subtitled, T0.Date.AddDays(2).AddHours(19).AddMinutes(15), T0.Date.AddDays(2).AddHours(21), 100),
        };
        public readonly Dictionary<int, string> Genres = new() { [1] = Genre.Action, [2] = Genre.Horror };
        public int Searches;
        public int Previews;

        public DateTime Now => BookingAgentTests.Now;
        public TimeSpan DayStart => new(8, 0, 0);

        public AgentCatalog Catalog(int? cinemaId) => new(
            new List<NlpCatalogItem> { new(1, "Phim hành động"), new(2, "Phim kinh dị") },
            new List<NlpCatalogItem> { new(13, "Nước suối"), new(15, "Combo bắp phô mai"), new(14, "Combo Solo") });

        public List<AgentShowtime> SearchShowtimes(ShowtimeSearch q)
        {
            Searches++;
            return Shows.Where(s =>
                    (q.Dates.Count == 0 || q.Dates.Contains(PricingCalculator.OperatingDate(s.StartTime)))
                    && (q.TimeFrom == null || s.StartTime.TimeOfDay >= q.TimeFrom) && (q.TimeTo == null || s.StartTime.TimeOfDay <= q.TimeTo)
                    && (q.Genre == null || Genres[s.MovieId] == q.Genre) && (q.Format == null || s.Format == q.Format)
                    && (q.MovieId == null || s.MovieId == q.MovieId) && (q.After == null || s.StartTime > q.After) && (q.Quantity == null || s.FreeSeats >= q.Quantity))
                .OrderBy(s => s.StartTime).ToList();
        }

        public AgentSeatPick? GetBestSeats(int showtimeId, int quantity, string? persona) =>
            new(showtimeId, persona ?? SeatPersona.BestViewAudio, Enumerable.Range(1, quantity).Select(i => new AgentSeat(showtimeId * 100 + i, "G" + i, SeatType.Standard, 90, 90_000)).ToList(),
                "G1–G" + quantity, 90);

        public AgentItem? FindCombo(string query, int quantity, int? cinemaId)
        {
            var key = NlpParser.Normalize(query);
            if (key == "combo") return new AgentItem(14, "Combo Solo", quantity, 79_000);
            return Catalog(null).Drinks.FirstOrDefault(d => NlpParser.Normalize(d.Name) == key) is { } d ? new AgentItem(d.Id, d.Name, quantity, d.Id == 13 ? 20_000 : 95_000) : null;
        }

        public AgentRuleCheck CheckRules(int showtimeId, int quantity, int? userId, bool forChild) => new(true, null, new List<string>());

        public AgentCard? BuildPreview(int showtimeId, IReadOnlyList<int> seatIds, IReadOnlyList<AgentItem> items, string? persona, IReadOnlyList<string>? notes = null)
        {
            Previews++;
            var s = Shows.First(x => x.ShowtimeId == showtimeId);
            var seats = seatIds.Select((id, i) => new AgentSeat(id, "G" + (i + 1), SeatType.Standard, 90, 90_000)).ToList();
            long tickets = seats.Sum(x => x.Price);
            return new AgentCard(showtimeId, s.MovieId, s.MovieTitle, s.AgeRating, "", "/img/p.svg", s.CinemaName, s.AuditoriumName, Fmt.WeekdayDate(s.StartTime),
                Fmt.Time(s.StartTime), s.Format, "Phụ đề", s.StartTime, persona ?? SeatPersona.BestViewAudio, seats.Count, seats, "G1", 90, items.ToList(), tickets,
                tickets + items.Sum(i => i.Amount), 10, new List<MiniRow>(), (notes ?? Array.Empty<string>()).ToList());
        }
    }

    private static readonly AgentContext Ctx = new(null, 1);

    [Fact]
    public void Agent_Example07_EarliestShowtime_FourSeatsNearAisle()
    {
        var tools = new FakeTools();
        var session = new AgentSession("s1");
        var r = new LocalAgent(tools).Handle(session, "Đặt 4 vé suất gần nhất, ghế gần lối đi", null, Ctx);
        Assert.NotNull(r.Card);
        Assert.Equal(101, r.Card!.ShowtimeId);
        Assert.Equal(4, r.Card.Seats.Count);
        Assert.Equal(SeatPersona.NearAisle, r.Card.Persona);
        Assert.Equal(LocalAgent.EngineName, r.Engine);
    }

    [Fact]
    public void Agent_Example08_FollowUpAddsItemsToCurrentCard()
    {
        var tools = new FakeTools();
        var agent = new LocalAgent(tools);
        var session = new AgentSession("s1");
        agent.Handle(session, "Đặt 2 vé suất gần nhất", null, Ctx);
        var r = agent.Handle(session, "Thêm 2 nước suối", null, Ctx);
        var item = Assert.Single(r.Card!.Items);
        Assert.Equal(13, item.DrinkId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(2 * 90_000 + 2 * 20_000, r.Card.Total);
        Assert.Equal(101, r.Card.ShowtimeId); // vẫn là thẻ hiện tại
    }

    [Fact]
    public void Agent_Example09_NextShowtimeOfSameMovie()
    {
        var tools = new FakeTools();
        var agent = new LocalAgent(tools);
        var session = new AgentSession("s1");
        agent.Handle(session, "Đặt 2 vé suất gần nhất kèm 1 combo bắp phô mai", null, Ctx);
        var r = agent.Handle(session, "Đổi sang suất sau đó", null, Ctx);
        Assert.Equal(102, r.Card!.ShowtimeId);
        Assert.Equal(1, r.Card.MovieId);
        Assert.Equal(2, r.Card.Quantity);
        Assert.Single(r.Card.Items); // giữ món đã chọn
        Assert.Equal(104, agent.Handle(session, "suất kế tiếp", null, Ctx).Card!.ShowtimeId); // bỏ qua suất phim khác
    }

    [Fact]
    public void Agent_Example10_ResetClearsCard()
    {
        var agent = new LocalAgent(new FakeTools());
        var session = new AgentSession("s1");
        agent.Handle(session, "Đặt 2 vé suất gần nhất", null, Ctx);
        Assert.NotNull(session.Card);
        var r = agent.Handle(session, "Thôi, làm lại", null, Ctx);
        Assert.Null(r.Card);
        Assert.Null(session.Card);
        Assert.Null(session.Criteria.Quantity);
    }

    [Fact]
    public void Agent_MissingInfo_AsksExactlyOneShortQuestion()
    {
        var agent = new LocalAgent(new FakeTools());
        var session = new AgentSession("s1");
        var r1 = agent.Handle(session, "Mình muốn xem phim hành động", null, Ctx);
        Assert.Null(r1.Card);
        Assert.Equal("Bạn muốn xem ngày nào và đi mấy người?", r1.Text);
        var r2 = agent.Handle(session, "Tối nay", null, Ctx);
        Assert.Equal("Bạn đi mấy người?", r2.Text);
        var r3 = agent.Handle(session, "3", null, Ctx);
        Assert.NotNull(r3.Card);
        Assert.Equal(102, r3.Card!.ShowtimeId); // tối nay (sau 18:00), phim hành động
        Assert.Equal(3, r3.Card.Quantity);
        Assert.All(new[] { r1.Text, r2.Text }, t => Assert.Equal(1, t.Count(ch => ch == '?')));
    }

    [Fact]
    public void Agent_NewFullRequest_StartsFreshCriteria_ShortFollowUpMerges()
    {
        var agent = new LocalAgent(new FakeTools());
        var session = new AgentSession("s1");
        agent.Handle(session, "Mai 8 giờ tối có phim kinh dị nào không", null, Ctx);
        Assert.Equal(2, session.Criteria.MovieId); // "phim kinh dị" trùng tên phim trong từ điển giả
        // Câu mới đủ ý: không giữ thể loại kinh dị và khung giờ của câu trước
        var r = agent.Handle(session, "Đặt 2 vé suất gần nhất", null, Ctx);
        Assert.Null(session.Criteria.MovieId);
        Assert.Null(session.Criteria.TimeFrom);
        Assert.Equal(101, r.Card!.ShowtimeId);
        // Câu ngắn bổ sung: giữ số người, đổi định dạng
        var r2 = agent.Handle(session, "IMAX thì sao", null, Ctx);
        Assert.Equal(103, r2.Card!.ShowtimeId);
        Assert.Equal(2, r2.Card.Quantity);
    }

    [Fact]
    public void Agent_Example04_SearchOnly_ListsShowtimesWithoutCard()
    {
        var tools = new FakeTools();
        var r = new LocalAgent(tools).Handle(new AgentSession("s1"), "Mai 8 giờ tối có phim kinh dị nào không", null, Ctx);
        Assert.Null(r.Card);
        Assert.Equal(0, tools.Previews);
        Assert.Equal(103, Assert.Single(r.Showtimes).ShowtimeId);
    }

    [Fact]
    public void Agent_PickShowtimeFromList_ThenBuildsCard()
    {
        var tools = new FakeTools();
        var agent = new LocalAgent(tools);
        var session = new AgentSession("s1");
        var list = agent.Handle(session, "Mai 8 giờ tối có phim kinh dị nào không", null, Ctx);
        session.LastShowtimes = list.Showtimes;
        var ask = agent.Handle(session, string.Empty, 103, Ctx);
        Assert.Equal("Bạn đi mấy người?", ask.Text);
        var r = agent.Handle(session, "2 người, gần màn hình", null, Ctx);
        Assert.Equal(103, r.Card!.ShowtimeId);
        Assert.Equal(SeatPersona.NearScreen, r.Card.Persona);
    }

    [Fact]
    public void Agent_NeverHoldsSeatsOrPays_ToolsHaveNoHoldDependency()
    {
        var forbidden = new[] { typeof(SeatHoldService), typeof(OnlineBookingService), typeof(BillService), typeof(BillStatusService) };
        foreach (var t in new[] { typeof(LocalAgent), typeof(LlmAgent), typeof(BookingTools) })
            foreach (var c in t.GetConstructors())
                Assert.DoesNotContain(c.GetParameters(), p => forbidden.Contains(p.ParameterType));
        Assert.DoesNotContain(typeof(IBookingTools).GetMethods(), m => m.Name.Contains("Hold") || m.Name.Contains("Pay") || m.Name.Contains("Checkout"));
        var names = LlmAgent.ToolDefinitions().Select(t => t!["function"]!["name"]!.GetValue<string>()).ToList();
        Assert.Equal(new[] { "search_showtimes", "get_best_seats", "find_combo", "check_rules", "build_preview" }, names);
    }

    [Fact]
    public void Agent_SessionKeptInMemoryPerSession()
    {
        var store = new AgentSessionStore(new MemoryCache(new MemoryCacheOptions()));
        var a = store.Get("a");
        a.Criteria.Quantity = 3;
        Assert.Same(a, store.Get("a"));
        Assert.Null(store.Get("b").Criteria.Quantity);
        store.Clear("a");
        Assert.Null(store.Get("a").Criteria.Quantity);
    }

    // ------------------------------------------------------------------ LlmAgent
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _steps;
        public readonly List<JsonNode> Requests = new();
        public string? Auth;

        public ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) => _steps = new(steps);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Auth = request.Headers.Authorization?.ToString();
            Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!);
            return _steps.Dequeue()(request);
        }
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string ToolCall(string name, string args) =>
        new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["role"] = "assistant", ["content"] = null,
                    ["tool_calls"] = new JsonArray(new JsonObject { ["id"] = "call_" + name, ["type"] = "function", ["function"] = new JsonObject { ["name"] = name, ["arguments"] = args } }),
                },
            }),
        }.ToJsonString();

    private static string Answer(string text) =>
        new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = text } }) }.ToJsonString();

    private static IOptions<LlmOptions> Llm() => Options.Create(new LlmOptions { BaseUrl = "http://llm.test/v1", Model = "test-model", ApiKey = "k-test" });

    [Fact]
    public async Task Llm_OpenAiChatCompletions_FunctionCalling_BuildsCard()
    {
        var handler = new ScriptedHandler(
            _ => Json(ToolCall("search_showtimes", "{\"date\":\"2026-11-12\",\"genre\":\"ACTION\"}")),
            _ => Json(ToolCall("build_preview", "{\"showtimeId\":101,\"seatIds\":[10101,10102],\"items\":[{\"drinkId\":13,\"quantity\":2}]}")),
            _ => Json(Answer("Mình đã dựng thẻ đặt vé suất 16:00.")));
        var tools = new FakeTools();
        var agent = new LlmAgent(new HttpClient(handler), Llm(), tools);
        var session = new AgentSession("s1");
        var r = await agent.Handle(session, "2 vé phim hành động hôm nay kèm 2 nước suối", null, Ctx);

        Assert.Equal(LlmAgent.EngineName, r.Engine);
        Assert.Equal(101, r.Card!.ShowtimeId);
        Assert.Equal(2, r.Card.Seats.Count);
        Assert.Equal(13, Assert.Single(r.Card.Items).DrinkId);
        Assert.Same(r.Card, session.Card);
        Assert.Equal("Bearer k-test", handler.Auth);
        var first = handler.Requests[0];
        Assert.Equal("test-model", first["model"]!.GetValue<string>());
        Assert.Equal(5, first["tools"]!.AsArray().Count);
        // Kết quả công cụ được gửi lại cho mô hình với role "tool"
        Assert.Contains(handler.Requests[1]["messages"]!.AsArray(), m => m!["role"]!.GetValue<string>() == "tool");
    }

    [Theory]
    [InlineData(0)]   // lỗi mạng
    [InlineData(429)] // hết hạn mức
    [InlineData(500)]
    public async Task Llm_NetworkErrorOrQuota_FallsBackToLocalAgent_CustomerSeesNoError(int status)
    {
        var handler = new ScriptedHandler(_ => status == 0 ? throw new HttpRequestException("mất mạng") : new HttpResponseMessage((HttpStatusCode)status));
        var tools = new FakeTools();
        var store = new AgentSessionStore(new MemoryCache(new MemoryCacheOptions()));
        var service = new BookingAgentService(new LocalAgent(tools), new LlmAgent(new HttpClient(handler), Llm(), tools), Llm(), store, null!,
            NullLogger<BookingAgentService>.Instance);
        var r = await service.Ask("s1", "Đặt 4 vé suất gần nhất, ghế gần lối đi", null, Ctx);
        Assert.Equal(LocalAgent.EngineName, r.Engine);
        Assert.Equal(101, r.Card!.ShowtimeId);
        Assert.Equal(2, store.Get("s1").History.Count);
    }

    [Fact]
    public async Task Service_WithoutApiKey_UsesLocalAgent()
    {
        var handler = new ScriptedHandler();
        var tools = new FakeTools();
        var noKey = Options.Create(new LlmOptions());
        var service = new BookingAgentService(new LocalAgent(tools), new LlmAgent(new HttpClient(handler), noKey, tools), noKey,
            new AgentSessionStore(new MemoryCache(new MemoryCacheOptions())), null!, NullLogger<BookingAgentService>.Instance);
        var r = await service.Ask("s1", "Cặp đôi muốn ghế đôi riêng tư tối nay", null, Ctx);
        Assert.Equal(LocalAgent.EngineName, r.Engine);
        Assert.Empty(handler.Requests);
        Assert.Equal(SeatPersona.PrivateCouple, r.Card!.Persona);
        Assert.Equal(2, r.Card.Quantity);
    }
}
