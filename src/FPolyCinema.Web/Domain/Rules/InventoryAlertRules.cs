namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Tồn của một nguyên liệu tại một thời điểm.</summary>
public sealed record MaterialLevel(int Id, string Name, string Unit, decimal Actual, decimal Reserved, decimal MinThreshold)
{
    public decimal Available => Actual - Reserved;
}

/// <summary>Một dòng định mức tại rạp: món, nguyên liệu, lượng cho một phần, khả dụng của nguyên liệu.</summary>
public sealed record RecipeStock(int DrinkId, int RawMaterialId, decimal PerPortion, decimal Available);

/// <summary>Kết quả so sánh tồn kho trước và sau một giao dịch kho tại một rạp.</summary>
public sealed record InventoryAlerts(List<int> SoldOut, List<int> Available, List<MaterialLevel> Low);

/// <summary>
/// Sau mỗi giao dịch kho, InventoryService tính lại các món liên quan (SPEC 3.11): món không còn đủ một phần thì báo PRODUCT_SOLD_OUT (IsSoldOut = 1);
/// nhập hàng làm món khả dụng lại thì báo PRODUCT_AVAILABLE; nguyên liệu xuống dưới MinThreshold thì báo STOCK_LOW. Logic thuần, có unit test.
/// </summary>
public static class InventoryAlertRules
{
    /// <summary>Nguyên liệu dưới ngưỡng: tồn thực tế nhỏ hơn MinThreshold (cùng cách tính với cảnh báo ở dashboard).</summary>
    public static bool IsLow(MaterialLevel m) => m.Actual < m.MinThreshold;

    /// <summary>Số phần làm được của từng món có định mức tại rạp (StockCalculator.MaxPortions), không xét cờ IsSoldOut.</summary>
    public static Dictionary<int, int> Portions(IEnumerable<RecipeStock> lines) =>
        lines.GroupBy(l => l.DrinkId).ToDictionary(g => g.Key, g => StockCalculator.MaxPortions(g.Select(l => (l.PerPortion, l.Available))));

    /// <summary>
    /// Món từ đủ một phần trở lên xuống dưới một phần → SoldOut; món từ dưới một phần lên đủ một phần → Available.
    /// Nguyên liệu vừa xuống dưới ngưỡng (trước chưa dưới) → Low.
    /// </summary>
    public static InventoryAlerts Evaluate(IReadOnlyDictionary<int, int> portionsBefore, IReadOnlyDictionary<int, int> portionsAfter,
        IReadOnlyCollection<MaterialLevel> before, IReadOnlyCollection<MaterialLevel> after)
    {
        var soldOut = new List<int>();
        var available = new List<int>();
        foreach (var (drinkId, n) in portionsAfter)
        {
            int old = portionsBefore.TryGetValue(drinkId, out var p) ? p : int.MaxValue;
            if (old >= 1 && n < 1) soldOut.Add(drinkId);
            else if (old < 1 && n >= 1) available.Add(drinkId);
        }
        var wasLow = before.Where(IsLow).Select(m => m.Id).ToHashSet();
        var low = after.Where(m => IsLow(m) && !wasLow.Contains(m.Id)).ToList();
        return new InventoryAlerts(soldOut, available, low);
    }

    /// <summary>Cờ IsSoldOut chung của món: tạm hết khi mọi rạp có định mức của món đều không làm được một phần.</summary>
    public static bool SoldOutEverywhere(IEnumerable<int> portionsPerCinema)
    {
        var list = portionsPerCinema.ToList();
        return list.Count > 0 && list.All(n => n < 1);
    }
}
