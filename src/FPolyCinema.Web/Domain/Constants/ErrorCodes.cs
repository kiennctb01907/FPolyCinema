namespace FPolyCinema.Web.Domain.Constants;

/// <summary>
/// Mã lỗi trả cho giao diện (CONTRACTS mục 8). Mỗi phase thêm các mã của mình.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Chuyển trạng thái hóa đơn không có trong bảng CONTRACTS mục 5 (HTTP 409).</summary>
    public const string InvalidTransition = "INVALID_TRANSITION";

    /// <summary>Mã HTTP của từng mã lỗi; mặc định 422 (vi phạm quy tắc nghiệp vụ).</summary>
    public static int HttpStatusOf(string code) => code switch
    {
        InvalidTransition => 409,
        _ => 422,
    };
}
