namespace FPolyCinema.Web.Domain.Constants;

/// <summary>Loại voucher (cột Vouchers.Type).</summary>
public static class VoucherType
{
    public const string Amount = "AMOUNT";
    public const string Percent = "PERCENT";
    public const string FreeTicket = "FREE_TICKET";

    public static readonly string[] All = { Amount, Percent, FreeTicket };
}

/// <summary>Nguồn phát hành voucher (cột Vouchers.Source).</summary>
public static class VoucherSource
{
    public const string Promo = "PROMO";
    public const string Compensation = "COMPENSATION";
    public const string Corporate = "CORPORATE";

    public static readonly string[] All = { Promo, Compensation, Corporate };
}

/// <summary>Hạng thành viên (cột Users.Tier).</summary>
public static class LoyaltyTier
{
    public const string Member = "MEMBER";
    public const string Vip = "VIP";
    public const string Vvip = "VVIP";

    public static readonly string[] All = { Member, Vip, Vvip };
}
