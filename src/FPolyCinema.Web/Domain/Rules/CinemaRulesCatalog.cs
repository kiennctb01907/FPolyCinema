using System.Globalization;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một tham số CinemaRules đang áp dụng: khóa cấu hình, tên tiếng Việt, giá trị đã định dạng, giá trị mặc định và có khác mặc định không.</summary>
public sealed record RuleSetting(string Key, string Label, string Value, string DefaultValue, bool Changed, string? Note);

/// <summary>Một nhóm tham số trên trang Cấu hình rạp.</summary>
public sealed record RuleSettingGroup(string Title, string Icon, List<RuleSetting> Settings);

/// <summary>
/// Danh mục tham số nghiệp vụ CinemaRules cho trang Cấu hình rạp (UI A10, chỉ đọc): chia nhóm, tên tiếng Việt, định dạng giá trị (phút, giờ, %, ₫)
/// và so với giá trị mặc định ở CONTRACTS mục 7 (giá trị khởi tạo của lớp CinemaRules). Logic thuần, có unit test bảo đảm mọi khóa đều có mặt đúng một lần.
/// </summary>
public static class CinemaRulesCatalog
{
    private enum Kind { Minutes, Seconds, Days, Clock, Share, Percent, Money, Count, Score, Text, PointTable }

    private sealed record Item(string Key, string Label, Kind Kind, Func<CinemaRules, object> Get, string? Note = null);

    private static readonly (string Title, string Icon, Item[] Items)[] Groups =
    {
        ("Giờ vận hành và suất chiếu", "bi-clock", new[]
        {
            new Item(nameof(CinemaRules.TimeZoneId), "Múi giờ", Kind.Text, r => r.TimeZoneId, "Mọi giờ nghiệp vụ tính theo múi giờ này"),
            new Item(nameof(CinemaRules.OperatingDayStart), "Bắt đầu ngày vận hành", Kind.Clock, r => r.OperatingDayStart, "Suất bắt đầu trước 06:00 thuộc ngày vận hành hôm trước"),
            new Item(nameof(CinemaRules.LastShowStartLatest), "Suất cuối bắt đầu muộn nhất", Kind.Clock, r => r.LastShowStartLatest),
            new Item(nameof(CinemaRules.AdsMinutes), "Thời lượng quảng cáo trước phim", Kind.Minutes, r => r.AdsMinutes),
            new Item(nameof(CinemaRules.CreditsMinutes), "Thời lượng danh đề cuối phim", Kind.Minutes, r => r.CreditsMinutes),
            new Item(nameof(CinemaRules.CleaningMinutes), "Thời gian dọn phòng", Kind.Minutes, r => r.CleaningMinutes),
            new Item(nameof(CinemaRules.BoardingLeadMinutes), "Mở cửa đón khách trước giờ chiếu", Kind.Minutes, r => r.BoardingLeadMinutes),
            new Item(nameof(CinemaRules.ChildU13LatestEnd), "Trẻ dưới 13 tuổi: suất kết thúc muộn nhất", Kind.Clock, r => r.ChildU13LatestEnd, "Điều 9 khoản 4 Nghị định 131/2022"),
            new Item(nameof(CinemaRules.ChildU16LatestEnd), "Trẻ dưới 16 tuổi: suất kết thúc muộn nhất", Kind.Clock, r => r.ChildU16LatestEnd, "Điều 9 khoản 4 Nghị định 131/2022"),
        }),
        ("Giữ ghế và giờ bán", "bi-hourglass-split", new[]
        {
            new Item(nameof(CinemaRules.SeatHoldMinutes), "Thời gian giữ ghế", Kind.Minutes, r => r.SeatHoldMinutes, "Đổi ghế không gia hạn"),
            new Item(nameof(CinemaRules.SeatHoldExtendMinutes), "Thu ngân gia hạn thêm (một lần)", Kind.Minutes, r => r.SeatHoldExtendMinutes),
            new Item(nameof(CinemaRules.HoldSweepSeconds), "Chu kỳ quét ghế hết hạn giữ", Kind.Seconds, r => r.HoldSweepSeconds),
            new Item(nameof(CinemaRules.OnlineSalesCutoffMinutes), "Bán online tới sau giờ phim bắt đầu", Kind.Minutes, r => r.OnlineSalesCutoffMinutes),
            new Item(nameof(CinemaRules.PosSalesCutoffMinutes), "Bán tại quầy tới sau giờ phim bắt đầu", Kind.Minutes, r => r.PosSalesCutoffMinutes),
            new Item(nameof(CinemaRules.CrossShowtimeWindowMinutes), "Gợi ý suất khác trong khoảng", Kind.Minutes, r => r.CrossShowtimeWindowMinutes),
            new Item(nameof(CinemaRules.GoodClusterScore), "Điểm cụm ghế được coi là đẹp", Kind.Score, r => r.GoodClusterScore),
        }),
        ("Giá vé và thuế", "bi-tags", new[]
        {
            new Item(nameof(CinemaRules.PriceSlotAfternoonStart), "Bắt đầu khung giá buổi chiều", Kind.Clock, r => r.PriceSlotAfternoonStart),
            new Item(nameof(CinemaRules.PriceSlotGoldenStart), "Bắt đầu khung giờ vàng", Kind.Clock, r => r.PriceSlotGoldenStart),
            new Item(nameof(CinemaRules.PriceSlotLateStart), "Bắt đầu khung suất khuya", Kind.Clock, r => r.PriceSlotLateStart),
            new Item(nameof(CinemaRules.PriceRoundTo), "Làm tròn giá vé tới", Kind.Money, r => (long)r.PriceRoundTo),
            new Item(nameof(CinemaRules.TicketVatPercent), "VAT vé xem phim", Kind.Percent, r => r.TicketVatPercent, "Mức 10% giảm còn 8% theo Nghị quyết 204/2025/QH15 đến 31/12/2026"),
            new Item(nameof(CinemaRules.FnbVatPercent), "VAT bắp nước", Kind.Percent, r => r.FnbVatPercent),
        }),
        ("Xếp lịch và ghế nội bộ", "bi-calendar3", new[]
        {
            new Item(nameof(CinemaRules.ScheduleRoundMinutes), "Làm tròn giờ bắt đầu suất tới bội số", Kind.Minutes, r => r.ScheduleRoundMinutes),
            new Item(nameof(CinemaRules.MaxSameMinuteStarts), "Số suất tối đa bắt đầu cùng phút", Kind.Count, r => r.MaxSameMinuteStarts),
            new Item(nameof(CinemaRules.MinStartStaggerMinutes), "Các phòng lệch giờ bắt đầu ít nhất", Kind.Minutes, r => r.MinStartStaggerMinutes),
            new Item(nameof(CinemaRules.HouseSeatsPerAuditorium), "Ghế nội bộ mỗi phòng", Kind.Count, r => r.HouseSeatsPerAuditorium),
            new Item(nameof(CinemaRules.HouseSeatReminderMinutes), "Nhắc giải phóng ghế nội bộ trước giờ chiếu", Kind.Minutes, r => r.HouseSeatReminderMinutes),
        }),
        ("Phim Việt", "bi-flag", new[]
        {
            new Item(nameof(CinemaRules.VietnameseFilmMinShare), "Tỷ lệ suất phim Việt tối thiểu", Kind.Share, r => r.VietnameseFilmMinShare, "Điều 9 khoản 3 Nghị định 131/2022, từ 01/01/2026"),
            new Item(nameof(CinemaRules.VietnameseFilmTargetShare), "Mục tiêu của bộ xếp lịch AI", Kind.Share, r => r.VietnameseFilmTargetShare),
            new Item(nameof(CinemaRules.VietnameseFilmPriorityStart), "Khung ưu tiên phim Việt từ", Kind.Clock, r => r.VietnameseFilmPriorityStart, "Điều 9 khoản 2 Nghị định 131/2022"),
            new Item(nameof(CinemaRules.VietnameseFilmPriorityEnd), "Khung ưu tiên phim Việt đến", Kind.Clock, r => r.VietnameseFilmPriorityEnd),
        }),
        ("Thành viên và điểm", "bi-award", new[]
        {
            new Item(nameof(CinemaRules.PointEarnPercent), "Tỷ lệ tích điểm theo hạng", Kind.PointTable, r => r.PointEarnPercent),
            new Item(nameof(CinemaRules.PointValueVnd), "Giá trị một điểm", Kind.Money, r => r.PointValueVnd),
            new Item(nameof(CinemaRules.MaxPointsRedeemPercent), "Đổi điểm tối đa trên giá trị đơn", Kind.Percent, r => (decimal)r.MaxPointsRedeemPercent),
            new Item(nameof(CinemaRules.TierVipSpend12Months), "Chi tiêu 12 tháng để lên VIP", Kind.Money, r => r.TierVipSpend12Months),
            new Item(nameof(CinemaRules.TierVvipSpend12Months), "Chi tiêu 12 tháng để lên VVIP", Kind.Money, r => r.TierVvipSpend12Months),
        }),
        ("Hủy vé, bồi thường và phê duyệt", "bi-shield-check", new[]
        {
            new Item(nameof(CinemaRules.SelfCancelMinMinutesBeforeStart), "Khách tự hủy trước giờ chiếu ít nhất", Kind.Minutes, r => r.SelfCancelMinMinutesBeforeStart),
            new Item(nameof(CinemaRules.SelfCancelMaxPerMonth), "Số lần tự hủy tối đa mỗi tháng", Kind.Count, r => r.SelfCancelMaxPerMonth, "Tính theo tháng dương lịch, giờ Việt Nam"),
            new Item(nameof(CinemaRules.ImHereWindowMinutesBeforeStart), "Nút \"Tôi đã tới rạp\" hiện trước giờ chiếu", Kind.Minutes, r => r.ImHereWindowMinutesBeforeStart),
            new Item(nameof(CinemaRules.CompensationVoucherAmount), "Voucher bồi thường khi hủy suất", Kind.Money, r => r.CompensationVoucherAmount),
            new Item(nameof(CinemaRules.CompensationVoucherValidDays), "Hạn dùng voucher bồi thường", Kind.Days, r => r.CompensationVoucherValidDays),
            new Item(nameof(CinemaRules.ApprovalRequestExpireMinutes), "Yêu cầu duyệt hủy từ xa hết hạn sau", Kind.Minutes, r => r.ApprovalRequestExpireMinutes),
            new Item(nameof(CinemaRules.ManagerPinMaxAttempts), "Số lần nhập sai PIN quản lý tối đa", Kind.Count, r => r.ManagerPinMaxAttempts),
            new Item(nameof(CinemaRules.ManagerPinLockMinutes), "Khóa PIN sau khi nhập sai quá số lần", Kind.Minutes, r => r.ManagerPinLockMinutes),
        }),
    };

    /// <summary>Mọi khóa có trên trang (dùng cho test bảo đảm không sót khóa nào của CinemaRules).</summary>
    public static IReadOnlyList<string> Keys => Groups.SelectMany(g => g.Items).Select(i => i.Key).ToList();

    /// <summary>Mô tả tham số đang áp dụng, so với giá trị mặc định (new CinemaRules()).</summary>
    public static List<RuleSettingGroup> Describe(CinemaRules current)
    {
        var defaults = new CinemaRules();
        return Groups.Select(g => new RuleSettingGroup(g.Title, g.Icon, g.Items.Select(i =>
        {
            var value = Format(i.Kind, i.Get(current));
            var def = Format(i.Kind, i.Get(defaults));
            return new RuleSetting(i.Key, i.Label, value, def, value != def, i.Note);
        }).ToList())).ToList();
    }

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private static string Format(Kind kind, object v) => kind switch
    {
        Kind.Minutes => $"{v} phút",
        Kind.Seconds => $"{v} giây",
        Kind.Days => $"{v} ngày",
        Kind.Clock => ((TimeSpan)v).ToString(@"hh\:mm", CultureInfo.InvariantCulture),
        Kind.Share => ((decimal)v * 100).ToString("0.##", Vi) + "%",
        Kind.Percent => ((decimal)v).ToString("0.##", Vi) + "%",
        Kind.Money => Fmt.Money((long)v),
        Kind.Count => v.ToString() ?? string.Empty,
        Kind.Score => $"{v}/100",
        Kind.PointTable => string.Join(" · ", ((Dictionary<string, int>)v).OrderBy(p => p.Value).Select(p => $"{TierLabel(p.Key)} {p.Value}%")),
        _ => v.ToString() ?? string.Empty,
    };

    private static string TierLabel(string tier) => tier switch { "MEMBER" => "Thành viên", _ => tier };
}
