using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Kết quả kiểm tra một quy tắc độ tuổi: Ok, hoặc mã lỗi (CONTRACTS mục 8) kèm câu báo lỗi theo UI mục 9.</summary>
public sealed record AgeCheck(bool Ok, string? ErrorCode = null, string? Message = null)
{
    public static readonly AgeCheck Pass = new(true);
    public static AgeCheck Fail(string code, string message) => new(false, code, message);
}

/// <summary>
/// Quy tắc pháp lý về độ tuổi và giờ chiếu (SPEC 3.1; Luật Điện ảnh 2022 Điều 32, Nghị định 131/2022 Điều 9 khoản 4). Logic thuần, có unit test.
/// - Phim chỉ được tạo suất khi có số giấy phép phân loại và nhãn khác C (LICENSE_MISSING, RATING_C).
/// - T13, T16, T18 yêu cầu người xem đủ tuổi: vé CHILD_U13 không bán cho phim T13 trở lên, vé CHILD_13_15 không bán cho T16, T18 (AGE_RESTRICTED).
/// - Phim K: trẻ dưới 13 tuổi phải có cha mẹ hoặc người giám hộ đi cùng (GUARDIAN_REQUIRED).
/// - Giờ chiếu cho trẻ em áp theo tuổi người xem: CHILD_U13 chỉ xem suất kết thúc trước ChildU13LatestEnd,
///   CHILD_13_15 chỉ xem suất kết thúc trước ChildU16LatestEnd (CHILD_CURFEW).
/// - Suất bắt đầu trước 06:00 hoặc kết thúc sang ngày hôm sau luôn là suất khuya.
/// - Khi xếp lịch, phim K kết thúc từ 22:00 hoặc phim T13 kết thúc từ 23:00 chỉ bị cảnh báo, không bị chặn.
/// </summary>
public static class AgeRules
{
    /// <summary>Mốc giờ: suất bắt đầu trước giờ này thuộc ngày vận hành hôm trước và luôn là suất khuya.</summary>
    public static readonly TimeSpan NightCutoff = PricingCalculator.NightCutoff;

    /// <summary>Tuổi tối thiểu của người xem theo nhãn (Điều 32 Luật Điện ảnh): T13 → 13, T16 → 16, T18 → 18, còn lại 0.</summary>
    public static int MinimumAge(string? rating) => rating switch
    {
        AgeRating.T13 => 13,
        AgeRating.T16 => 16,
        AgeRating.T18 => 18,
        _ => 0,
    };

    /// <summary>Nhãn T13, T16, T18: người xem phải đủ tuổi.</summary>
    public static bool RequiresMinimumAge(string? rating) => MinimumAge(rating) > 0;

    /// <summary>Tuổi tròn của một người tại một ngày.</summary>
    public static int AgeOn(DateTime birthDate, DateTime date)
    {
        int age = date.Year - birthDate.Year;
        if (date.Date < birthDate.Date.AddYears(age)) age--;
        return Math.Max(age, 0);
    }

    /// <summary>Người xem ở tuổi này có được xem phim nhãn này không. Phim C không ai được xem.</summary>
    public static bool CanWatch(string? rating, int viewerAge) => rating != AgeRating.C && viewerAge >= MinimumAge(rating);

    /// <summary>
    /// Phim có được tạo suất chiếu không (SPEC 3.1 mục 1): phim C chỉ lưu hồ sơ (RATING_C); phim chưa có số giấy phép
    /// phân loại thì chưa được chiếu (LICENSE_MISSING).
    /// </summary>
    public static AgeCheck CanSchedule(string? rating, string? classificationLicenseNo)
    {
        if (rating == AgeRating.C)
            return AgeCheck.Fail(ErrorCodes.RatingC, "Phim nhãn C không được phép phổ biến nên chỉ lưu hồ sơ, không tạo suất chiếu được.");
        if (string.IsNullOrWhiteSpace(classificationLicenseNo))
            return AgeCheck.Fail(ErrorCodes.LicenseMissing, "Phim chưa có số Giấy phép phân loại phim nên chưa tạo suất được. Bạn nhập số giấy phép ở hồ sơ phim trước.");
        return AgeCheck.Pass;
    }

    /// <summary>Suất khuya: bắt đầu trước 06:00 hoặc kết thúc sang ngày hôm sau (SPEC 3.1 mục 4).</summary>
    public static bool IsLateShow(DateTime startTime, DateTime endTime) =>
        startTime.TimeOfDay < NightCutoff || endTime.Date > startTime.Date;

    /// <summary>Suất kết thúc trước mốc giờ trong cùng ngày; suất khuya không bao giờ được coi là kết thúc trước mốc.</summary>
    public static bool EndsBefore(DateTime startTime, DateTime endTime, TimeSpan latestEnd) =>
        !IsLateShow(startTime, endTime) && endTime.TimeOfDay < latestEnd;

    /// <summary>Giờ kết thúc để ghi trong lời nhắn, ví dụ "22:35" hoặc "00:20 ngày hôm sau".</summary>
    public static string EndText(DateTime startTime, DateTime endTime) =>
        Fmt.Time(endTime) + (endTime.Date > startTime.Date ? " ngày hôm sau" : string.Empty);

    /// <summary>
    /// Kiểm tra một vé theo đối tượng (SPEC 3.1 mục 2 và mục 4): AGE_RESTRICTED khi vé trẻ em không hợp nhãn phim,
    /// CHILD_CURFEW khi suất kết thúc từ mốc giờ của độ tuổi đó trở đi (hoặc là suất khuya).
    /// </summary>
    public static AgeCheck CheckTicket(string audienceType, string rating, DateTime startTime, DateTime endTime, CinemaRules rules)
    {
        bool under13 = audienceType == AudienceType.ChildU13;
        bool age13To15 = audienceType == AudienceType.Child13To15;
        if (!under13 && !age13To15) return AgeCheck.Pass;

        int childMaxAge = under13 ? 12 : 15;
        if (MinimumAge(rating) > childMaxAge || rating == AgeRating.C)
        {
            var min = MinimumAge(rating);
            return AgeCheck.Fail(ErrorCodes.AgeRestricted, rating == AgeRating.C
                ? "Phim nhãn C không được phép phổ biến nên không bán vé."
                : min <= 15
                    // Phim T13: vé trẻ em từ 13 đến dưới 16 tuổi vẫn dùng được, nên nói rõ chỉ vé dưới 13 tuổi bị chặn
                    ? $"Phim {rating} dành cho khán giả từ đủ {min} tuổi. Vé trẻ em dưới 13 tuổi không áp dụng cho phim này."
                    : $"Phim {rating} dành cho khán giả từ đủ {min} tuổi. Vé trẻ em không áp dụng cho phim này.");
        }

        var latest = under13 ? rules.ChildU13LatestEnd : rules.ChildU16LatestEnd;
        if (!EndsBefore(startTime, endTime, latest))
        {
            var who = under13 ? "trẻ em dưới 13 tuổi" : "trẻ em dưới 16 tuổi";
            return AgeCheck.Fail(ErrorCodes.ChildCurfew,
                $"Suất này kết thúc lúc {EndText(startTime, endTime)}. Theo quy định, {who} chỉ xem suất kết thúc trước {latest:hh\\:mm}.");
        }
        return AgeCheck.Pass;
    }

    /// <summary>
    /// Phim K: hóa đơn có vé CHILD_U13 phải có ít nhất một vé ADULT hoặc SENIOR, hoặc có xác nhận người giám hộ đi cùng (GUARDIAN_REQUIRED).
    /// </summary>
    public static AgeCheck CheckGuardian(string rating, IEnumerable<string> audienceTypes, bool guardianConfirmed)
    {
        if (rating != AgeRating.K) return AgeCheck.Pass;
        var list = audienceTypes.ToList();
        if (!list.Contains(AudienceType.ChildU13)) return AgeCheck.Pass;
        if (guardianConfirmed || list.Any(a => a is AudienceType.Adult or AudienceType.Senior)) return AgeCheck.Pass;
        return AgeCheck.Fail(ErrorCodes.GuardianRequired,
            "Phim K: trẻ dưới 13 tuổi cần cha, mẹ hoặc người giám hộ đi cùng. Thêm 1 vé người lớn hoặc xác nhận có người giám hộ.");
    }

    /// <summary>
    /// Cảnh báo vàng khi xếp lịch (SPEC 3.1 mục 4): phim K kết thúc từ ChildU13LatestEnd hoặc phim T13 kết thúc từ ChildU16LatestEnd,
    /// vì khán giả chính của phim sẽ không vào được. Chỉ cảnh báo, không chặn. Trả null khi không có gì cần nhắc.
    /// </summary>
    public static string? ScheduleWarning(string rating, DateTime startTime, DateTime endTime, CinemaRules rules)
    {
        if (rating == AgeRating.K && !EndsBefore(startTime, endTime, rules.ChildU13LatestEnd))
            return $"Phim K kết thúc lúc {EndText(startTime, endTime)}, từ {rules.ChildU13LatestEnd:hh\\:mm} trở đi: trẻ em dưới 13 tuổi (khán giả chính của phim) không vào được suất này.";
        if (rating == AgeRating.T13 && !EndsBefore(startTime, endTime, rules.ChildU16LatestEnd))
            return $"Phim T13 kết thúc lúc {EndText(startTime, endTime)}, từ {rules.ChildU16LatestEnd:hh\\:mm} trở đi: khán giả từ 13 đến dưới 16 tuổi không vào được suất này.";
        return null;
    }
}
