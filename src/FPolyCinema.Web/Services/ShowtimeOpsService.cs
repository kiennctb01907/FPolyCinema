using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Một suất vừa đổi trạng thái vận hành.</summary>
public sealed record OpsChange(int ShowtimeId, int CinemaId, int AuditoriumId, string From, string To);

/// <summary>
/// Vận hành phòng chiếu (SPEC 3.3, 3.8): ShowtimeClockService gọi Tick mỗi 30 giây để chuyển OperationalStatus theo giờ (ShowtimeOpsRules);
/// nút "Đã dọn xong" (GateStaff) chuyển CLEANING_IN_PROGRESS sang COMPLETED, nhờ đó suất kế tiếp trong phòng nhảy thẳng tới trạng thái đúng giờ.
/// Mỗi lần đổi trạng thái phát SHOWTIME_OPS_CHANGED tới cinema_ops_{cinemaId}.
/// </summary>
public class ShowtimeOpsService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly RealtimeNotifier _notifier;

    public ShowtimeOpsService(IClock clock, CinemaRules rules, RealtimeNotifier notifier)
    {
        _clock = clock;
        _rules = rules;
        _notifier = notifier;
    }

    /// <summary>Đầu ngày vận hành hiện tại (06:00 của ngày vận hành).</summary>
    private DateTime OperatingDayStart(DateTime now) => PricingCalculator.OperatingDate(now).Add(PricingCalculator.NightCutoff);

    /// <summary>
    /// Một lượt cập nhật: suất của ngày vận hành đã qua mà chưa dọn xong coi như đã dọn cuối ngày (COMPLETED); các suất trong cửa sổ tính lại
    /// theo giờ và theo phòng đã sẵn sàng hay chưa. Trả các suất đã đổi (đã phát sự kiện).
    /// </summary>
    public async Task<List<OpsChange>> Tick()
    {
        var now = _clock.Now;
        var changes = new List<OpsChange>();
        var dayStart = OperatingDayStart(now);

        foreach (var s in ShowtimeDAL.UnfinishedBefore(dayStart).Where(s => ShowtimeOpsRules.IsStaleDay(s.StartTime, now)))
            if (ShowtimeDAL.SetOperationalStatus(s.Id, s.OperationalStatus, OperationalStatus.Completed) == 1)
                changes.Add(new OpsChange(s.Id, s.CinemaId, s.AuditoriumId, s.OperationalStatus, OperationalStatus.Completed));

        var rows = ShowtimeDAL.ForClock(dayStart.AddHours(-6), now.AddMinutes(_rules.BoardingLeadMinutes + 1));
        foreach (var room in rows.GroupBy(r => r.AuditoriumId))
        {
            string? previous = null;
            foreach (var s in room.OrderBy(r => r.StartTime))
            {
                var current = s.OperationalStatus;
                var next = ShowtimeOpsRules.Next(current, new ShowtimeClock(s.StartTime, s.FeatureStartTime, s.EndTime), now, ShowtimeOpsRules.IsRoomReady(previous), _rules);
                if (next != current && ShowtimeDAL.SetOperationalStatus(s.Id, current, next) == 1)
                {
                    changes.Add(new OpsChange(s.Id, s.CinemaId, s.AuditoriumId, current, next));
                    current = next;
                }
                if (current != OperationalStatus.Cancelled) previous = current;
            }
        }
        foreach (var c in changes) await _notifier.ShowtimeOpsChanged(c.CinemaId, c.ShowtimeId, c.To);
        return changes;
    }

    /// <summary>
    /// Nút "Đã dọn xong" (GateStaff): suất đang CLEANING_IN_PROGRESS chuyển COMPLETED; sau đó tính lại ngay để suất kế tiếp trong phòng chuyển BOARDING
    /// (bảng sảnh hiện "Mời vào rạp"). cinemaId là rạp của nhân viên (null với quản lý toàn chuỗi).
    /// </summary>
    public async Task<List<OpsChange>> MarkCleaned(int showtimeId, int? cinemaId)
    {
        var info = SeatMapService.Info(showtimeId) ?? throw CategoryService.NotFound("Không tìm thấy suất chiếu.");
        if (cinemaId != null && info.Auditorium.CinemaId != cinemaId) throw FieldErrors.Of("showtimeId", "Suất này thuộc rạp khác.");
        if (!ShowtimeOpsRules.CanMarkCleaned(info.Showtime.OperationalStatus))
            throw new BusinessRuleException(ErrorCodes.InvalidTransition, info.Showtime.OperationalStatus == OperationalStatus.Completed
                ? $"{info.Auditorium.Name} đã được xác nhận dọn xong."
                : $"Suất {Fmt.Time(info.Showtime.StartTime)} ở {info.Auditorium.Name} chưa tới giờ dọn phòng.");
        if (ShowtimeDAL.SetOperationalStatus(showtimeId, OperationalStatus.CleaningInProgress, OperationalStatus.Completed) != 1)
            throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Trạng thái suất vừa thay đổi. Bạn tải lại màn hình.");
        var changes = new List<OpsChange> { new(showtimeId, info.Auditorium.CinemaId, info.Auditorium.Id, OperationalStatus.CleaningInProgress, OperationalStatus.Completed) };
        await _notifier.ShowtimeOpsChanged(info.Auditorium.CinemaId, showtimeId, OperationalStatus.Completed);
        changes.AddRange(await Tick());
        return changes;
    }

    /// <summary>Phòng của suất đã sẵn sàng chưa (suất trước trong phòng đã COMPLETED hoặc không có).</summary>
    public static bool RoomReady(int auditoriumId, DateTime startTime) => ShowtimeOpsRules.IsRoomReady(ShowtimeDAL.PreviousStatus(auditoriumId, startTime));

    /// <summary>Suất cần dọn của rạp (CLEANING_IN_PROGRESS), cho khung "Phòng đang dọn" ở máy soát vé.</summary>
    public List<LiveOccupancyRow> Cleaning(int? cinemaId) =>
        ShowtimeDAL.Operations(cinemaId, OperatingDayStart(_clock.Now).AddHours(-6), _clock.Now.AddHours(1))
            .Where(s => s.OperationalStatus == OperationalStatus.CleaningInProgress).ToList();

    /// <summary>
    /// Dashboard vận hành (SPEC 3.8-12, 3.3-21): các suất đang chạy kèm sĩ số trực tiếp (đã vào trên đã bán), suất đang dọn, và suất trễ
    /// (phòng chưa sẵn sàng mà đã quá StartTime – báo đỏ).
    /// </summary>
    public OpsBoard Board(int? cinemaId)
    {
        var now = _clock.Now;
        var rows = ShowtimeDAL.Operations(cinemaId, OperatingDayStart(now).AddHours(-6), now.AddMinutes(_rules.BoardingLeadMinutes + 1));
        return new OpsBoard(
            rows.Where(r => ShowtimeOpsRules.IsRunning(r.OperationalStatus)).ToList(),
            rows.Where(r => r.OperationalStatus == OperationalStatus.CleaningInProgress).ToList(),
            rows.Where(r => ShowtimeOpsRules.IsLate(r.OperationalStatus, r.StartTime, now)).ToList(),
            now);
    }
}

/// <summary>Dữ liệu dashboard vận hành: suất đang chạy (kèm sĩ số), đang dọn, trễ.</summary>
public sealed record OpsBoard(List<LiveOccupancyRow> Running, List<LiveOccupancyRow> Cleaning, List<LiveOccupancyRow> Late, DateTime Now);
