using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain;

/// <summary>
/// Lỗi vi phạm quy tắc nghiệp vụ, mang mã lỗi theo CONTRACTS mục 8 và câu tiếng Việt cho người dùng.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message, object? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }

    /// <summary>Mã lỗi, ví dụ INVALID_TRANSITION.</summary>
    public string Code { get; }

    /// <summary>Thông tin thêm gửi về giao diện (có thể null).</summary>
    public object? Details { get; }

    /// <summary>Mã HTTP tương ứng với mã lỗi.</summary>
    public int HttpStatus => ErrorCodes.HttpStatusOf(Code);
}
