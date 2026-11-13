using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;

namespace FPolyCinema.Web.Background;

/// <summary>
/// Bộ quét giữ ghế (SPEC 3.5, lớp giải phóng thứ nhất): chạy mỗi HoldSweepSeconds, trả các ghế HELD đã hết hạn về AVAILABLE, hủy hóa đơn waiting
/// hết hạn (EXPIRED, tác nhân System) và phát SEAT_MAP_UPDATED. Lớp thứ hai là mọi truy vấn đọc sơ đồ luôn coi ghế HELD hết hạn là trống,
/// nên ghế không bị treo kể cả khi bộ quét chưa kịp chạy.
/// </summary>
public class SeatHoldSweeper : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly CinemaRules _rules;
    private readonly ILogger<SeatHoldSweeper> _log;

    public SeatHoldSweeper(IServiceScopeFactory scopes, CinemaRules rules, ILogger<SeatHoldSweeper> log)
    {
        _scopes = scopes;
        _rules = rules;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromSeconds(Math.Max(1, _rules.HoldSweepSeconds));
        using var timer = new PeriodicTimer(period);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<SeatHoldService>().SweepExpired();
                if (n > 0) _log.LogInformation("Bộ quét giữ ghế đã hủy {Count} hóa đơn hết hạn.", n);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Bộ quét giữ ghế gặp lỗi, sẽ chạy lại ở lượt sau.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
