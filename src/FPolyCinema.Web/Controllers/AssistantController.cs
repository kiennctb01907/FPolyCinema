using System.Security.Cryptography;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// C7. Trợ lý đặt vé (UC13, SPEC 4.2): widget trên web khách gọi các API JSON dưới đây. Hội thoại gắn với cookie phiên fpc_agent
/// và chỉ nằm trong bộ nhớ máy chủ. Hỏi đáp không cần đăng nhập; nút "Giữ ghế &amp; thanh toán ngay" cần tài khoản khách hàng.
/// </summary>
[Authorize(Policy = Policies.CustomerOnly)]
[Route("assistant")]
public class AssistantController : AppControllerBase
{
    private readonly BookingAgentService _agent;
    private readonly CinemaContextService _cinema;

    public AssistantController(BookingAgentService agent, CinemaContextService cinema)
    {
        _agent = agent;
        _cinema = cinema;
    }

    /// <summary>Mã phiên hội thoại trong cookie fpc_agent; chưa có thì tạo mới.</summary>
    private string SessionId()
    {
        if (Request.Cookies.TryGetValue(AgentSessionStore.CookieName, out var id) && id.Length == 32 && id.All(Uri.IsHexDigit)) return id;
        id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        Response.Cookies.Append(AgentSessionStore.CookieName, id, new CookieOptions
        {
            HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps, MaxAge = AgentSessionStore.Idle * 4,
        });
        return id;
    }

    private AgentContext Context() => new(CurrentRole == Roles.Customer ? CurrentUserId : null, _cinema.CurrentCinemaId);

    /// <summary>Hội thoại hiện tại của phiên (mở lại widget sau khi chuyển trang): các lượt, thẻ đặt vé đang có, chip gợi ý.</summary>
    [AllowAnonymous]
    [HttpGet("state")]
    public IActionResult State()
    {
        var s = _agent.Session(SessionId());
        lock (s)
        {
            return ApiOk(new { history = s.History.ToList(), card = s.Card, chips = s.Card != null ? LocalAgent.CardChips : LocalAgent.StartChips });
        }
    }

    /// <summary>Gửi một câu cho trợ lý, hoặc chọn một suất trong danh sách gợi ý (showtimeId).</summary>
    [AllowAnonymous]
    [HttpPost("message")]
    public async Task<IActionResult> Message(string? text, int? showtimeId, CancellationToken ct) =>
        ApiOk(await _agent.Ask(SessionId(), text, showtimeId, Context(), ct));

    /// <summary>Bắt đầu lại hội thoại.</summary>
    [AllowAnonymous]
    [HttpPost("reset")]
    public IActionResult Reset()
    {
        _agent.Reset(SessionId());
        return ApiOk(new { chips = LocalAgent.StartChips });
    }

    /// <summary>"Giữ ghế &amp; thanh toán ngay": giữ ghế của thẻ đặt vé qua SeatHoldService rồi chuyển sang bước thanh toán.</summary>
    [HttpPost("hold")]
    public async Task<IActionResult> Hold()
    {
        var (redirect, notes) = await _agent.HoldCard(SessionId(), CurrentUserId);
        if (notes.Count > 0) Toast("warning", string.Join(" ", notes));
        return ApiOk(new { redirect });
    }
}
