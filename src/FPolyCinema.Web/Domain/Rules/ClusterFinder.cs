using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một ghế của sơ đồ khi tìm cụm ghế: Free là ghế còn chọn được (trống, hoặc đang do chính người xem giữ).</summary>
public sealed record ClusterSeat(int SeatId, int Row, int Col, string Label, string SeatType, int? PairGroupId, bool Free);

/// <summary>Một phương án cụm ghế: các ghế liền nhau, điểm trung bình (làm tròn) và khoảng cách tới hàng lý tưởng.</summary>
public sealed record SeatCluster(IReadOnlyList<ClusterSeat> Seats, int Score, double RowDistance)
{
    /// <summary>Nhãn gọn, ví dụ "G6–G8" (một ghế thì "G6").</summary>
    public string Range => Seats.Count == 1 ? Seats[0].Label : $"{Seats[0].Label}–{Seats[^1].Label}";

    public IEnumerable<int> SeatIds => Seats.Select(s => s.SeatId);
}

/// <summary>
/// Tìm cụm K ghế (SPEC 4.1). Logic thuần, có unit test.
/// - Điểm từng ghế theo SeatScoring với persona đang chọn (ghế đầu đoạn tính theo ô lối đi hoặc tường trong hàng).
/// - Duyệt từng đoạn (dãy ghế liền nhau trong một hàng) bằng cửa sổ trượt K ghế trống liền nhau; ghế xe lăn cần xác nhận riêng nên
///   không đưa vào cụm và coi như biên; với SWEETBOX chỉ nhận nguyên cặp (cửa sổ không được cắt đôi một cặp).
/// - Bỏ các cụm vi phạm luật ghế mồ côi (OrphanSeatValidator).
/// - Xếp theo điểm trung bình giảm dần; bằng điểm thì ưu tiên cụm gần r_ideal hơn, rồi gần giữa phòng hơn.
/// - Trả tối đa 3 phương án, các phương án không dùng chung ghế để khách có lựa chọn khác nhau.
/// </summary>
public static class ClusterFinder
{
    public const int MaxResults = 3;

    /// <summary>Điểm của mọi ghế theo persona (dùng cho bản đồ điểm AI và tooltip).</summary>
    public static Dictionary<int, int> ScoreAll(IReadOnlyCollection<ClusterSeat> seats, int totalRows, int totalCols, decimal idealRowRatio, string persona)
    {
        var colsByRow = seats.GroupBy(s => s.Row).ToDictionary(g => g.Key, g => (IReadOnlyCollection<int>)g.Select(s => s.Col).ToHashSet());
        return seats.ToDictionary(s => s.SeatId, s => SeatScoring.Score(totalRows, totalCols, s.Row, s.Col, s.SeatType, persona, idealRowRatio,
            SeatScoring.IsSegmentEnd(colsByRow[s.Row], s.Col, totalCols)));
    }

    /// <summary>
    /// Tìm tối đa <paramref name="maxResults"/> cụm <paramref name="quantity"/> ghế tốt nhất. Danh sách rỗng khi không còn cụm nào hợp lệ.
    /// </summary>
    public static List<SeatCluster> Find(IReadOnlyCollection<ClusterSeat> seats, int totalRows, int totalCols, decimal idealRowRatio, string persona,
        int quantity, int maxResults = MaxResults)
    {
        if (quantity <= 0 || seats.Count == 0) return new List<SeatCluster>();
        var scores = ScoreAll(seats, totalRows, totalCols, idealRowRatio, persona);
        double rIdeal = SeatScoring.IdealRow(totalRows, idealRowRatio, persona);
        double cIdeal = SeatScoring.IdealCol(totalCols);
        var slots = seats.Select(s => new SeatSlot(s.SeatId, s.Row, s.Col, s.Label, s.SeatType, !s.Free)).ToList();

        var candidates = new List<(SeatCluster Cluster, double ColDistance)>();
        foreach (var segment in Segments(seats))
        {
            for (int i = 0; i + quantity <= segment.Count; i++)
            {
                var window = segment.GetRange(i, quantity);
                if (window.Any(s => !s.Free)) continue;
                if (!WholePairs(window, seats)) continue;
                if (OrphanSeatValidator.Validate(slots, window.Select(s => s.SeatId).ToHashSet()) != null) continue;
                int score = (int)Math.Round(window.Average(s => scores[s.SeatId]), MidpointRounding.AwayFromZero);
                double rowDist = Math.Abs(window[0].Row - rIdeal);
                double colDist = Math.Abs(window.Average(s => s.Col) - cIdeal);
                candidates.Add((new SeatCluster(window, score, rowDist), colDist));
            }
        }

        var result = new List<SeatCluster>();
        var used = new HashSet<int>();
        foreach (var c in candidates.OrderByDescending(c => c.Cluster.Score).ThenBy(c => c.Cluster.RowDistance).ThenBy(c => c.ColDistance)
                     .ThenBy(c => c.Cluster.Seats[0].Row).ThenBy(c => c.Cluster.Seats[0].Col))
        {
            if (c.Cluster.Seats.Any(s => used.Contains(s.SeatId))) continue;
            result.Add(c.Cluster);
            foreach (var s in c.Cluster.Seats) used.Add(s.SeatId);
            if (result.Count >= maxResults) break;
        }
        return result;
    }

    /// <summary>Đoạn ghế: cùng hàng, cột liên tiếp; ghế xe lăn và ô trống cắt đoạn.</summary>
    public static List<List<ClusterSeat>> Segments(IEnumerable<ClusterSeat> seats)
    {
        var result = new List<List<ClusterSeat>>();
        foreach (var row in seats.Where(s => s.SeatType != SeatType.Wheelchair).GroupBy(s => s.Row).OrderBy(g => g.Key))
        {
            List<ClusterSeat>? cur = null;
            foreach (var s in row.OrderBy(s => s.Col))
            {
                if (cur == null || s.Col != cur[^1].Col + 1)
                {
                    cur = new List<ClusterSeat>();
                    result.Add(cur);
                }
                cur.Add(s);
            }
        }
        return result;
    }

    /// <summary>Cửa sổ chứa ghế đôi thì phải chứa đủ cả cặp (cùng PairGroupId).</summary>
    private static bool WholePairs(IReadOnlyCollection<ClusterSeat> window, IReadOnlyCollection<ClusterSeat> all)
    {
        var ids = window.Select(s => s.SeatId).ToHashSet();
        foreach (var s in window.Where(s => s.SeatType == SeatType.Sweetbox))
        {
            if (s.PairGroupId is not int g) return false;
            if (all.Where(o => o.PairGroupId == g && o.SeatType == SeatType.Sweetbox).Any(o => !ids.Contains(o.SeatId))) return false;
        }
        return true;
    }
}

/// <summary>Dữ kiện của một hóa đơn khi xét tự hủy vé (SPEC 3.9).</summary>
public sealed record SelfCancelFacts(string Channel, string Status, bool IsOwner, bool IsCorporate, DateTime StartTime,
    IReadOnlyCollection<string> TicketCheckInStatuses, string FnbStatus, int CancelsThisMonth);

/// <summary>Kết quả xét tự hủy: được hay không, lý do (câu cho người dùng) và số lần hủy còn lại trong tháng.</summary>
public sealed record SelfCancelCheck(bool Ok, string? Reason, int RemainingThisMonth, DateTime Deadline);

/// <summary>
/// Điều kiện khách tự hủy vé (SPEC 3.9). Logic thuần, có unit test.
/// - Hóa đơn ONLINE ở trạng thái finish, của chính khách đó, không phải hóa đơn CORPORATE.
/// - now ≤ StartTime − SelfCancelMinMinutesBeforeStart.
/// - Mọi vé đều NOT_CHECKED_IN; FnbStatus là NONE hoặc PENDING_PICKUP.
/// - Số lần CANCELLED_BY_USER trong tháng dương lịch hiện tại (giờ Việt Nam) nhỏ hơn SelfCancelMaxPerMonth.
/// Không đủ điều kiện thì SELF_CANCEL_NOT_ALLOWED kèm lý do cụ thể.
/// </summary>
public static class SelfCancelRules
{
    /// <summary>Đầu tháng dương lịch chứa thời điểm now (để đếm số lần đã tự hủy).</summary>
    public static DateTime MonthStart(DateTime now) => new(now.Year, now.Month, 1);

    public static SelfCancelCheck Check(SelfCancelFacts f, DateTime now, int minMinutesBeforeStart, int maxPerMonth)
    {
        var deadline = f.StartTime.AddMinutes(-minMinutesBeforeStart);
        int remaining = Math.Max(0, maxPerMonth - f.CancelsThisMonth);
        SelfCancelCheck No(string reason) => new(false, reason, remaining, deadline);

        if (!f.IsOwner) return No("Hóa đơn này không thuộc tài khoản của bạn.");
        if (f.Channel != BillChannel.Online || f.IsCorporate) return No("Chỉ vé mua online mới tự hủy được. Vé mua tại quầy hoặc vé doanh nghiệp bạn liên hệ quầy vé.");
        if (f.Status != BillStatus.Finish) return No(f.Status == BillStatus.Cancel ? "Vé này đã được hủy." : "Vé này chưa thanh toán xong.");
        if (now > deadline)
            return No($"Vé chỉ tự hủy được trước giờ chiếu {HoursText(minMinutesBeforeStart)}. Suất này bắt đầu lúc {f.StartTime:HH\\:mm}.");
        if (f.TicketCheckInStatuses.Any(s => s != TicketCheckInStatus.NotCheckedIn))
            return No("Vé đã có ghế vào cửa hoặc đã đổi nên không tự hủy được.");
        if (f.FnbStatus is not (FnbStatus.None or FnbStatus.PendingPickup))
            return No("Bắp nước của đơn này đã được làm nên không tự hủy được.");
        if (f.CancelsThisMonth >= maxPerMonth)
            return No($"Bạn đã tự hủy {f.CancelsThisMonth} lần trong tháng này (tối đa {maxPerMonth} lần mỗi tháng).");
        return new SelfCancelCheck(true, null, remaining, deadline);
    }

    /// <summary>"2 tiếng" cho 120 phút, "90 phút" cho 90 phút.</summary>
    public static string HoursText(int minutes) => minutes % 60 == 0 ? $"{minutes / 60} tiếng" : $"{minutes} phút";
}

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

/// <summary>
/// Chữ ký webhook của cổng thanh toán (SPEC 3.7): HMAC-SHA256 trên nội dung gửi, khóa Payment:WebhookSecret, viết dạng hex chữ thường.
/// So sánh chữ ký theo thời gian cố định. Logic thuần, có unit test.
/// </summary>
public static class WebhookSignature
{
    public const string Header = "X-Signature";

    public static string Compute(string secret, string payload)
    {
        using var h = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool Verify(string secret, string payload, string? signature)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrWhiteSpace(signature)) return false;
        var expected = System.Text.Encoding.ASCII.GetBytes(Compute(secret, payload));
        var actual = System.Text.Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}

/// <summary>Tiến độ tới hạng kế tiếp (UI C5): hạng kế tiếp, mức chi tiêu cần đạt, phần trăm đã đạt. Logic thuần, có unit test.</summary>
public sealed record TierProgress(string Tier, string? NextTier, long Spend, long NextThreshold, int Percent);

public static class TierProgressCalculator
{
    public static TierProgress Of(long spend12Months, long vipThreshold, long vvipThreshold)
    {
        if (spend12Months >= vvipThreshold) return new TierProgress(LoyaltyTier.Vvip, null, spend12Months, vvipThreshold, 100);
        var (tier, next, threshold) = spend12Months >= vipThreshold ? (LoyaltyTier.Vip, LoyaltyTier.Vvip, vvipThreshold) : (LoyaltyTier.Member, LoyaltyTier.Vip, vipThreshold);
        int pct = threshold <= 0 ? 100 : (int)Math.Clamp(spend12Months * 100 / threshold, 0, 100);
        return new TierProgress(tier, next, spend12Months, threshold, pct);
    }
}
