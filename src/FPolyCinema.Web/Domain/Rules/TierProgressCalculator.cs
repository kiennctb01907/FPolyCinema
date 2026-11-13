using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Tiến độ tới hạng kế tiếp (UI C5): hạng kế tiếp, mức chi tiêu cần đạt, phần trăm đã đạt. Logic thuần, có unit test.</summary>
public sealed record TierProgress(string Tier, string? NextTier, long Spend, long NextThreshold, int Percent);

public static class TierProgressCalculator
{
    public static TierProgress Of(long spend12Months, long vipThreshold, long vvipThreshold)
    {
        if (spend12Months >= vvipThreshold) return new TierProgress(LoyaltyTier.Vvip, null, spend12Months, vvipThreshold, 100);
        var (tier, next, threshold) = spend12Months >= vipThreshold ? (LoyaltyTier.Vip, LoyaltyTier.Vvip, vvipThreshold) : (LoyaltyTier.Member, LoyaltyTier.Vip, vipThreshold);
        int pct = threshold <= 0 ? 100 : (int)Math.Clamp(spend12Months * 100 / threshold, 0, 100);
        return new TierProgress(tier, next, spend12Months, threshold, pct);
    }
}
