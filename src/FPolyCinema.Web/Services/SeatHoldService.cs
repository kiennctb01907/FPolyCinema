using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Services;

/// <summary>Tùy chọn của một lần giữ ghế.</summary>
/// <param name="Channel">Kênh bán (POS, ONLINE) để tính hạn bán.</param>
/// <param name="ActorRole">Vai trò người giữ ghế; PosStaff mới được bỏ qua luật ghế mồ côi.</param>
/// <param name="OverrideOrphan">Nhân viên chủ động bỏ qua luật ghế mồ côi (phải có OverrideReason, ghi AuditLog).</param>
/// <param name="WheelchairConfirmed">Đã xác nhận "Vị trí dành cho khách dùng xe lăn".</param>
public sealed record HoldOptions(string Channel, string ActorRole, int? UserId, bool OverrideOrphan = false, string? OverrideReason = null, bool WheelchairConfirmed = false);

/// <summary>Kết quả giữ ghế: hóa đơn sau khi giữ, các ghế đang giữ, ghế đổi trạng thái (đã phát SEAT_MAP_UPDATED).</summary>
public sealed record HoldResult(Bill Bill, IReadOnlyList<int> HeldSeatIds, IReadOnlyDictionary<int, List<SeatChange>> Changes, bool OrphanOverridden);

/// <summary>
/// Giữ ghế và chống tranh chấp (SPEC 3.5). Hóa đơn là đơn vị giữ ghế: ShowtimeSeats.HoldBillId trỏ về hóa đơn waiting, HoldExpiresAt
/// đặt ở lần giữ đầu tiên. Mọi bước chạy trong DBUtil.InTransaction, khóa ghế của suất bằng UPDLOCK, HOLDLOCK; phát sự kiện sau khi commit.
/// </summary>
public class SeatHoldService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly BillService _bills;
    private readonly RealtimeNotifier _notifier;

    public SeatHoldService(IClock clock, CinemaRules rules, BillService bills, RealtimeNotifier notifier)
    {
        _clock = clock;
        _rules = rules;
        _bills = bills;
        _notifier = notifier;
    }

    /// <summary>
    /// SeatHoldService.Hold(billId, showtimeId, seatIds, override): đặt lựa chọn ghế của hóa đơn trong suất thành đúng tập seatIds.
    /// 1. Suất còn mở bán (SALES_CLOSED). 2. Khóa ghế của suất. 3. Trạng thái hiệu lực. 4. Ghế phải trống hoặc của chính hóa đơn (SEAT_TAKEN).
    /// 5. Cặp SWEETBOX, xác nhận xe lăn, luật ghế mồ côi (PosStaff bỏ qua được nếu có lý do). 6. UPDATE ghế mới thành HELD, ghế cũ bỏ chọn về AVAILABLE;
    /// số dòng cập nhật phải đúng bằng số ghế. 7. Commit rồi phát SEAT_MAP_UPDATED chỉ gồm các ghế thay đổi.
    /// </summary>
    public async Task<HoldResult> Hold(int billId, int showtimeId, IReadOnlyCollection<int> seatIds, HoldOptions options)
    {
        var now = _clock.Now;
        var result = DBUtil.InTransaction((conn, tx) => HoldCore(conn, tx, billId, showtimeId, seatIds.Distinct().ToList(), options, now));
        foreach (var (sid, changes) in result.Changes) await _notifier.SeatMapUpdated(sid, changes);
        return result;
    }

    private HoldResult HoldCore(SqlConnection conn, SqlTransaction tx, int billId, int showtimeId, List<int> requested, HoldOptions o, DateTime now)
    {
        var bill = BillDAL.GetForUpdate(billId, conn, tx) ?? throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
        if (bill.Status != BillStatus.Waiting)
            throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Hóa đơn này đã thanh toán hoặc đã hủy nên không đổi ghế được. Bạn tạo hóa đơn mới.");
        if (SeatHoldRules.IsExpired(bill.HoldExpiresAt, now))
            throw new BusinessRuleException(ErrorCodes.PaymentExpired, $"Đã hết {_rules.SeatHoldMinutes} phút giữ ghế. Bạn chọn lại ghế để tiếp tục nhé.");

        var changes = new Dictionary<int, List<SeatChange>>();
        var info = SeatMapService.Info(showtimeId, conn, tx) ?? throw CategoryService.NotFound("Không tìm thấy suất chiếu.");
        if (info.Auditorium.CinemaId != bill.CinemaId)
            throw FieldErrors.Of("showtimeId", "Suất này thuộc rạp khác, không bán được ở quầy này.");

        // Bước 1: suất còn mở bán (chỉ khi có ghế được giữ; bỏ chọn ghế thì luôn được)
        if (requested.Count > 0 && !SeatHoldRules.IsSalesOpen(info.Showtime.FeatureStartTime, info.Showtime.OperationalStatus, o.Channel, now, _rules))
        {
            var deadline = SeatHoldRules.SalesDeadline(info.Showtime.FeatureStartTime, o.Channel, _rules);
            throw new BusinessRuleException(ErrorCodes.SalesClosed, info.Showtime.OperationalStatus == OperationalStatus.Cancelled
                ? "Suất này đã bị hủy nên không bán vé được."
                : $"Suất {Fmt.Time(info.Showtime.StartTime)} đã hết giờ bán {(o.Channel == BillChannel.Pos ? "tại quầy" : "online")} (đến {Fmt.Time(deadline)}). Bạn chọn suất khác.");
        }

        // Hóa đơn chỉ có tối đa một suất: đang giữ ghế ở suất khác thì trả các ghế đó trước
        if (bill.ShowtimeId is int oldSid && oldSid != showtimeId)
        {
            ShowtimeSeatDAL.LockForShowtime(oldSid, conn, tx);
            var old = ShowtimeSeatDAL.HeldByBill(bill.Id, conn, tx).Where(s => s.ShowtimeId == oldSid).Select(s => s.SeatId).ToList();
            ShowtimeSeatDAL.Release(oldSid, bill.Id, old, conn, tx);
            changes[oldSid] = old.Select(id => new SeatChange(id, SeatDisplayStatus.Available, null, null)).ToList();
        }

        // Bước 2, 3: khóa theo suất và tính trạng thái hiệu lực
        var states = ShowtimeSeatDAL.LockForShowtime(showtimeId, conn, tx);
        var byId = states.ToDictionary(s => s.SeatId);
        var unknown = requested.Where(id => !byId.ContainsKey(id)).ToList();
        if (unknown.Count > 0) throw FieldErrors.Of("seatIds", "Có ghế không thuộc suất này. Bạn tải lại sơ đồ ghế.");
        var eff = states.ToDictionary(s => s.SeatId, s => SeatMapService.Effective(s, now));
        var mineNow = states.Where(s => eff[s.SeatId] == SeatDisplayStatus.Held && s.HoldBillId == bill.Id).Select(s => s.SeatId).ToHashSet();
        var set = requested.ToHashSet();

        // Bước 4: ghế phải trống hoặc đang do chính hóa đơn giữ
        var blocked = requested.Where(id => !SeatHoldRules.CanHold(eff[id], byId[id].HoldBillId, bill.Id)).Select(id => byId[id]).ToList();
        if (blocked.Count > 0) throw SeatTaken(blocked, eff);

        // Bước 5: cặp ghế đôi, xác nhận xe lăn, luật ghế mồ côi
        var pairs = SweetboxRule.IncompletePairs(states.Where(s => s.SeatType == SeatType.Sweetbox).Select(s => new PairSeat(s.SeatId, s.Label, s.PairGroupId)), set);
        if (pairs.Count > 0)
            throw new BusinessRuleException(ErrorCodes.SweetboxPair, SweetboxRule.Message(pairs[0]),
                new { seats = pairs[0].Select(p => p.Label), seatIds = pairs[0].Select(p => p.SeatId) });

        var newWheelchairs = requested.Where(id => !mineNow.Contains(id) && byId[id].SeatType == SeatType.Wheelchair).Select(id => byId[id]).ToList();
        if (newWheelchairs.Count > 0 && !o.WheelchairConfirmed)
            throw new BusinessRuleException(ErrorCodes.WheelchairConfirmRequired,
                $"Ghế {string.Join(", ", newWheelchairs.Select(s => s.Label))} là vị trí dành cho khách dùng xe lăn. Bạn xác nhận trước khi chọn.",
                new { seats = newWheelchairs.Select(s => s.Label), seatIds = newWheelchairs.Select(s => s.SeatId) });

        bool overridden = false;
        var slots = states.Select(s => new SeatSlot(s.SeatId, s.RowIndex, s.ColIndex, s.Label, s.SeatType,
            eff[s.SeatId] is SeatDisplayStatus.Booked or SeatDisplayStatus.HouseHold or SeatDisplayStatus.Maintenance
            || (eff[s.SeatId] == SeatDisplayStatus.Held && s.HoldBillId != bill.Id))).ToList();
        var orphan = OrphanSeatValidator.Validate(slots, set);
        if (orphan != null)
        {
            bool canOverride = o.Channel == BillChannel.Pos && Roles.PosStaff.Contains(o.ActorRole);
            if (canOverride && o.OverrideOrphan && !string.IsNullOrWhiteSpace(o.OverrideReason))
            {
                overridden = true;
                AuditLogDAL.Insert(new AuditLog
                {
                    UserId = o.UserId,
                    Action = AuditAction.OrphanSeatOverride,
                    EntityType = "Bill",
                    EntityId = bill.Id.ToString(),
                    Detail = Truncate($"Hóa đơn {bill.Code}, suất {showtimeId}: bỏ qua luật ghế mồ côi ({string.Join(", ", orphan.Orphans.Select(x => x.Label))}). Lý do: {o.OverrideReason!.Trim()}"),
                    CreatedAt = now,
                }, conn, tx);
            }
            else
            {
                throw new BusinessRuleException(ErrorCodes.OrphanSeat, orphan.Message, new
                {
                    orphans = orphan.Orphans.Select(x => x.Label),
                    orphanSeatIds = orphan.Orphans.Select(x => x.SeatId),
                    suggestion = orphan.Suggestion.Select(x => x.Label),
                    suggestionSeatIds = orphan.Suggestion.Select(x => x.SeatId),
                    selectedSeatIds = requested,
                    canOverride,
                });
            }
        }

        // Bước 6: giữ ghế mới (và các ghế giữ tiếp), trả ghế bị bỏ chọn
        var expiry = requested.Count > 0 ? SeatHoldRules.HoldExpiry(bill.HoldExpiresAt, now, _rules) : bill.HoldExpiresAt;
        if (requested.Count > 0)
        {
            int n = ShowtimeSeatDAL.Hold(showtimeId, bill.Id, expiry!.Value, now, requested, conn, tx);
            if (n != requested.Count) throw SeatTaken(requested.Where(id => !mineNow.Contains(id)).Select(id => byId[id]).ToList(), eff);
        }
        var dropped = mineNow.Where(id => !set.Contains(id)).ToList();
        ShowtimeSeatDAL.Release(showtimeId, bill.Id, dropped, conn, tx);

        var list = changes.TryGetValue(showtimeId, out var l) ? l : changes[showtimeId] = new List<SeatChange>();
        list.AddRange(dropped.Select(id => new SeatChange(id, SeatDisplayStatus.Available, null, null)));
        list.AddRange(requested.Where(id => !mineNow.Contains(id)).Select(id => new SeatChange(id, SeatDisplayStatus.Held, expiry, bill.Id)));

        bill.ShowtimeId = requested.Count > 0 ? showtimeId : (bill.ShowtimeId == showtimeId ? null : bill.ShowtimeId);
        if (requested.Count == 0 && bill.ShowtimeId == null) bill.GuardianConfirmed = false;
        bill.HoldExpiresAt = expiry;
        BillDAL.Update(bill, conn, tx);
        _bills.Recalculate(bill.Id, conn, tx);

        return new HoldResult(BillDAL.GetById(bill.Id, conn, tx)!, requested, changes, overridden);
    }

    private static BusinessRuleException SeatTaken(List<SeatStateRow> seats, IReadOnlyDictionary<int, string> eff)
    {
        var labels = string.Join(", ", seats.Select(s => s.Label));
        var first = seats.Count > 0 ? eff[seats[0].SeatId] : SeatDisplayStatus.Held;
        string message = first switch
        {
            SeatDisplayStatus.Maintenance => $"Ghế {labels} đang bảo trì nên không bán được. Bạn chọn ghế khác.",
            SeatDisplayStatus.HouseHold => $"Ghế {labels} là ghế nội bộ, quản lý ca cần giải phóng trước khi bán. Bạn chọn ghế khác.",
            _ => $"Ghế {labels} vừa có người chọn trước nên đã được bỏ khỏi lựa chọn của bạn.",
        };
        return new BusinessRuleException(ErrorCodes.SeatTaken, message, new { seats = seats.Select(s => s.Label), seatIds = seats.Select(s => s.SeatId) });
    }

    /// <summary>
    /// Bộ quét giữ ghế (SPEC 3.5, gọi từ SeatHoldSweeper): hủy EXPIRED các hóa đơn waiting đã hết hạn và trả ghế của chúng về AVAILABLE,
    /// trả nốt các ghế HELD hết hạn còn sót, rồi phát SEAT_MAP_UPDATED. Trả số hóa đơn đã hủy.
    /// </summary>
    public async Task<int> SweepExpired(int limit = 200)
    {
        var now = _clock.Now;
        var all = new Dictionary<int, List<SeatChange>>();
        int expired = 0;
        foreach (var id in BillDAL.ExpiredWaitingIds(now, limit))
        {
            var changes = DBUtil.InTransaction((conn, tx) => _bills.ExpireBill(id, now, conn, tx));
            if (changes == null) continue;
            expired++;
            Merge(all, changes);
        }
        var stray = DBUtil.InTransaction((conn, tx) => ShowtimeSeatDAL.ReleaseStrayExpired(now, conn, tx));
        Merge(all, stray.GroupBy(s => s.ShowtimeId).ToDictionary(g => g.Key, g => g.Select(s => new SeatChange(s.SeatId, SeatDisplayStatus.Available, null, null)).ToList()));
        foreach (var (sid, changes) in all) await _notifier.SeatMapUpdated(sid, changes);
        return expired;
    }

    private static void Merge(Dictionary<int, List<SeatChange>> into, IReadOnlyDictionary<int, List<SeatChange>> from)
    {
        foreach (var (k, v) in from)
        {
            if (!into.TryGetValue(k, out var list)) into[k] = list = new List<SeatChange>();
            list.AddRange(v);
        }
    }

    private static string Truncate(string s) => s.Length <= 2000 ? s : s[..1997] + "…";
}
