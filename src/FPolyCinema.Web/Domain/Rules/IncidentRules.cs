using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Tổng kết sau khi hủy suất do sự cố: số hóa đơn, số vé, tổng tiền đã hoàn.</summary>
public sealed record IncidentSummary(int Bills, int Tickets, long Refunded, int Vouchers, int ExpiredWaiting);

/// <summary>
/// Hủy suất do sự cố (SPEC 3.10 mục 2): chọn suất, nhập lý do, gõ lại mã suất để xác nhận. Logic thuần, có unit test.
/// </summary>
public static class IncidentRules
{
    public const int MinReasonLength = 5;

    /// <summary>Mã suất để gõ lại khi xác nhận, dạng "S" + Id (ví dụ S1024).</summary>
    public static string ConfirmCode(int showtimeId) => "S" + showtimeId;

    /// <summary>Mã gõ lại khớp mã suất (không phân biệt hoa thường, bỏ khoảng trắng).</summary>
    public static bool ConfirmMatches(int showtimeId, string? typed) =>
        string.Equals((typed ?? string.Empty).Trim(), ConfirmCode(showtimeId), StringComparison.OrdinalIgnoreCase);

    /// <summary>Lỗi theo ô (rỗng khi hợp lệ): lý do ít nhất 5 ký tự, mã xác nhận khớp, suất chưa hủy và chưa dọn xong.</summary>
    public static Dictionary<string, string> Validate(int showtimeId, string operationalStatus, string? reason, string? typedCode)
    {
        var errors = new Dictionary<string, string>();
        if (operationalStatus is OperationalStatus.Cancelled) errors["showtimeId"] = "Suất này đã bị hủy trước đó.";
        else if (operationalStatus is OperationalStatus.Completed) errors["showtimeId"] = "Suất này đã chiếu xong, không hủy được.";
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < MinReasonLength) errors["reason"] = $"Bạn ghi lý do hủy suất (ít nhất {MinReasonLength} ký tự).";
        if (!ConfirmMatches(showtimeId, typedCode)) errors["confirmCode"] = $"Bạn gõ lại đúng mã suất {ConfirmCode(showtimeId)} để xác nhận.";
        return errors;
    }
}
