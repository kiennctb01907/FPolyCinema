using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Web.Services;

/// <summary>LlmAgent không dùng được (lỗi mạng, hết hạn mức, khóa sai, trả lời hỏng): BookingAgentService chuyển sang LocalAgent.</summary>
public class LlmUnavailableException : Exception
{
    public LlmUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// LlmAgent (SPEC 4.2-02): một client duy nhất theo chuẩn OpenAI Chat Completions có function calling (POST {Llm:BaseUrl}/chat/completions,
/// model Llm:Model, khóa Llm:ApiKey). Dùng được với OpenAI, Gemini qua endpoint tương thích OpenAI, hoặc Ollama chạy cục bộ.
/// Mô hình gọi cùng bộ công cụ với LocalAgent (search_showtimes, get_best_seats, find_combo, check_rules, build_preview); không có công cụ
/// giữ ghế hay thanh toán (SPEC 4.2-08). Mọi lỗi ném LlmUnavailableException để BookingAgentService tự chuyển sang LocalAgent.
/// </summary>
public class LlmAgent
{
    public const string EngineName = "llm";
    public const int MaxToolRounds = 6;

    private readonly HttpClient _http;
    private readonly LlmOptions _options;
    private readonly IBookingTools _tools;

    public LlmAgent(HttpClient http, IOptions<LlmOptions> options, IBookingTools tools)
    {
        _http = http;
        _options = options.Value;
        _tools = tools;
    }

    /// <summary>Định nghĩa 5 công cụ theo JSON Schema của function calling.</summary>
    public static JsonArray ToolDefinitions()
    {
        static JsonObject Fn(string name, string description, JsonObject properties, params string[] required) => new()
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray()) },
            },
        };
        static JsonObject P(string type, string description, params string[] values)
        {
            var o = new JsonObject { ["type"] = type, ["description"] = description };
            if (values.Length > 0) o["enum"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray());
            return o;
        }
        return new JsonArray(
            Fn("search_showtimes", "Tìm suất chiếu còn bán online.", new JsonObject
            {
                ["date"] = P("string", "Ngày yyyy-MM-dd; nhiều ngày cách nhau dấu phẩy; bỏ trống là 7 ngày tới."),
                ["timeFrom"] = P("string", "Giờ bắt đầu sớm nhất HH:mm."),
                ["timeTo"] = P("string", "Giờ bắt đầu muộn nhất HH:mm."),
                ["genre"] = P("string", "Thể loại.", Genre.All),
                ["languageFormat"] = P("string", "Bản ngôn ngữ.", LanguageFormat.All),
                ["format"] = P("string", "Định dạng phòng.", AuditoriumFormat.All),
                ["movieQuery"] = P("string", "Tên phim (gần đúng)."),
                ["cinemaId"] = P("integer", "Mã rạp."),
                ["quantity"] = P("integer", "Số ghế trống tối thiểu."),
            }),
            Fn("get_best_seats", "Cụm ghế liền nhau tốt nhất theo sở thích.", new JsonObject
            {
                ["showtimeId"] = P("integer", "Mã suất."),
                ["quantity"] = P("integer", "Số ghế."),
                ["persona"] = P("string", "Sở thích ghế.", SeatPersona.All),
            }, "showtimeId", "quantity"),
            Fn("find_combo", "Tìm món bắp nước đang bán.", new JsonObject
            {
                ["query"] = P("string", "Tên món, ví dụ 'combo bắp phô mai', 'nước suối'."),
                ["quantity"] = P("integer", "Số lượng."),
            }, "query"),
            Fn("check_rules", "Kiểm tra độ tuổi và giờ chiếu cho trẻ em của suất.", new JsonObject
            {
                ["showtimeId"] = P("integer", "Mã suất."),
                ["quantity"] = P("integer", "Số vé."),
            }, "showtimeId"),
            Fn("build_preview", "Dựng thẻ đặt vé để khách xem (không giữ ghế).", new JsonObject
            {
                ["showtimeId"] = P("integer", "Mã suất."),
                ["seatIds"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" }, ["description"] = "Mã ghế từ get_best_seats." },
                ["items"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = "Món từ find_combo.",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject { ["drinkId"] = P("integer", "Mã món."), ["quantity"] = P("integer", "Số lượng.") },
                    },
                },
                ["persona"] = P("string", "Sở thích ghế.", SeatPersona.All),
            }, "showtimeId", "seatIds"));
    }

    private string SystemPrompt(AgentContext ctx)
    {
        var now = _tools.Now;
        return "Bạn là trợ lý đặt vé của chuỗi rạp FPoly Cinema, trả lời ngắn gọn bằng tiếng Việt. " +
               $"Bây giờ là {now.ToString("dddd dd/MM/yyyy HH:mm", Fmt.Vi)} (giờ Việt Nam). " +
               (ctx.CinemaId is int c ? $"Khách đang chọn rạp có mã {c}. " : string.Empty) +
               "Quy trình: search_showtimes → check_rules → get_best_seats → find_combo (nếu khách muốn bắp nước) → build_preview. " +
               "Thiếu ngày hoặc số người thì hỏi lại đúng một câu ngắn. Bạn không giữ ghế và không thanh toán; khách tự bấm nút \"Giữ ghế & thanh toán ngay\" trên thẻ đặt vé. " +
               "Sở thích ghế: giữa phòng, đẹp nhất là BEST_VIEW_AUDIO; gần màn hình, bị cận là NEAR_SCREEN; riêng tư, ghế đôi là PRIVATE_COUPLE; gần lối đi là NEAR_AISLE. " +
               "Buổi sáng 08:00–12:00, chiều 12:00–18:00, tối 18:00–23:59; \"8 giờ tối\" là 19:00–21:00.";
    }

    public async Task<AgentReply> Handle(AgentSession session, string? text, int? pickShowtimeId, AgentContext ctx, CancellationToken ct = default)
    {
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = SystemPrompt(ctx) } };
        foreach (var t in session.History.TakeLast(12)) messages.Add(new JsonObject { ["role"] = t.Role, ["content"] = t.Text });
        var userText = text ?? string.Empty;
        if (pickShowtimeId is int pick) userText = $"Tôi chọn suất có showtimeId = {pick}. {userText}".Trim();
        if (session.Card != null) userText += $"\n(Thẻ đặt vé hiện tại: showtimeId {session.Card.ShowtimeId}, ghế {string.Join(",", session.Card.Seats.Select(s => s.SeatId))}, món {JsonSerializer.Serialize(session.Card.Items.Select(i => new { i.DrinkId, i.Quantity }))}.)";
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = userText });

        AgentCard? built = null;
        List<AgentShowtime> found = new();
        for (int round = 0; round < MaxToolRounds; round++)
        {
            var msg = await Complete(messages, ct);
            var calls = msg["tool_calls"] as JsonArray;
            if (calls == null || calls.Count == 0)
            {
                var content = msg["content"] is JsonValue cv && cv.TryGetValue<string>(out var reply) ? reply.Trim() : null;
                if (string.IsNullOrEmpty(content)) throw new LlmUnavailableException("Mô hình không trả lời nội dung.");
                if (built != null) session.Card = built;
                var card = built;
                return new AgentReply(content, card, card == null ? found.Take(6).ToList() : new List<AgentShowtime>(),
                    card != null ? LocalAgent.CardChips : LocalAgent.StartChips, EngineName);
            }
            messages.Add(msg.DeepClone());
            foreach (var call in calls)
            {
                var id = call?["id"]?.GetValue<string>() ?? string.Empty;
                var name = call?["function"]?["name"]?.GetValue<string>() ?? string.Empty;
                JsonObject args;
                try { args = JsonNode.Parse(call?["function"]?["arguments"]?.GetValue<string>() ?? "{}") as JsonObject ?? new JsonObject(); }
                catch (JsonException) { args = new JsonObject(); }
                var result = Execute(name, args, ctx, ref built, found);
                messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = id, ["content"] = JsonSerializer.Serialize(result) });
            }
        }
        throw new LlmUnavailableException("Mô hình gọi công cụ quá nhiều lượt.");
    }

    /// <summary>Thực hiện một lời gọi công cụ của mô hình bằng bộ công cụ chung.</summary>
    private object Execute(string name, JsonObject a, AgentContext ctx, ref AgentCard? built, List<AgentShowtime> found)
    {
        int? I(string k) => a[k] is JsonValue v && v.TryGetValue<int>(out var n) ? n : a[k] is JsonValue s && s.TryGetValue<string>(out var str) && int.TryParse(str, out var m) ? m : null;
        string? S(string k) => a[k] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;
        TimeSpan? T(string k) => TimeSpan.TryParseExact(S(k), "hh\\:mm", CultureInfo.InvariantCulture, out var t) ? t : null;
        switch (name)
        {
            case "search_showtimes":
            {
                var dates = (S("date") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(d => DateTime.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var x) ? x : (DateTime?)null)
                    .OfType<DateTime>().ToList();
                var list = _tools.SearchShowtimes(new ShowtimeSearch(dates, T("timeFrom"), T("timeTo"), Valid(S("genre"), Genre.All), Valid(S("languageFormat"), LanguageFormat.All),
                    Valid(S("format"), AuditoriumFormat.All), null, S("movieQuery"), I("cinemaId") ?? ctx.CinemaId, I("quantity")));
                found.Clear();
                found.AddRange(list);
                return list.Take(15).Select(s => new { s.ShowtimeId, s.MovieTitle, s.AgeRating, s.CinemaName, s.AuditoriumName, s.Format, s.LanguageFormat,
                    start = s.StartTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), s.FreeSeats }).ToList();
            }
            case "get_best_seats":
            {
                var pick = _tools.GetBestSeats(I("showtimeId") ?? 0, Math.Clamp(I("quantity") ?? 2, 1, 10), Valid(S("persona"), SeatPersona.All));
                return pick == null ? new { error = "Không còn cụm ghế liền nhau phù hợp." } : new { pick.Persona, pick.Range, pick.Score, seats = pick.Seats };
            }
            case "find_combo":
            {
                var item = _tools.FindCombo(S("query") ?? string.Empty, Math.Clamp(I("quantity") ?? 1, 1, 20), ctx.CinemaId);
                return item == null ? new { error = "Không có món này hoặc món đang hết." } : new { item.DrinkId, item.Name, item.Quantity, item.Price };
            }
            case "check_rules":
            {
                var r = _tools.CheckRules(I("showtimeId") ?? 0, Math.Clamp(I("quantity") ?? 1, 1, 10), ctx.UserId, false);
                return new { r.Ok, r.Message, r.Notes };
            }
            case "build_preview":
            {
                var seatIds = (a["seatIds"] as JsonArray ?? new JsonArray()).Select(x => x is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0).Where(n => n > 0).ToList();
                var items = new List<AgentItem>();
                var drinks = _tools.Catalog(ctx.CinemaId).Drinks;
                foreach (var it in a["items"] as JsonArray ?? new JsonArray())
                {
                    var did = it?["drinkId"] is JsonValue dv && dv.TryGetValue<int>(out var d) ? d : 0;
                    var qty = it?["quantity"] is JsonValue qv && qv.TryGetValue<int>(out var q) ? q : 1;
                    var drink = drinks.FirstOrDefault(x => x.Id == did);
                    if (drink != null && _tools.FindCombo(drink.Name, qty, ctx.CinemaId) is AgentItem found1) items.Add(found1);
                }
                var showtimeId = I("showtimeId") ?? 0;
                var rules = _tools.CheckRules(showtimeId, Math.Max(seatIds.Count, 1), ctx.UserId, false);
                if (!rules.Ok) return new { error = rules.Message };
                var card = _tools.BuildPreview(showtimeId, seatIds, items, Valid(S("persona"), SeatPersona.All), rules.Notes);
                if (card == null) return new { error = "Không dựng được thẻ đặt vé: suất hoặc ghế không hợp lệ." };
                built = card;
                return new { ok = true, card.Title, card.Date, card.Time, card.Range, card.Score, card.Total };
            }
            default:
                return new { error = "Không có công cụ này." };
        }
    }

    private static string? Valid(string? value, string[] allowed) => value != null && allowed.Contains(value) ? value : null;

    /// <summary>Gọi /chat/completions một lần; trả message đầu tiên. Lỗi mạng, hết giờ, mã HTTP lỗi hay JSON hỏng đều ném LlmUnavailableException.</summary>
    private async Task<JsonObject> Complete(JsonArray messages, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["messages"] = messages.DeepClone(),
            ["tools"] = ToolDefinitions(),
            ["tool_choice"] = "auto",
            ["temperature"] = 0.2,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        try
        {
            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new LlmUnavailableException($"Máy chủ mô hình trả mã {(int)res.StatusCode}.");
            var msg = JsonNode.Parse(json)?["choices"]?[0]?["message"] as JsonObject;
            return msg ?? throw new LlmUnavailableException("Trả lời của mô hình không đúng định dạng.");
        }
        catch (LlmUnavailableException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            throw new LlmUnavailableException("Không gọi được mô hình.", ex);
        }
    }
}
