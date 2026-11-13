using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Kết quả đóng ca mù: số đếm được, số hệ thống tính, chênh lệch (dương là thừa, âm là thiếu).</summary>
public sealed record CashDropResult(long Counted, long Expected, long Discrepancy)
{
    public bool IsOver => Discrepancy > 0;
    public bool IsShort => Discrepancy < 0;
}

/// <summary>
/// Đóng ca mù (SPEC 3.6): nhân viên đếm tiền theo mệnh giá và nhập tổng, không thấy số hệ thống tính; sau khi xác nhận mới tính
/// ExpectedCash = OpeningCashFloat + tiền mặt thu − tiền mặt hoàn trong ca, Discrepancy = tiền thực đếm − ExpectedCash. Logic thuần, có unit test.
/// </summary>
public static class CashDropCalculator
{
    /// <summary>Mệnh giá tiền Việt Nam dùng khi đếm két (UI P7).</summary>
    public static readonly long[] Denominations = { 500_000, 200_000, 100_000, 50_000, 20_000, 10_000, 5_000, 2_000, 1_000 };

    /// <summary>Tổng tiền đếm được theo mệnh giá. Ném ArgumentException khi có mệnh giá lạ hoặc số tờ âm.</summary>
    public static long CountTotal(IReadOnlyDictionary<long, int> counts)
    {
        long total = 0;
        foreach (var (denom, n) in counts)
        {
            if (Array.IndexOf(Denominations, denom) < 0) throw new ArgumentException($"Mệnh giá {Fmt.Money(denom)} không có trong danh sách.");
            if (n < 0) throw new ArgumentException("Số tờ phải từ 0 trở lên.");
            total += denom * n;
        }
        return total;
    }

    /// <summary>Số tiền mặt hệ thống tính phải có trong két.</summary>
    public static long Expected(long openingCashFloat, long cashIn, long cashRefund) => openingCashFloat + cashIn - cashRefund;

    /// <summary>Tính biên bản đóng ca.</summary>
    public static CashDropResult Close(long counted, long openingCashFloat, long cashIn, long cashRefund)
    {
        var expected = Expected(openingCashFloat, cashIn, cashRefund);
        return new CashDropResult(counted, expected, counted - expected);
    }
}

/// <summary>Kết quả kiểm PIN quản lý.</summary>
public sealed record PinAttempt(bool Ok, int FailedCount, DateTime? LockedUntil, int Remaining);

/// <summary>
/// PIN quản lý (SPEC 2.5): 6 chữ số, lưu dạng băm; sai ManagerPinMaxAttempts lần thì khóa ManagerPinLockMinutes phút. Logic thuần, có unit test.
/// </summary>
public static class ManagerPinRules
{
    public const int Length = 6;

    /// <summary>PIN đúng định dạng 6 chữ số.</summary>
    public static bool IsWellFormed(string? pin) => pin != null && pin.Length == Length && pin.All(char.IsAsciiDigit);

    /// <summary>PIN đang bị khóa.</summary>
    public static bool IsLocked(DateTime? lockedUntil, DateTime now) => lockedUntil != null && lockedUntil > now;

    /// <summary>Trạng thái sau một lần nhập: đúng thì xóa đếm sai; sai thì tăng đếm, đủ số lần thì khóa và đặt lại đếm.</summary>
    public static PinAttempt After(bool correct, int failedCount, DateTime now, CinemaRules rules)
    {
        if (correct) return new PinAttempt(true, 0, null, rules.ManagerPinMaxAttempts);
        int failed = failedCount + 1;
        if (failed >= rules.ManagerPinMaxAttempts)
            return new PinAttempt(false, 0, now.AddMinutes(rules.ManagerPinLockMinutes), 0);
        return new PinAttempt(false, failed, null, rules.ManagerPinMaxAttempts - failed);
    }
}

/// <summary>Một nhóm vé cùng giá khi đối soát: giá cuối (gồm VAT), thuế suất, số vé.</summary>
public sealed record SettlementLine(long FinalPrice, decimal VatRate, int Count);

/// <summary>Kết quả đối soát nhà phát hành (khớp sp_box_office_settlement).</summary>
public sealed record SettlementResult(int TicketCount, long GrossRevenue, long NetRevenue, int BelowFloorTickets, long FloorAbsorbedAmount,
    long BaseTotal, long DistributorAmount, long CinemaRetainedAmount);

/// <summary>
/// Đối soát nhà phát hành (SPEC 3.12): Net = FinalPrice × 100 / (100 + VatRate); FloorNet = MinimumTicketPrice × 100 / (100 + VatRate);
/// Base = max(Net, FloorNet); tiền trả nhà phát hành = làm tròn(ΣBase × tỷ lệ / 100); phần rạp tự chịu = Σ(FloorNet − Net) của vé dưới sàn;
/// tiền rạp giữ lại = doanh thu thuần − tiền trả nhà phát hành. Logic thuần, có unit test; cùng công thức với stored procedure.
/// </summary>
public static class SettlementCalculator
{
    public static decimal Net(long finalPrice, decimal vatRate) => finalPrice * 100m / (100m + vatRate);

    public static decimal FloorNet(long minimumTicketPrice, decimal vatRate) => minimumTicketPrice * 100m / (100m + vatRate);

    public static SettlementResult Compute(IEnumerable<SettlementLine> lines, long minimumTicketPrice, decimal distributorSharePercent)
    {
        int count = 0, below = 0;
        long gross = 0;
        decimal net = 0, absorbed = 0, baseTotal = 0;
        foreach (var l in lines)
        {
            var n = Net(l.FinalPrice, l.VatRate);
            var f = FloorNet(minimumTicketPrice, l.VatRate);
            count += l.Count;
            gross += l.FinalPrice * l.Count;
            net += n * l.Count;
            if (n < f)
            {
                below += l.Count;
                absorbed += (f - n) * l.Count;
            }
            baseTotal += Math.Max(n, f) * l.Count;
        }
        long R(decimal x) => (long)Math.Round(x, 0, MidpointRounding.AwayFromZero);
        long distributor = R(baseTotal * distributorSharePercent / 100m);
        long netRounded = R(net);
        return new SettlementResult(count, gross, netRounded, below, R(absorbed), R(baseTotal), distributor, netRounded - distributor);
    }
}

/// <summary>
/// Kho theo định mức (SPEC 3.11): khả dụng = ActualStock − ReservedStock; một món bán được n phần khi mọi nguyên liệu có khả dụng ≥ n × định mức.
/// Logic thuần, có unit test.
/// </summary>
public static class StockCalculator
{
    /// <summary>Số phần tối đa bán được của một món; món không có định mức thì không giới hạn (int.MaxValue).</summary>
    public static int MaxPortions(IEnumerable<(decimal PerPortion, decimal Available)> recipe)
    {
        int max = int.MaxValue;
        foreach (var (per, available) in recipe)
        {
            if (per <= 0) continue;
            var n = available <= 0 ? 0 : (int)Math.Min(int.MaxValue, Math.Floor(available / per));
            max = Math.Min(max, n);
        }
        return max;
    }

    /// <summary>Đủ nguyên liệu cho n phần.</summary>
    public static bool CanSell(IEnumerable<(decimal PerPortion, decimal Available)> recipe, int portions) => portions <= MaxPortions(recipe);
}
