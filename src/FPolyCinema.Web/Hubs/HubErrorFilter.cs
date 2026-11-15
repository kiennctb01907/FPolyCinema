using Microsoft.AspNetCore.SignalR;

namespace FPolyCinema.Web.Hubs;

/// <summary>
/// Ghi log lỗi khi gọi hàm của CinemaHub. HubException là lời từ chối có chủ ý (không đủ quyền tham gia nhóm, dữ liệu không tồn tại), thường xảy ra
/// khi trang mở lâu rồi tự kết nối lại sau khi phiên đăng nhập đã đổi (đăng xuất, đổi vai trò ở tab khác, tạo lại CSDL), nên chỉ ghi một dòng cảnh báo,
/// không kèm stack trace. Lỗi khác ghi mức Error kèm exception. Exception vẫn được ném lại để client nhận lời từ chối (CONTRACTS mục 6).
/// Bộ ghi log mặc định của SignalR (DefaultHubDispatcher) được hạ xuống Critical trong appsettings.json để không ghi trùng.
/// </summary>
public sealed class HubErrorFilter : IHubFilter
{
    private readonly ILogger<HubErrorFilter> _logger;

    public HubErrorFilter(ILogger<HubErrorFilter> logger) => _logger = logger;

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(context);
        }
        catch (HubException ex)
        {
            var user = context.Context.User?.Identity?.IsAuthenticated == true ? context.Context.User.Identity!.Name ?? context.Context.UserIdentifier : "khách chưa đăng nhập";
            _logger.LogWarning("Từ chối {Method}({Args}) của {User}: {Message}",
                context.HubMethodName, string.Join(", ", context.HubMethodArguments), user, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chạy hàm {Method} của hub", context.HubMethodName);
            throw;
        }
    }
}
