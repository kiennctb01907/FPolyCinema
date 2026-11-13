using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Điều khách phải tích xác nhận trước khi thanh toán online, kèm câu ghi bên cạnh ô tích.</summary>
public sealed record OnlineConfirmation(string Code, string Text);

/// <summary>
/// Quy tắc độ tuổi và giờ chiếu khi bán online (SPEC 3.1). Logic thuần, có unit test.
/// - Online không bán vé trẻ em và các loại vé giảm theo luật (chỉ bán tại quầy để kiểm giấy tờ).
/// - Tài khoản có ngày sinh mà chưa đủ tuổi theo nhãn phim thì chặn (AGE_RESTRICTED).
/// - Phim T13 trở lên luôn yêu cầu tích "Tôi xác nhận người xem đủ tuổi theo quy định" (AGE_CONFIRM_REQUIRED).
/// - Phim K: hiện lưu ý và yêu cầu tích xác nhận có cha mẹ hoặc người giám hộ đi cùng trẻ dưới 13 tuổi.
/// - Suất kết thúc từ ChildU13LatestEnd: lưu ý "Trẻ em dưới 13 tuổi không vào được suất này"; từ ChildU16LatestEnd: "dưới 16 tuổi".
/// </summary>
public static class OnlineAgeRules
{
    public const string AgeConfirmText = "Tôi xác nhận người xem đủ tuổi theo quy định";

    /// <summary>Lưu ý giờ chiếu cho trẻ em của suất (null khi suất kết thúc trước ChildU13LatestEnd).</summary>
    public static string? CurfewNotice(DateTime startTime, DateTime endTime, TimeSpan childU13LatestEnd, TimeSpan childU16LatestEnd)
    {
        if (!AgeRules.EndsBefore(startTime, endTime, childU16LatestEnd)) return "Trẻ em dưới 16 tuổi không vào được suất này";
        if (!AgeRules.EndsBefore(startTime, endTime, childU13LatestEnd)) return "Trẻ em dưới 13 tuổi không vào được suất này";
        return null;
    }

    /// <summary>Các ô cần tích trước khi thanh toán: xác nhận đủ tuổi (T13+ hoặc suất có giới hạn giờ cho trẻ em), người giám hộ (phim K).</summary>
    public static List<OnlineConfirmation> Confirmations(string rating, DateTime startTime, DateTime endTime, TimeSpan childU13LatestEnd, TimeSpan childU16LatestEnd)
    {
        var list = new List<OnlineConfirmation>();
        var curfew = CurfewNotice(startTime, endTime, childU13LatestEnd, childU16LatestEnd);
        if (AgeRules.RequiresMinimumAge(rating) || curfew != null)
        {
            var text = AgeConfirmText;
            if (AgeRules.RequiresMinimumAge(rating)) text += $" (phim {rating}: từ đủ {AgeRules.MinimumAge(rating)} tuổi)";
            if (curfew != null) text += $". {curfew}";
            list.Add(new OnlineConfirmation(ErrorCodes.AgeConfirmRequired, text + "."));
        }
        if (rating == AgeRating.K)
            list.Add(new OnlineConfirmation(ErrorCodes.GuardianRequired, "Phim K: trẻ dưới 13 tuổi phải đi cùng cha, mẹ hoặc người giám hộ. Tôi xác nhận trẻ đi cùng người giám hộ."));
        return list;
    }

    /// <summary>Kiểm trước khi thanh toán online. Trả Pass hoặc lỗi đầu tiên (AGE_RESTRICTED, AGE_CONFIRM_REQUIRED, GUARDIAN_REQUIRED).</summary>
    public static AgeCheck Check(string rating, DateTime? birthDate, DateTime now, DateTime startTime, DateTime endTime, bool ageConfirmed, bool guardianConfirmed,
        TimeSpan childU13LatestEnd, TimeSpan childU16LatestEnd)
    {
        if (rating == AgeRating.C) return AgeCheck.Fail(ErrorCodes.AgeRestricted, "Phim nhãn C không được phép phổ biến nên không bán vé.");
        if (birthDate is DateTime bd && !AgeRules.CanWatch(rating, AgeRules.AgeOn(bd, now)))
            return AgeCheck.Fail(ErrorCodes.AgeRestricted,
                $"Phim {rating} dành cho khán giả từ đủ {AgeRules.MinimumAge(rating)} tuổi. Theo ngày sinh trong tài khoản, bạn chưa đủ tuổi xem phim này.");
        foreach (var c in Confirmations(rating, startTime, endTime, childU13LatestEnd, childU16LatestEnd))
        {
            if (c.Code == ErrorCodes.AgeConfirmRequired && !ageConfirmed)
                return AgeCheck.Fail(ErrorCodes.AgeConfirmRequired, $"Bạn tích ô \"{AgeConfirmText}\" trước khi thanh toán.");
            if (c.Code == ErrorCodes.GuardianRequired && !guardianConfirmed)
                return AgeCheck.Fail(ErrorCodes.GuardianRequired, "Phim K: bạn tích xác nhận trẻ dưới 13 tuổi đi cùng cha, mẹ hoặc người giám hộ trước khi thanh toán.");
        }
        return AgeCheck.Pass;
    }
}
