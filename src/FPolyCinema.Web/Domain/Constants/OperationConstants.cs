namespace FPolyCinema.Web.Domain.Constants;

/// <summary>Trạng thái ca bán hàng (cột PosShifts.Status).</summary>
public static class ShiftStatus
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";

    public static readonly string[] All = { Open, Closed };
}

/// <summary>Loại yêu cầu phê duyệt (cột SupervisorApprovals.Type).</summary>
public static class ApprovalType
{
    public const string VoidBill = "VOID_BILL";

    public static readonly string[] All = { VoidBill };
}

/// <summary>Trạng thái yêu cầu phê duyệt (cột SupervisorApprovals.Status).</summary>
public static class ApprovalStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Expired = "EXPIRED";

    public static readonly string[] All = { Pending, Approved, Rejected, Expired };
}

/// <summary>Cách duyệt (cột SupervisorApprovals.Method).</summary>
public static class ApprovalMethod
{
    public const string Pin = "PIN";
    public const string Remote = "REMOTE";

    public static readonly string[] All = { Pin, Remote };
}

/// <summary>Trạng thái phiếu hủy hàng (cột WastageLogs.Status).</summary>
public static class WastageStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    public static readonly string[] All = { Pending, Approved, Rejected };
}

/// <summary>Loại giao dịch kho (cột InventoryTransactions.Type).</summary>
public static class InventoryTxnType
{
    public const string Import = "IMPORT";
    public const string Reserve = "RESERVE";
    public const string Release = "RELEASE";
    public const string Consume = "CONSUME";
    public const string Wastage = "WASTAGE";
    public const string Adjust = "ADJUST";

    public static readonly string[] All = { Import, Reserve, Release, Consume, Wastage, Adjust };
}

/// <summary>Loại hợp đồng doanh nghiệp (cột CorporateBookings.Type).</summary>
public static class CorporateType
{
    public const string PrivateScreening = "PRIVATE_SCREENING";
    public const string VoucherBatch = "VOUCHER_BATCH";

    public static readonly string[] All = { PrivateScreening, VoucherBatch };
}
