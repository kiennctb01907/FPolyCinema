namespace FPolyCinema.Web.Domain.Constants;

/// <summary>Trạng thái hóa đơn (cột Bills.Status), chữ thường đúng slide bài 6.</summary>
public static class BillStatus
{
    public const string Waiting = "waiting";
    public const string Finish = "finish";
    public const string Cancel = "cancel";

    public static readonly string[] All = { Waiting, Finish, Cancel };
}

/// <summary>Lý do hủy hóa đơn (cột Bills.CancelReason).</summary>
public static class CancelReason
{
    public const string Expired = "EXPIRED";
    public const string CancelledBeforePayment = "CANCELLED_BEFORE_PAYMENT";
    public const string CancelledByUser = "CANCELLED_BY_USER";
    public const string VoidedByPos = "VOIDED_BY_POS";
    public const string OverbookedRefunded = "OVERBOOKED_REFUNDED";
    public const string IncidentCompensated = "INCIDENT_COMPENSATED";

    public static readonly string[] All = { Expired, CancelledBeforePayment, CancelledByUser, VoidedByPos, OverbookedRefunded, IncidentCompensated };
}

/// <summary>Kênh bán (cột Bills.Channel).</summary>
public static class BillChannel
{
    public const string Pos = "POS";
    public const string Online = "ONLINE";
    public const string Corporate = "CORPORATE";

    public static readonly string[] All = { Pos, Online, Corporate };
}

/// <summary>Trạng thái vào cửa của cả hóa đơn (cột Bills.CheckInStatus). NONE: hóa đơn không có vé.</summary>
public static class BillCheckInStatus
{
    public const string None = "NONE";
    public const string NotCheckedIn = "NOT_CHECKED_IN";
    public const string PartiallyCheckedIn = "PARTIALLY_CHECKED_IN";
    public const string FullyCheckedIn = "FULLY_CHECKED_IN";
    public const string Voided = "VOIDED";

    public static readonly string[] All = { None, NotCheckedIn, PartiallyCheckedIn, FullyCheckedIn, Voided };
}

/// <summary>Trạng thái vào cửa của từng vé (cột Tickets.CheckInStatus).</summary>
public static class TicketCheckInStatus
{
    public const string NotCheckedIn = "NOT_CHECKED_IN";
    public const string CheckedIn = "CHECKED_IN";
    public const string Swapped = "SWAPPED";
    public const string Voided = "VOIDED";

    public static readonly string[] All = { NotCheckedIn, CheckedIn, Swapped, Voided };
}

/// <summary>Trạng thái bắp nước của hóa đơn (cột Bills.FnbStatus). NONE: hóa đơn không có bắp nước.</summary>
public static class FnbStatus
{
    public const string None = "NONE";
    public const string PendingPickup = "PENDING_PICKUP";
    public const string Preparing = "PREPARING";
    public const string ReadyForPickup = "READY_FOR_PICKUP";
    public const string Fulfilled = "FULFILLED";
    public const string Voided = "VOIDED";

    public static readonly string[] All = { None, PendingPickup, Preparing, ReadyForPickup, Fulfilled, Voided };
}

/// <summary>Phương thức thanh toán (cột Payments.Method). Voucher và điểm là giảm giá, không phải phương thức.</summary>
public static class PaymentMethod
{
    public const string Cash = "CASH";
    public const string VietQrSim = "VIETQR_SIM";
    public const string Wallet = "WALLET";

    public static readonly string[] All = { Cash, VietQrSim, Wallet };
}

/// <summary>Chiều dòng tiền (cột Payments.Direction).</summary>
public static class PaymentDirection
{
    public const string In = "IN";
    public const string Refund = "REFUND";

    public static readonly string[] All = { In, Refund };
}

/// <summary>Trạng thái giao dịch thanh toán (cột Payments.Status).</summary>
public static class PaymentStatus
{
    public const string Pending = "PENDING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";

    public static readonly string[] All = { Pending, Succeeded, Failed };
}

/// <summary>
/// Đối tượng vé (CONTRACTS mục 4, cột Tickets.AudienceType và AudienceDiscounts.AudienceType).
/// Mức giảm lưu ở bảng AudienceDiscounts.
/// </summary>
public static class AudienceType
{
    public const string Adult = "ADULT";
    public const string U22 = "U22";
    public const string ChildU13 = "CHILD_U13";
    public const string Child13To15 = "CHILD_13_15";
    public const string Senior = "SENIOR";
    public const string Merit = "MERIT";
    public const string Hardship = "HARDSHIP";
    public const string DisabledSevere = "DISABLED_SEVERE";
    public const string DisabledExtreme = "DISABLED_EXTREME";

    public static readonly string[] All = { Adult, U22, ChildU13, Child13To15, Senior, Merit, Hardship, DisabledSevere, DisabledExtreme };
}
