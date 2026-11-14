using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Yêu cầu duyệt từ xa (SPEC 3.6-21, 3.6-22): thu ngân gửi yêu cầu thay cho nhập PIN; quản lý duyệt hoặc từ chối trên dashboard; yêu cầu hết hạn
/// sau ApprovalRequestExpireMinutes. Logic thuần, có unit test.
/// </summary>
public static class ApprovalRules
{
    /// <summary>Hạn của yêu cầu mới.</summary>
    public static DateTime ExpiresAt(DateTime createdAt, int expireMinutes) => createdAt.AddMinutes(expireMinutes);

    /// <summary>Yêu cầu còn chờ duyệt: trạng thái PENDING và chưa tới hạn.</summary>
    public static bool IsPending(string status, DateTime expiresAt, DateTime now) => status == ApprovalStatus.Pending && now < expiresAt;

    /// <summary>Trạng thái hiệu lực khi đọc: PENDING quá hạn coi là EXPIRED.</summary>
    public static string Effective(string status, DateTime expiresAt, DateTime now) =>
        status == ApprovalStatus.Pending && now >= expiresAt ? ApprovalStatus.Expired : status;

    /// <summary>Lý do không quyết định được (null khi được): yêu cầu đã được xử lý hoặc đã hết hạn.</summary>
    public static string? CannotDecideReason(string status, DateTime expiresAt, DateTime now) => Effective(status, expiresAt, now) switch
    {
        ApprovalStatus.Pending => null,
        ApprovalStatus.Expired => "Yêu cầu đã hết hạn. Thu ngân cần gửi lại yêu cầu.",
        ApprovalStatus.Approved => "Yêu cầu đã được duyệt trước đó.",
        _ => "Yêu cầu đã bị từ chối trước đó.",
    };

    /// <summary>Người duyệt hợp lệ: ShiftManager của đúng rạp, hoặc Manager của rạp đó hay toàn chuỗi.</summary>
    public static bool CanApprove(string role, int? approverCinemaId, int billCinemaId) =>
        (role == Roles.ShiftManager && approverCinemaId == billCinemaId) || (role == Roles.Manager && (approverCinemaId == null || approverCinemaId == billCinemaId));
}
