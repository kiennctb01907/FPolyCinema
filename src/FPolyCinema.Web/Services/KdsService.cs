using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Một đơn trên màn hình bếp và bảng gọi số (payload của KDS_ORDER_QUEUED, KDS_ORDER_UPDATED).</summary>
public sealed record KdsOrder(int BillId, string Code, string Channel, string ChannelLabel, string? PickupNumber, string FnbStatus, DateTime QueuedAt,
    List<KdsItem> Items);

/// <summary>Một món của đơn bếp.</summary>
public sealed record KdsItem(string Name, int Quantity);

/// <summary>
/// Bếp và bảng gọi số (SPEC 3.11, UI P4, P5): đơn bắp nước mua tại quầy hiện ngay trên màn hình bếp; đơn online bắt đầu làm khi khách bấm
/// "Tôi đã tới rạp – làm bắp nước ngay" hoặc thu ngân quét QR ở quầy bắp nước (RELEASE + CONSUME, PREPARING); bếp bấm "Sẵn sàng giao"
/// (READY_FOR_PICKUP, số gọi lên bảng, FNB_STATUS_CHANGED tới khách) rồi "Đã giao" (FULFILLED). Phát KDS_ORDER_QUEUED, KDS_ORDER_UPDATED tới fnb_kds_{cinemaId}.
/// </summary>
public class KdsService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly RealtimeNotifier _notifier;
    private readonly InventoryService _inventory;

    public KdsService(IClock clock, CinemaRules rules, RealtimeNotifier notifier, InventoryService inventory)
    {
        _clock = clock;
        _rules = rules;
        _notifier = notifier;
        _inventory = inventory;
    }

    /// <summary>Các đơn đang chuẩn bị và sẵn sàng giao của rạp (cũ nhất trước).</summary>
    public static List<KdsOrder> Orders(int cinemaId, int? billId = null)
    {
        var rows = BillDAL.KdsOrders(cinemaId, billId);
        var lines = BillDetailDAL.KdsLines(rows.Select(r => r.BillId).ToList());
        return rows.Select(r => ToOrder(r, lines)).ToList();
    }

    private static KdsOrder ToOrder(KdsOrderRow r, List<KdsLineRow> lines) => new(r.BillId, r.Code, r.Channel, r.Channel == BillChannel.Pos ? "Quầy" : "Online",
        r.PickupNumber is int n ? CodeGenerator.FormatPickupNumber(n) : null, r.FnbStatus, r.QueuedAt,
        lines.Where(l => l.BillId == r.BillId).Select(l => new KdsItem(l.DrinkName, l.Quantity)).ToList());

    /// <summary>Đơn bắp nước mua tại quầy vừa thanh toán (PREPARING): đưa lên màn hình bếp ngay (SPEC 3.11-06).</summary>
    public async Task QueuePosOrder(int billId, int cinemaId)
    {
        var order = Orders(cinemaId, billId).FirstOrDefault();
        if (order != null) await _notifier.KdsOrderQueued(cinemaId, order);
    }

    /// <summary>
    /// Bắt đầu làm món cho đơn online (SPEC 3.11-04): hóa đơn finish có bắp nước PENDING_PICKUP. ReservedStock và ActualStock cùng giảm (RELEASE + CONSUME),
    /// FnbStatus = PREPARING, cấp số gọi, đơn hiện trên màn hình bếp. byCustomerId: khách bấm "Tôi đã tới rạp" (chỉ trong khung giờ cho phép);
    /// cinemaId: thu ngân quét QR ở quầy bắp nước của rạp đó.
    /// </summary>
    public async Task<KdsOrder> StartOnline(int billId, int? byCustomerId, int? cinemaId, int? staffId)
    {
        var now = _clock.Now;
        var bill0 = BillDAL.GetById(billId) ?? throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
        var before = InventoryService.Snapshot(bill0.CinemaId);
        var bill = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(billId, conn, tx)!;
            if (byCustomerId != null && b.CustomerId != byCustomerId) throw CategoryService.NotFound("Không tìm thấy vé.");
            if (cinemaId != null && b.CinemaId != cinemaId) throw FieldErrors.Of("code", "Hóa đơn này thuộc rạp khác.");
            var reason = KdsRules.CannotStartReason(b.Status, b.FnbStatus);
            if (reason != null) throw new BusinessRuleException(ErrorCodes.InvalidTransition, reason);
            if (byCustomerId != null)
            {
                var st = b.ShowtimeId is int sid ? ShowtimeDAL.GetById(sid, conn, tx) : null;
                if (st != null && !KdsRules.InImHereWindow(st.StartTime, st.EndTime, now, _rules.ImHereWindowMinutesBeforeStart))
                    throw new BusinessRuleException(ErrorCodes.InvalidTransition,
                        $"Nút \"Tôi đã tới rạp\" dùng được từ {Fmt.Time(st.StartTime.AddMinutes(-_rules.ImHereWindowMinutesBeforeStart))} đến {Fmt.Time(st.EndTime)}.");
            }
            _inventory.StartPreparingOnline(b, staffId ?? byCustomerId, now, conn, tx);
            var day = PricingCalculator.OperatingDate(now);
            int pickup = b.PickupNumber ?? CodeGenerator.NextPickupNumber(BillDAL.LastPickupNumber(b.CinemaId, day.Add(PricingCalculator.NightCutoff), day.AddDays(1).Add(PricingCalculator.NightCutoff), conn, tx));
            b.PickupNumber = pickup;
            b.FnbStatus = FnbStatus.Preparing;
            BillDAL.Update(b, conn, tx);
            return b;
        });
        var order = Orders(bill.CinemaId, bill.Id).First();
        await _notifier.KdsOrderQueued(bill.CinemaId, order);
        await _notifier.FnbStatusChanged(bill.Id, FnbStatus.Preparing, bill.PickupNumber, StatusDisplay.Of(StatusDisplay.Fnb, FnbStatus.Preparing).Label);
        await InventoryService.PublishAlerts(_notifier, before);
        return order;
    }

    /// <summary>Bếp bấm "Sẵn sàng giao" (READY_FOR_PICKUP) hoặc "Đã giao" (FULFILLED).</summary>
    public async Task<KdsOrder> Advance(int billId, string to, int cinemaId)
    {
        var b = BillDAL.GetById(billId) ?? throw CategoryService.NotFound("Không tìm thấy đơn.");
        if (b.CinemaId != cinemaId) throw FieldErrors.Of("billId", "Đơn này thuộc rạp khác.");
        if (!KdsRules.CanAdvance(b.FnbStatus, to) || BillDAL.SetFnbStatus(b.Id, b.FnbStatus, to) != 1)
            throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Đơn đã được cập nhật ở máy khác. Màn hình sẽ tải lại.");
        // Giữ thời điểm vào hàng chờ ban đầu (đồng hồ chờ trên thẻ không về 0); đơn đã giao không còn trong danh sách thì dựng từ hóa đơn
        var order = Orders(cinemaId, b.Id).FirstOrDefault() ?? ToOrder(new KdsOrderRow(b.Id, b.Code, b.Channel, b.PickupNumber, to, _clock.Now), BillDetailDAL.KdsLines(new[] { b.Id }));
        await _notifier.KdsOrderUpdated(cinemaId, order);
        await _notifier.FnbStatusChanged(b.Id, to, b.PickupNumber, StatusDisplay.Of(StatusDisplay.Fnb, to).Label);
        return order;
    }

    /// <summary>Đơn bị hủy khi đang chuẩn bị hoặc chờ nhận: gỡ khỏi màn hình bếp.</summary>
    public async Task Removed(int billId, int cinemaId, string? pickupNumber) =>
        await _notifier.KdsOrderUpdated(cinemaId, new KdsOrder(billId, string.Empty, string.Empty, string.Empty, pickupNumber, FnbStatus.Voided, _clock.Now, new List<KdsItem>()));
}
