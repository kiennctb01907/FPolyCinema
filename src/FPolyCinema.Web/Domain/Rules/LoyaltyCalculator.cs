using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Điểm và hạng thành viên (SPEC 3.13). Logic thuần, có unit test.
/// - Cộng điểm khi hóa đơn có CustomerId chuyển finish: phần trả bằng tiền mặt, chuyển khoản hoặc ví × PointEarnPercent[hạng] / 100 / PointValueVnd, làm tròn xuống.
/// - Hạng xét lại mỗi lần cộng điểm theo tổng chi tiêu 12 tháng gần nhất (TierVipSpend12Months, TierVvipSpend12Months).
/// - Đổi điểm: 1 điểm = PointValueVnd, tối đa MaxPointsRedeemPercent giá trị hóa đơn (POINTS_INVALID).
/// </summary>
public static class LoyaltyCalculator
{
    /// <summary>Số điểm cộng cho phần đã trả.</summary>
    public static int EarnPoints(long paidAmount, string tier, CinemaRules rules)
    {
        if (paidAmount <= 0 || rules.PointValueVnd <= 0) return 0;
        var pct = rules.PointEarnPercent.TryGetValue(tier, out var p) ? p : 0;
        return (int)(paidAmount * pct / 100 / rules.PointValueVnd);
    }

    /// <summary>Hạng theo tổng chi tiêu 12 tháng gần nhất.</summary>
    public static string TierFor(long spend12Months, CinemaRules rules) =>
        spend12Months >= rules.TierVvipSpend12Months ? LoyaltyTier.Vvip
        : spend12Months >= rules.TierVipSpend12Months ? LoyaltyTier.Vip
        : LoyaltyTier.Member;

    /// <summary>Số tiền quy đổi của một số điểm.</summary>
    public static long PointsValue(int points, CinemaRules rules) => points * rules.PointValueVnd;

    /// <summary>Số điểm tối đa được đổi cho hóa đơn: không quá số dư và không quá MaxPointsRedeemPercent giá trị hóa đơn.</summary>
    public static int MaxRedeemPoints(long billValue, int balance, CinemaRules rules)
    {
        if (billValue <= 0 || rules.PointValueVnd <= 0) return 0;
        var byPercent = billValue * rules.MaxPointsRedeemPercent / 100 / rules.PointValueVnd;
        return (int)Math.Max(0, Math.Min(balance, byPercent));
    }

    /// <summary>Kiểm số điểm muốn đổi; trả null khi hợp lệ, ngược lại là câu báo lỗi (POINTS_INVALID).</summary>
    public static string? ValidateRedeem(int points, int balance, long billValue, CinemaRules rules)
    {
        if (points < 0) return "Số điểm đổi phải từ 0 trở lên.";
        if (points == 0) return null;
        if (points > balance) return $"Thành viên chỉ còn {Fmt.Number(balance)} điểm.";
        var max = MaxRedeemPoints(billValue, balance, rules);
        if (points > max)
            return $"Hóa đơn này đổi được tối đa {Fmt.Number(max)} điểm (không quá {rules.MaxPointsRedeemPercent}% giá trị hóa đơn).";
        return null;
    }
}

/// <summary>Kết quả kiểm voucher: hợp lệ kèm số tiền giảm, hoặc lý do cụ thể (VOUCHER_INVALID).</summary>
public sealed record VoucherCheck(bool Ok, long Discount, string? Reason)
{
    public static VoucherCheck Fail(string reason) => new(false, 0, reason);
}

/// <summary>
/// Kiểm voucher (SPEC 3.13): Active, thời hạn, giá trị đơn tối thiểu, số lượt dùng, chủ sở hữu. Logic thuần, có unit test.
/// AMOUNT giảm Value đồng; PERCENT giảm Value% làm tròn xuống tới 1.000 đồng; FREE_TICKET trả một vé ghế STANDARD hoặc VIP (vé đắt nhất), không áp cho SWEETBOX.
/// Số tiền giảm không vượt giá trị hóa đơn.
/// </summary>
public static class VoucherRules
{
    /// <param name="ticketPrices">Giá niêm yết và loại ghế của từng vé (dùng cho FREE_TICKET).</param>
    public static VoucherCheck Check(Voucher v, DateTime now, long subtotal, int? customerId, IEnumerable<(long ListPrice, string SeatType)> ticketPrices)
    {
        if (!v.Active) return VoucherCheck.Fail($"Voucher {v.Code} đã ngưng sử dụng.");
        if (now < v.ValidFrom) return VoucherCheck.Fail($"Voucher {v.Code} chỉ dùng được từ {Fmt.DateTime(v.ValidFrom)}.");
        if (now > v.ValidUntil) return VoucherCheck.Fail($"Voucher {v.Code} đã hết hạn lúc {Fmt.DateTime(v.ValidUntil)}.");
        if (v.UsedCount >= v.UsageLimit) return VoucherCheck.Fail($"Voucher {v.Code} đã hết lượt dùng.");
        if (v.OwnerUserId != null && v.OwnerUserId != customerId)
            return VoucherCheck.Fail($"Voucher {v.Code} là voucher riêng của một thành viên. Bạn tra cứu đúng thành viên sở hữu voucher trước.");
        if (subtotal < v.MinBillAmount)
            return VoucherCheck.Fail($"Voucher {v.Code} áp dụng cho hóa đơn từ {Fmt.Money(v.MinBillAmount)}. Hóa đơn hiện có {Fmt.Money(subtotal)}.");

        long discount;
        switch (v.Type)
        {
            case VoucherType.Amount:
                discount = v.Value;
                break;
            case VoucherType.Percent:
                discount = subtotal * v.Value / 100 / 1000 * 1000;
                break;
            case VoucherType.FreeTicket:
                var eligible = ticketPrices.Where(t => t.SeatType is SeatType.Standard or SeatType.Vip).Select(t => t.ListPrice).ToList();
                if (eligible.Count == 0) return VoucherCheck.Fail($"Voucher {v.Code} đổi một vé ghế thường hoặc VIP. Hóa đơn chưa có vé loại này.");
                discount = eligible.Max();
                break;
            default:
                return VoucherCheck.Fail($"Voucher {v.Code} không dùng được.");
        }
        discount = Math.Min(discount, subtotal);
        if (discount <= 0) return VoucherCheck.Fail($"Voucher {v.Code} không giảm được gì cho hóa đơn này.");
        return new VoucherCheck(true, discount, null);
    }
}

/// <summary>
/// Phân bổ giảm giá cấp hóa đơn (voucher, điểm) cho từng vé và từng dòng bắp nước theo tỷ lệ giá trị, phần dư dồn vào dòng cuối (SPEC 3.4).
/// Logic thuần, có unit test.
/// </summary>
public static class DiscountAllocator
{
    /// <summary>Trả số tiền giảm của từng dòng; tổng đúng bằng min(discount, tổng giá trị), không dòng nào giảm quá giá trị của nó.</summary>
    public static long[] Allocate(long discount, IReadOnlyList<long> amounts)
    {
        var result = new long[amounts.Count];
        long total = amounts.Sum();
        if (discount <= 0 || total <= 0 || amounts.Count == 0) return result;
        discount = Math.Min(discount, total);
        long used = 0;
        for (int i = 0; i < amounts.Count - 1; i++)
        {
            result[i] = (long)((decimal)discount * amounts[i] / total);
            used += result[i];
        }
        long rest = discount - used;
        // Phần dư dồn vào dòng cuối; nếu dòng cuối không đủ thì dồn ngược lên các dòng trước
        for (int i = amounts.Count - 1; i >= 0 && rest > 0; i--)
        {
            long room = amounts[i] - result[i];
            long add = Math.Min(room, rest);
            result[i] += add;
            rest -= add;
        }
        return result;
    }
}
