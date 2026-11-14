using FPolyCinema.Web.Services;

namespace FPolyCinema.Web.Background;

/// <summary>
/// ShowtimeClockService (SPEC 3.3): mỗi 30 giây cập nhật OperationalStatus của các suất theo giờ (ShowtimeOpsService.Tick) và phát
/// SHOWTIME_OPS_CHANGED; cùng lượt đó chuyển các yêu cầu duyệt từ xa quá hạn sang EXPIRED và báo về máy POS (SPEC 3.6-22).
/// </summary>
public class ShowtimeClockService : BackgroundService
{
    public static readonly TimeSpan Period = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ShowtimeClockService> _log;

    public ShowtimeClockService(IServiceScopeFactory scopes, ILogger<ShowtimeClockService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var changes = await scope.ServiceProvider.GetRequiredService<ShowtimeOpsService>().Tick();
                if (changes.Count > 0) _log.LogInformation("Đồng hồ suất chiếu đã đổi trạng thái {Count} suất.", changes.Count);
                await scope.ServiceProvider.GetRequiredService<ApprovalService>().ExpireOverdue();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Đồng hồ suất chiếu gặp lỗi, sẽ chạy lại ở lượt sau.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
