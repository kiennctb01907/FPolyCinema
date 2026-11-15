using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Vị trí một cửa sổ trên màn hình, tính theo tỷ lệ của vùng màn hình dùng được (0–1).</summary>
public sealed record WindowSlot(string Name, double X, double Y, double Width, double Height);

/// <summary>Một suất ứng viên cho kịch bản demo: giờ, trạng thái vận hành, số ghế trống và còn bán online hay không.</summary>
public sealed record DemoShowtimeOption(int Id, DateTime Start, DateTime FeatureStart, DateTime End, string Status, int FreeSeats, bool SalesOpen);

/// <summary>
/// Quy tắc thuần của trung tâm demo nhiều màn hình (SPEC 5, UI D4): tên miền phụ của từng cửa sổ, cách chia đôi màn hình và chọn suất cho kịch bản.
/// </summary>
public static class DemoScreenRules
{
    /// <summary>Tên miền phụ của localhost dùng cho từng cửa sổ (SPEC S5-09). Mỗi tên miền giữ cookie đăng nhập riêng.</summary>
    public const string Cashier = "cashier", Manager = "manager", Gate = "gate", KhachA = "khach-a", KhachB = "khach-b";

    public static readonly IReadOnlyList<string> Subdomains = new[] { Cashier, Manager, Gate, KhachA, KhachB };

    /// <summary>Máy đang chạy ở localhost (hoặc một tên miền phụ của localhost) thì mới tách phiên được bằng tên miền phụ.</summary>
    public static bool IsLocalHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        var h = host.Trim().ToLowerInvariant();
        return h == "localhost" || h.EndsWith(".localhost", StringComparison.Ordinal);
    }

    /// <summary>
    /// Gốc địa chỉ của cửa sổ theo tên miền phụ: http://cashier.localhost:5080. Khi máy không chạy ở localhost (127.0.0.1, tên miền thật) thì trả gốc hiện tại
    /// (không tách được phiên) – trang báo cho người dùng biết.
    /// </summary>
    public static string WindowOrigin(string scheme, string host, int? port, string subdomain)
    {
        if (!Subdomains.Contains(subdomain)) throw new ArgumentException("Tên miền phụ không có trong danh sách của trung tâm demo.", nameof(subdomain));
        var h = IsLocalHost(host) ? subdomain + ".localhost" : host;
        return $"{scheme}://{h}{(port is int p ? ":" + p : string.Empty)}";
    }

    /// <summary>Đường dẫn tự đăng nhập của cửa sổ: /demo/switch-role?role=…&amp;returnUrl=… (SPEC S5-09).</summary>
    public static string SwitchRolePath(string role, string returnUrl) =>
        $"/demo/switch-role?role={Uri.EscapeDataString(role)}&returnUrl={Uri.EscapeDataString(returnUrl)}";

    /// <summary>
    /// Chia đôi màn hình cho các cửa sổ của một kịch bản: cửa sổ đầu chiếm nửa trái; các cửa sổ còn lại chia đều nửa phải theo chiều dọc.
    /// Một cửa sổ thì chiếm cả màn hình.
    /// </summary>
    public static List<WindowSlot> Slots(int count)
    {
        if (count <= 0) return new List<WindowSlot>();
        if (count == 1) return new List<WindowSlot> { new("full", 0, 0, 1, 1) };
        var list = new List<WindowSlot> { new("left", 0, 0, 0.5, 1) };
        int right = count - 1;
        for (int i = 0; i < right; i++)
        {
            var name = right == 1 ? "right" : i == 0 ? "right-top" : i == right - 1 ? "right-bottom" : "right-" + (i + 1);
            list.Add(new WindowSlot(name, 0.5, (double)i / right, 0.5, 1.0 / right));
        }
        return list;
    }

    /// <summary>
    /// Suất cho kịch bản "Hai khách cùng giành một ghế": còn bán online, còn ít nhất 4 ghế trống, ưu tiên suất sớm nhất bắt đầu sau hiện tại ít nhất
    /// 30 phút (đủ thời gian giữ ghế); không có thì suất còn bán sớm nhất.
    /// </summary>
    public static DemoShowtimeOption? PickContest(IEnumerable<DemoShowtimeOption> options, DateTime now)
    {
        var open = options.Where(o => o.SalesOpen && o.FreeSeats >= 4).OrderBy(o => o.Start).ThenBy(o => o.Id).ToList();
        return open.FirstOrDefault(o => o.Start >= now.AddMinutes(30)) ?? open.FirstOrDefault();
    }

    /// <summary>
    /// Suất cho vé mẫu của kịch bản bắp nước và soát vé: còn bán online, còn ít nhất 2 ghế trống. Ưu tiên suất đang mở cửa đón khách
    /// (BOARDING, ADS_PLAYING – quét vé được ngay), rồi suất đã vào khung "Tôi đã tới rạp" (từ StartTime − imHereWindowMinutes), rồi suất sớm nhất sau đó.
    /// </summary>
    public static DemoShowtimeOption? PickEntry(IEnumerable<DemoShowtimeOption> options, DateTime now, int imHereWindowMinutes)
    {
        int Rank(DemoShowtimeOption o) =>
            o.Status is OperationalStatus.Boarding or OperationalStatus.AdsPlaying ? 0
            : now >= o.Start.AddMinutes(-imHereWindowMinutes) ? 1
            : 2;
        return options.Where(o => o.SalesOpen && o.FreeSeats >= 2 && o.End > now)
            .OrderBy(Rank).ThenBy(o => o.Start).ThenBy(o => o.Id).FirstOrDefault();
    }

    /// <summary>
    /// Vé của khách mẫu còn dùng được cho kịch bản bắp nước và soát vé: hóa đơn finish, có bắp nước chưa hủy, suất chưa kết thúc,
    /// còn ít nhất một vé chưa vào cửa.
    /// </summary>
    public static bool TicketUsable(string? billStatus, string? fnbStatus, string? checkInStatus, DateTime? start, DateTime? end, DateTime now) =>
        billStatus == BillStatus.Finish && fnbStatus is not (null or FnbStatus.None or FnbStatus.Voided)
        && start is DateTime && end is DateTime e && e > now
        && checkInStatus is not (BillCheckInStatus.FullyCheckedIn or BillCheckInStatus.Voided);

    /// <summary>Đã tới khung "Tôi đã tới rạp" của suất (từ StartTime − imHereWindowMinutes): demo kịch bản bắp nước được ngay.</summary>
    public static bool ReadyNow(DateTime start, DateTime now, int imHereWindowMinutes) => now >= start.AddMinutes(-imHereWindowMinutes);
}
