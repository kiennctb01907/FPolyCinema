using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Dựng dữ liệu vận hành ca (UI A7, A1-05) dùng chung cho /ops và khối vận hành ở dashboard /admin: yêu cầu phê duyệt đang chờ, suất đang chạy kèm sĩ số,
/// suất trễ, phòng đang dọn, ghế nội bộ cần nhắc, phiếu hủy hàng chờ duyệt. Thuộc lớp giao diện vì dựng ViewModel (quyết định 57).
/// </summary>
public class OpsDashboardPages
{
    private readonly ApprovalService _approvals;
    private readonly ShowtimeOpsService _ops;
    private readonly OpsManagementService _mgmt;
    private readonly CinemaContextService _cinema;
    private readonly CinemaRules _rules;

    public OpsDashboardPages(ApprovalService approvals, ShowtimeOpsService ops, OpsManagementService mgmt, CinemaContextService cinema, CinemaRules rules)
    {
        _approvals = approvals;
        _ops = ops;
        _mgmt = mgmt;
        _cinema = cinema;
        _rules = rules;
    }

    public OpsDashboardVm Build()
    {
        var cinemaId = _cinema.CurrentCinemaId;
        var (all, reminders) = _mgmt.HouseSeats(cinemaId);
        var cinemaIds = cinemaId is int c ? new List<int> { c } : _cinema.Cinemas.Select(x => x.Id).ToList();
        return new OpsDashboardVm
        {
            ScopeName = _cinema.Current?.Name ?? "Toàn chuỗi", CinemaId = cinemaId, Approvals = _approvals.Pending(cinemaId), Board = _ops.Board(cinemaId),
            HouseSeats = all, HouseReminders = reminders, PendingWastage = WastageLogDAL.Search(cinemaId, WastageStatus.Pending, 20),
            HouseReminderMinutes = _rules.HouseSeatReminderMinutes,
            CinemaIds = cinemaIds, AuditoriumIds = AuditoriumDAL.GetAll().Where(a => cinemaIds.Contains(a.CinemaId)).Select(a => a.Id).ToList(),
        };
    }
}
