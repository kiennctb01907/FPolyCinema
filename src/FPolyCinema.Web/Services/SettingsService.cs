using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Services;

/// <summary>Một rạp trên trang Cấu hình rạp: thông tin liên hệ và số phòng chiếu đang hoạt động.</summary>
public sealed record CinemaSettingRow(int Id, string Name, string Address, string? Phone, bool Active, int ActiveAuditoriums, int TotalAuditoriums);

/// <summary>Dữ liệu trang Cấu hình rạp (UI A10): hệ thống rạp, tham số CinemaRules đang áp dụng, múi giờ, giờ hiện tại.</summary>
public sealed record SettingsPage(List<CinemaSettingRow> Cinemas, List<RuleSettingGroup> Groups, string TimeZoneId, string TimeZoneName, DateTime Now, int ChangedCount);

/// <summary>
/// Trang Cấu hình rạp chỉ đọc (UI A10): tham số nghiệp vụ đọc từ appsettings.json mục CinemaRules (CONTRACTS mục 7) và biến môi trường CinemaRules__TênKhóa,
/// không sửa trên web.
/// </summary>
public class SettingsService
{
    private readonly CinemaRules _rules;
    private readonly IClock _clock;

    public SettingsService(CinemaRules rules, IClock clock)
    {
        _rules = rules;
        _clock = clock;
    }

    public SettingsPage Build()
    {
        var rooms = AuditoriumDAL.GetAll();
        var cinemas = CinemaDAL.GetAll().OrderBy(c => c.Id)
            .Select(c => new CinemaSettingRow(c.Id, c.Name, c.Address, c.Phone, c.Active,
                rooms.Count(a => a.CinemaId == c.Id && a.Active), rooms.Count(a => a.CinemaId == c.Id)))
            .ToList();
        var groups = CinemaRulesCatalog.Describe(_rules);
        var zone = SystemClock.ResolveTimeZone(_rules.TimeZoneId);
        return new SettingsPage(cinemas, groups, _rules.TimeZoneId, zone.DisplayName, _clock.Now, groups.Sum(g => g.Settings.Count(s => s.Changed)));
    }
}
