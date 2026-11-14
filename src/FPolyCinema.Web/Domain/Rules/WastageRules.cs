using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một dòng phiếu hủy hàng hoặc phiếu nhập kho do người dùng nhập.</summary>
public sealed record StockLineInput(int RawMaterialId, decimal Quantity, string? Reason);

/// <summary>
/// Nhập kho và hủy hàng hỏng (SPEC 3.11-14, 3.11-15). Logic thuần, có unit test.
/// - Phiếu nhập (Manager, IMPORT) và phiếu hủy (PosStaff lập, PENDING) cần ít nhất một dòng có số lượng dương, mỗi nguyên liệu một dòng.
/// - Phiếu hủy chỉ duyệt hoặc từ chối khi còn PENDING; duyệt thì ActualStock giảm (WASTAGE), không cho xuống dưới phần đã giữ cho đơn online.
/// </summary>
public static class WastageRules
{
    public const decimal MaxQuantity = 1_000_000m;

    /// <summary>Lỗi theo ô (rỗng khi hợp lệ). needReason: phiếu hủy yêu cầu lý do cho từng dòng.</summary>
    public static Dictionary<string, string> ValidateLines(IReadOnlyList<StockLineInput> lines, IReadOnlySet<int> knownMaterialIds, bool needReason)
    {
        var e = new Dictionary<string, string>();
        var used = lines.Where(l => l.Quantity != 0 || !string.IsNullOrWhiteSpace(l.Reason)).ToList();
        if (used.Count == 0) { e["lines"] = "Bạn nhập ít nhất một nguyên liệu với số lượng lớn hơn 0."; return e; }
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (l.Quantity == 0 && string.IsNullOrWhiteSpace(l.Reason)) continue;
            if (!knownMaterialIds.Contains(l.RawMaterialId)) e[$"lines[{i}].RawMaterialId"] = "Nguyên liệu không thuộc rạp này.";
            if (l.Quantity <= 0 || l.Quantity > MaxQuantity) e[$"lines[{i}].Quantity"] = "Số lượng phải lớn hơn 0.";
            if (needReason && string.IsNullOrWhiteSpace(l.Reason)) e[$"lines[{i}].Reason"] = "Bạn ghi lý do hủy (ví dụ: hết hạn, rơi vỡ).";
        }
        if (used.GroupBy(l => l.RawMaterialId).Any(g => g.Count() > 1)) e["lines"] = "Mỗi nguyên liệu chỉ nhập một dòng.";
        return e;
    }

    /// <summary>Chỉ quyết định phiếu PENDING.</summary>
    public static bool CanDecide(string status) => status == WastageStatus.Pending;

    /// <summary>Hủy không làm tồn thực tế thấp hơn phần đang giữ cho đơn online (khả dụng không âm).</summary>
    public static bool Fits(decimal actual, decimal reserved, decimal quantity) => actual - quantity >= reserved;
}
