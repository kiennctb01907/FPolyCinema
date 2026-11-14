using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Services;

/// <summary>Ảnh chụp kho của một rạp trước một giao dịch (để so sánh và phát cảnh báo sau khi commit).</summary>
public sealed record StockSnapshot(int CinemaId, Dictionary<int, int> Portions, List<MaterialLevel> Materials);

/// <summary>
/// Kho hai tầng của Phase 6 (SPEC 3.11): bắt đầu làm món cho đơn online (RELEASE + CONSUME), nhập kho (IMPORT), hủy hàng hỏng (WastageLogs PENDING,
/// SupervisorOrManager duyệt thì WASTAGE), và cảnh báo sau mỗi giao dịch kho: PRODUCT_SOLD_OUT, PRODUCT_AVAILABLE (cờ IsSoldOut), STOCK_LOW.
/// </summary>
public partial class InventoryService
{
    // ------------------------------------------------------------------ cảnh báo sau giao dịch kho

    /// <summary>Chụp tồn của rạp: số phần làm được của từng món có định mức và tồn của từng nguyên liệu.</summary>
    public static StockSnapshot Snapshot(int cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var lines = RawMaterialDAL.StockLines(cinemaId, null, conn, tx).Select(l => new RecipeStock(l.DrinkId, l.RawMaterialId, l.PerPortion, l.Available));
        var materials = RawMaterialDAL.ByCinema(cinemaId, conn, tx).Select(Level).ToList();
        return new StockSnapshot(cinemaId, InventoryAlertRules.Portions(lines), materials);
    }

    private static MaterialLevel Level(RawMaterial m) => new(m.Id, m.Name, m.Unit, m.ActualStock, m.ReservedStock, m.MinThreshold);

    /// <summary>
    /// So kho hiện tại với ảnh chụp trước giao dịch (InventoryAlertRules), cập nhật cờ IsSoldOut chung của món và phát PRODUCT_SOLD_OUT,
    /// PRODUCT_AVAILABLE tới cinema_{cinemaId}, STOCK_LOW tới manager_alerts_{cinemaId}. Gọi sau khi transaction đã commit.
    /// </summary>
    public static async Task<InventoryAlerts> PublishAlerts(RealtimeNotifier notifier, StockSnapshot before)
    {
        var after = Snapshot(before.CinemaId);
        var alerts = InventoryAlertRules.Evaluate(before.Portions, after.Portions, before.Materials, after.Materials);
        SyncSoldOutFlags(alerts.SoldOut.Concat(alerts.Available).ToList());
        await notifier.ProductSoldOut(before.CinemaId, alerts.SoldOut);
        await notifier.ProductAvailable(before.CinemaId, alerts.Available);
        if (alerts.Low.Count > 0)
            await notifier.StockLow(before.CinemaId, new
            {
                materials = alerts.Low.Select(m => new { id = m.Id, name = m.Name, unit = m.Unit, actual = m.Actual, minThreshold = m.MinThreshold, text = $"{m.Name} còn {Fmt.Decimal(m.Actual)} {m.Unit}, ngưỡng {Fmt.Decimal(m.MinThreshold)}" }),
            });
        return alerts;
    }

    /// <summary>Cờ IsSoldOut chung của món: tạm hết khi mọi rạp có định mức của món đều không làm được một phần (Drinks dùng chung cho mọi rạp).</summary>
    public static void SyncSoldOutFlags(IReadOnlyCollection<int> drinkIds)
    {
        if (drinkIds.Count == 0) return;
        var perCinema = CinemaDAL.GetActive().Select(c => InventoryAlertRules.Portions(RawMaterialDAL.StockLines(c.Id, drinkIds)
            .Select(l => new RecipeStock(l.DrinkId, l.RawMaterialId, l.PerPortion, l.Available)))).ToList();
        var soldOut = new List<int>();
        var available = new List<int>();
        foreach (var id in drinkIds)
        {
            var values = perCinema.Where(p => p.ContainsKey(id)).Select(p => p[id]);
            if (InventoryAlertRules.SoldOutEverywhere(values)) soldOut.Add(id); else available.Add(id);
        }
        DrinkDAL.SetSoldOut(soldOut, true);
        DrinkDAL.SetSoldOut(available, false);
    }

    // ------------------------------------------------------------------ bắt đầu làm món cho đơn online

    /// <summary>
    /// Đơn online bắt đầu làm món (SPEC 3.11-04): ReservedStock và ActualStock cùng giảm theo định mức (RELEASE + CONSUME).
    /// </summary>
    public void StartPreparingOnline(Bill bill, int? userId, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        RawMaterialDAL.LockForBill(bill.Id, bill.CinemaId, conn, tx);
        InventoryTransactionDAL.InsertForBill(bill.Id, bill.CinemaId, InventoryTxnType.Release, "Khách tới rạp, bắt đầu làm món", userId, now, conn, tx);
        InventoryTransactionDAL.InsertForBill(bill.Id, bill.CinemaId, InventoryTxnType.Consume, "Làm món cho đơn online", userId, now, conn, tx);
        RawMaterialDAL.ApplyForBill(bill.Id, bill.CinemaId, -1, -1, conn, tx);
    }

    // ------------------------------------------------------------------ nhập kho (IMPORT)

    /// <summary>Phiếu nhập kho đơn giản (Manager, SPEC 3.11-14): mỗi dòng cộng ActualStock và ghi IMPORT. Trả số dòng đã nhập.</summary>
    public int Import(int cinemaId, IReadOnlyList<StockLineInput> lines, string? note, int userId, DateTime now)
    {
        var known = RawMaterialDAL.ByCinema(cinemaId).Select(m => m.Id).ToHashSet();
        var errors = WastageRules.ValidateLines(lines, known, needReason: false);
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        var used = lines.Where(l => l.Quantity > 0).ToList();
        DBUtil.InTransaction((conn, tx) =>
        {
            foreach (var l in used)
            {
                RawMaterialDAL.GetForUpdate(l.RawMaterialId, conn, tx);
                RawMaterialDAL.ChangeActual(l.RawMaterialId, l.Quantity, conn, tx);
                InventoryTransactionDAL.Insert(new InventoryTransaction
                {
                    RawMaterialId = l.RawMaterialId, Type = InventoryTxnType.Import, Quantity = l.Quantity,
                    Note = string.IsNullOrWhiteSpace(note) ? "Phiếu nhập kho" : note.Trim(), CreatedBy = userId, CreatedAt = now,
                }, conn, tx);
            }
        });
        return used.Count;
    }

    // ------------------------------------------------------------------ hủy hàng hỏng (WastageLogs)

    /// <summary>PosStaff lập phiếu hủy hàng hỏng (PENDING), chưa trừ kho. Trả Id phiếu.</summary>
    public int CreateWastage(int cinemaId, IReadOnlyList<StockLineInput> lines, string? note, int userId, DateTime now)
    {
        var known = RawMaterialDAL.ByCinema(cinemaId).Select(m => m.Id).ToHashSet();
        var errors = WastageRules.ValidateLines(lines, known, needReason: true);
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        return DBUtil.InTransaction((conn, tx) =>
        {
            int id = WastageLogDAL.Insert(new WastageLog { CinemaId = cinemaId, Status = WastageStatus.Pending, Note = note?.Trim(), CreatedBy = userId, CreatedAt = now }, conn, tx);
            foreach (var l in lines.Where(l => l.Quantity > 0))
                WastageLogItemDAL.Insert(new WastageLogItem { WastageLogId = id, RawMaterialId = l.RawMaterialId, Quantity = l.Quantity, Reason = l.Reason?.Trim() }, conn, tx);
            return id;
        });
    }

    /// <summary>
    /// SupervisorOrManager duyệt (ActualStock giảm, ghi WASTAGE) hoặc từ chối phiếu hủy hàng PENDING; ghi AuditLogs (SPEC 2.7-07).
    /// Trả rạp của phiếu (để phát cảnh báo kho).
    /// </summary>
    public int DecideWastage(int logId, bool approve, int deciderId, int? deciderCinemaId, DateTime now)
    {
        return DBUtil.InTransaction((conn, tx) =>
        {
            var log = WastageLogDAL.GetForUpdate(logId, conn, tx) ?? throw CategoryService.NotFound("Không tìm thấy phiếu hủy hàng.");
            if (deciderCinemaId != null && log.CinemaId != deciderCinemaId) throw FieldErrors.Of("id", "Phiếu này thuộc rạp khác.");
            if (!WastageRules.CanDecide(log.Status)) throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Phiếu này đã được xử lý trước đó.");
            var items = WastageLogItemDAL.RowsByLog(log.Id, conn, tx);
            if (approve)
            {
                foreach (var i in items)
                {
                    var m = RawMaterialDAL.GetForUpdate(i.RawMaterialId, conn, tx)!;
                    if (!WastageRules.Fits(m.ActualStock, m.ReservedStock, i.Quantity) || RawMaterialDAL.ChangeActual(m.Id, -i.Quantity, conn, tx) != 1)
                        throw new BusinessRuleException(ErrorCodes.StockInsufficient,
                            $"{m.Name} chỉ còn {Fmt.Decimal(m.ActualStock - m.ReservedStock)} {m.Unit} chưa giữ cho đơn online, không hủy được {Fmt.Decimal(i.Quantity)} {m.Unit}.");
                    InventoryTransactionDAL.Insert(new InventoryTransaction
                    {
                        RawMaterialId = m.Id, Type = InventoryTxnType.Wastage, Quantity = i.Quantity, WastageLogId = log.Id, Note = i.Reason, CreatedBy = deciderId, CreatedAt = now,
                    }, conn, tx);
                }
            }
            log.Status = approve ? WastageStatus.Approved : WastageStatus.Rejected;
            log.DecidedBy = deciderId;
            log.DecidedAt = now;
            WastageLogDAL.Update(log, conn, tx);
            AuditLogDAL.Insert(new AuditLog
            {
                UserId = deciderId, Action = approve ? AuditAction.WastageApproved : AuditAction.WastageRejected, EntityType = "WastageLogs", EntityId = log.Id.ToString(),
                Detail = $"{(approve ? "Duyệt" : "Từ chối")} phiếu hủy hàng #{log.Id}: " + string.Join("; ", items.Select(i => $"{i.MaterialName} {Fmt.Decimal(i.Quantity)} {i.Unit}")),
                CreatedAt = now,
            }, conn, tx);
            return log.CinemaId;
        });
    }
}
