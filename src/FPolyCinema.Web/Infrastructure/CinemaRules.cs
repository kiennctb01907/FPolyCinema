using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Quy tắc vận hành dùng chung cho mọi service (CONTRACTS mục 7, khóa CinemaRules).
/// Giá trị mặc định ở đây trùng với appsettings.json; mọi khóa ghi đè được bằng biến môi trường
/// dạng CinemaRules__TênKhóa (ví dụ CinemaRules__SeatHoldMinutes=12).
/// </summary>
public class CinemaRules
{
    public const string SectionName = "CinemaRules";

    /// <summary>Múi giờ Việt Nam. Trên Windows cũ không có tên IANA thì dùng "SE Asia Standard Time".</summary>
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";

    public int SeatHoldMinutes { get; set; } = 10;
    public int SeatHoldExtendMinutes { get; set; } = 5;
    public int HoldSweepSeconds { get; set; } = 20;
    public int OnlineSalesCutoffMinutes { get; set; } = 0;
    public int PosSalesCutoffMinutes { get; set; } = 15;

    public int AdsMinutes { get; set; } = 15;
    public int CreditsMinutes { get; set; } = 5;
    public int CleaningMinutes { get; set; } = 15;
    public int ScheduleRoundMinutes { get; set; } = 5;
    public int BoardingLeadMinutes { get; set; } = 10;

    /// <summary>Giờ mở cửa của ngày vận hành. Suất bắt đầu trước 06:00 thuộc ngày vận hành hôm trước.</summary>
    public TimeSpan OperatingDayStart { get; set; } = new(8, 0, 0);
    public TimeSpan LastShowStartLatest { get; set; } = new(23, 30, 0);
    public TimeSpan ChildU13LatestEnd { get; set; } = new(22, 0, 0);
    public TimeSpan ChildU16LatestEnd { get; set; } = new(23, 0, 0);

    public decimal VietnameseFilmMinShare { get; set; } = 0.20m;
    public decimal VietnameseFilmTargetShare { get; set; } = 0.22m;
    public TimeSpan VietnameseFilmPriorityStart { get; set; } = new(18, 0, 0);
    public TimeSpan VietnameseFilmPriorityEnd { get; set; } = new(22, 0, 0);

    public TimeSpan PriceSlotAfternoonStart { get; set; } = new(12, 0, 0);
    public TimeSpan PriceSlotGoldenStart { get; set; } = new(17, 0, 0);
    public TimeSpan PriceSlotLateStart { get; set; } = new(22, 0, 0);
    public int PriceRoundTo { get; set; } = 1000;

    public int MaxSameMinuteStarts { get; set; } = 2;
    public int MinStartStaggerMinutes { get; set; } = 10;
    public int HouseSeatsPerAuditorium { get; set; } = 4;
    public int HouseSeatReminderMinutes { get; set; } = 30;

    public int SelfCancelMinMinutesBeforeStart { get; set; } = 120;
    public int SelfCancelMaxPerMonth { get; set; } = 2;
    public int ImHereWindowMinutesBeforeStart { get; set; } = 60;
    public int CrossShowtimeWindowMinutes { get; set; } = 60;
    public int GoodClusterScore { get; set; } = 75;

    /// <summary>VAT vé (%). 10% giảm còn 8% theo Nghị quyết 204/2025/QH15 đến 31/12/2026.</summary>
    public decimal TicketVatPercent { get; set; } = 8;
    public decimal FnbVatPercent { get; set; } = 8;

    /// <summary>Phần trăm tích điểm theo hạng thành viên (MEMBER, VIP, VVIP).</summary>
    public Dictionary<string, int> PointEarnPercent { get; set; } = new()
    {
        [LoyaltyTier.Member] = 5,
        [LoyaltyTier.Vip] = 7,
        [LoyaltyTier.Vvip] = 10,
    };
    public long PointValueVnd { get; set; } = 1000;
    public int MaxPointsRedeemPercent { get; set; } = 50;
    public long TierVipSpend12Months { get; set; } = 4_000_000;
    public long TierVvipSpend12Months { get; set; } = 8_000_000;

    public long CompensationVoucherAmount { get; set; } = 50_000;
    public int CompensationVoucherValidDays { get; set; } = 60;
    public int ApprovalRequestExpireMinutes { get; set; } = 10;
    public int ManagerPinMaxAttempts { get; set; } = 5;
    public int ManagerPinLockMinutes { get; set; } = 5;
}
