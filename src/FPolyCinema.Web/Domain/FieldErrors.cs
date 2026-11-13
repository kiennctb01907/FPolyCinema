using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain;

/// <summary>
/// Lỗi dữ liệu theo từng ô (VALIDATION_FAILED, HTTP 400). Details là từ điển tên ô → câu lỗi tiếng Việt,
/// để controller gắn vào ModelState (form thường) hoặc giao diện hiện ngay dưới ô (AJAX).
/// </summary>
public static class FieldErrors
{
    public static BusinessRuleException Of(string field, string message) =>
        new(ErrorCodes.ValidationFailed, message, new Dictionary<string, string> { [field] = message });

    public static BusinessRuleException Of(IDictionary<string, string> errors) =>
        new(ErrorCodes.ValidationFailed, errors.Values.FirstOrDefault() ?? "Dữ liệu chưa hợp lệ.", new Dictionary<string, string>(errors));
}
