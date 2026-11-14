using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Bán vé doanh nghiệp (SPEC 3.10 mục 3). Logic thuần, có unit test.
/// - Thuê trọn suất: chỉ với suất chưa bán vé nào (không vé, không ghế đang giữ), chưa bắt đầu và chưa hủy; suất được đánh dấu IsPrivate.
/// - Phát hành loạt voucher FREE_TICKET: số lượng 1–500, hạn dùng sau thời điểm phát hành; mỗi mã dùng một lần.
/// </summary>
public static class CorporateRules
{
    public const int MaxVoucherBatch = 500;

    /// <summary>Lỗi theo ô khi thuê trọn suất (rỗng khi hợp lệ).</summary>
    public static Dictionary<string, string> ValidatePrivateScreening(string operationalStatus, bool isPrivate, int ticketsSold, int seatsHeld,
        DateTime startTime, DateTime now, long contractAmount)
    {
        var e = new Dictionary<string, string>();
        if (operationalStatus == OperationalStatus.Cancelled) e["ShowtimeId"] = "Suất này đã bị hủy.";
        else if (isPrivate) e["ShowtimeId"] = "Suất này đã được thuê trọn.";
        else if (ticketsSold > 0 || seatsHeld > 0) e["ShowtimeId"] = "Chỉ thuê trọn được suất chưa bán vé nào.";
        else if (now >= startTime) e["ShowtimeId"] = "Suất đã bắt đầu.";
        if (contractAmount <= 0) e["ContractAmount"] = "Bạn nhập giá trị hợp đồng lớn hơn 0.";
        return e;
    }

    /// <summary>Lỗi theo ô khi phát hành loạt voucher (rỗng khi hợp lệ).</summary>
    public static Dictionary<string, string> ValidateVoucherBatch(int? count, DateTime? validUntil, DateTime now, long contractAmount)
    {
        var e = new Dictionary<string, string>();
        if (count is null or < 1 or > MaxVoucherBatch) e["VoucherCount"] = $"Số lượng voucher từ 1 đến {MaxVoucherBatch}.";
        if (validUntil == null || validUntil <= now) e["ValidUntil"] = "Hạn dùng phải sau thời điểm hiện tại.";
        if (contractAmount < 0) e["ContractAmount"] = "Giá trị hợp đồng không được âm.";
        return e;
    }

    /// <summary>Chia giá hợp đồng cho từng ghế (đồng), phần dư dồn vào ghế cuối để tổng đúng bằng giá hợp đồng.</summary>
    public static long[] SplitContract(long contractAmount, int seats)
    {
        if (seats <= 0) return Array.Empty<long>();
        var result = new long[seats];
        long each = contractAmount / seats;
        for (int i = 0; i < seats; i++) result[i] = each;
        result[^1] += contractAmount - each * seats;
        return result;
    }

    /// <summary>FREE_TICKET chỉ trả cho ghế STANDARD hoặc VIP, không áp cho SWEETBOX (đã kiểm ở VoucherRules).</summary>
    public static bool FreeTicketApplies(string seatType) => seatType is SeatType.Standard or SeatType.Vip;
}
