using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>Câu mô tả voucher cho web khách: mức giảm, điều kiện, hạn dùng.</summary>
public static class VoucherText
{
    public static string Title(Voucher v) => v.Type switch
    {
        VoucherType.Percent => $"Giảm {v.Value}%",
        VoucherType.FreeTicket => "Tặng 1 vé",
        _ => $"Giảm {Fmt.Money(v.Value)}",
    };

    public static string Condition(Voucher v)
    {
        var parts = new List<string>();
        if (v.MinBillAmount > 0) parts.Add($"Đơn từ {Fmt.Money(v.MinBillAmount)}");
        parts.Add($"Hạn đến {Fmt.Date(v.ValidUntil)}");
        int left = v.UsageLimit - v.UsedCount;
        if (v.OwnerUserId == null) parts.Add($"Còn {Fmt.Number(left)} lượt");
        return string.Join(" · ", parts);
    }

    /// <summary>Trạng thái voucher của khách: còn dùng, đã dùng, hết hạn, ngưng.</summary>
    public static (string Label, string Tone) State(Voucher v, DateTime now) =>
        !v.Active ? ("Ngưng áp dụng", "neutral")
        : v.UsedCount >= v.UsageLimit ? ("Đã dùng", "neutral")
        : v.ValidUntil <= now ? ("Hết hạn", "neutral")
        : v.ValidFrom > now ? ("Chưa tới ngày dùng", "info")
        : ("Còn dùng được", "success");
}
