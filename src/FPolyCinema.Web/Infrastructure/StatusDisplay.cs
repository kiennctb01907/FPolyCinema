using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>Nhãn tiếng Việt và kiểu màu (success, warning, danger, info, neutral) của một giá trị trạng thái.</summary>
public sealed record StatusLabel(string Label, string Tone);

/// <summary>
/// Bảng hiển thị trạng thái và danh mục (UI mục 6, CONTRACTS mục 1): không bao giờ hiện thẳng giá trị enum.
/// Layout xuất toàn bộ bảng ra JSON (id="status-display") để ui.js dùng chung qua ui.status(nhóm, giá trị).
/// </summary>
public static class StatusDisplay
{
    public const string Bill = "bill";
    public const string CancelReasons = "cancelReason";
    public const string Ticket = "ticket";
    public const string BillCheckIn = "billCheckIn";
    public const string Fnb = "fnb";
    public const string Showtime = "showtime";
    public const string Approval = "approval";
    public const string Shift = "shift";
    public const string Active = "active";
    public const string Role = "role";
    public const string Tier = "tier";
    public const string Age = "ageRating";
    public const string Genres = "genre";
    public const string Language = "languageFormat";
    public const string Warning = "contentWarning";
    public const string Channel = "channel";
    public const string Payment = "paymentMethod";
    public const string Seat = "seatType";
    public const string Audience = "audienceType";
    public const string Wastage = "wastage";

    private static StatusLabel L(string label, string tone = "neutral") => new(label, tone);

    /// <summary>Toàn bộ bảng: nhóm → (giá trị → nhãn). Giá trị Active dùng "true"/"false".</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, StatusLabel>> All =
        new Dictionary<string, IReadOnlyDictionary<string, StatusLabel>>
        {
            [Bill] = new Dictionary<string, StatusLabel>
            {
                [BillStatus.Waiting] = L("Chờ thanh toán", "warning"),
                [BillStatus.Finish] = L("Đã thanh toán", "success"),
                [BillStatus.Cancel] = L("Đã hủy", "danger"),
            },
            [CancelReasons] = new Dictionary<string, StatusLabel>
            {
                [CancelReason.Expired] = L("Hết hạn giữ ghế"),
                [CancelReason.CancelledBeforePayment] = L("Hủy trước khi thanh toán"),
                [CancelReason.CancelledByUser] = L("Khách tự hủy"),
                [CancelReason.VoidedByPos] = L("Hủy tại quầy (đã duyệt)"),
                [CancelReason.OverbookedRefunded] = L("Hoàn tiền do mất ghế"),
                [CancelReason.IncidentCompensated] = L("Hủy do sự cố, đã bồi thường"),
            },
            [Ticket] = new Dictionary<string, StatusLabel>
            {
                [TicketCheckInStatus.NotCheckedIn] = L("Chưa vào"),
                [TicketCheckInStatus.CheckedIn] = L("Đã vào", "success"),
                [TicketCheckInStatus.Swapped] = L("Đã đổi ghế", "info"),
                [TicketCheckInStatus.Voided] = L("Đã hủy", "danger"),
            },
            [BillCheckIn] = new Dictionary<string, StatusLabel>
            {
                [BillCheckInStatus.None] = L("Không có vé"),
                [BillCheckInStatus.NotCheckedIn] = L("Chưa vào"),
                [BillCheckInStatus.PartiallyCheckedIn] = L("Đã vào một phần", "warning"),
                [BillCheckInStatus.FullyCheckedIn] = L("Đã vào đủ", "success"),
                [BillCheckInStatus.Voided] = L("Đã hủy", "danger"),
            },
            [Fnb] = new Dictionary<string, StatusLabel>
            {
                [FnbStatus.None] = L("Không có bắp nước"),
                [FnbStatus.PendingPickup] = L("Chờ nhận", "info"),
                [FnbStatus.Preparing] = L("Đang chuẩn bị", "warning"),
                [FnbStatus.ReadyForPickup] = L("Mời nhận đồ", "success"),
                [FnbStatus.Fulfilled] = L("Đã giao"),
                [FnbStatus.Voided] = L("Đã hủy", "danger"),
            },
            [Showtime] = new Dictionary<string, StatusLabel>
            {
                [OperationalStatus.Scheduled] = L("Sắp chiếu"),
                [OperationalStatus.Boarding] = L("Mời vào rạp", "success"),
                [OperationalStatus.AdsPlaying] = L("Đang chiếu quảng cáo", "info"),
                [OperationalStatus.FeaturePlaying] = L("Đang chiếu", "info"),
                [OperationalStatus.CreditsRolling] = L("Sắp kết thúc", "info"),
                [OperationalStatus.CleaningInProgress] = L("Đang dọn phòng", "warning"),
                [OperationalStatus.Completed] = L("Đã xong"),
                [OperationalStatus.Cancelled] = L("Đã hủy", "danger"),
            },
            [Approval] = new Dictionary<string, StatusLabel>
            {
                [ApprovalStatus.Pending] = L("Chờ duyệt", "warning"),
                [ApprovalStatus.Approved] = L("Đã duyệt", "success"),
                [ApprovalStatus.Rejected] = L("Từ chối", "danger"),
                [ApprovalStatus.Expired] = L("Hết hạn"),
            },
            [Shift] = new Dictionary<string, StatusLabel>
            {
                [ShiftStatus.Open] = L("Đang mở", "success"),
                [ShiftStatus.Closed] = L("Đã chốt"),
            },
            [Active] = new Dictionary<string, StatusLabel>
            {
                ["true"] = L("Đang hoạt động", "success"),
                ["false"] = L("Ngưng"),
            },
            [Role] = new Dictionary<string, StatusLabel>
            {
                [Roles.Customer] = L("Khách hàng"),
                [Roles.POSCashier] = L("Thu ngân và quầy bắp nước", "info"),
                [Roles.TicketInspector] = L("Soát vé và điều phối phòng chiếu", "info"),
                [Roles.ShiftManager] = L("Quản lý ca", "warning"),
                [Roles.Manager] = L("Quản lý rạp (quản trị)", "success"),
            },
            [Tier] = new Dictionary<string, StatusLabel>
            {
                [LoyaltyTier.Member] = L("Thành viên"),
                [LoyaltyTier.Vip] = L("VIP", "warning"),
                [LoyaltyTier.Vvip] = L("VVIP", "success"),
            },
            [Age] = new Dictionary<string, StatusLabel>
            {
                [AgeRating.P] = L("P: Phim dành cho khán giả ở mọi độ tuổi", AgeRating.P),
                [AgeRating.K] = L("K: Phim dành cho khán giả dưới 13 tuổi, cần cha, mẹ hoặc người giám hộ đi cùng", AgeRating.K),
                [AgeRating.T13] = L("T13: Phim dành cho khán giả từ đủ 13 tuổi trở lên", AgeRating.T13),
                [AgeRating.T16] = L("T16: Phim dành cho khán giả từ đủ 16 tuổi trở lên", AgeRating.T16),
                [AgeRating.T18] = L("T18: Phim dành cho khán giả từ đủ 18 tuổi trở lên", AgeRating.T18),
                [AgeRating.C] = L("C: Phim không được phép phổ biến", AgeRating.C),
            },
            [Genres] = new Dictionary<string, StatusLabel>
            {
                [Genre.Action] = L("Hành động"),
                [Genre.Adventure] = L("Phiêu lưu"),
                [Genre.Animation] = L("Hoạt hình"),
                [Genre.Comedy] = L("Hài"),
                [Genre.Drama] = L("Tâm lý"),
                [Genre.Family] = L("Gia đình"),
                [Genre.Horror] = L("Kinh dị"),
                [Genre.Romance] = L("Tình cảm"),
                [Genre.SciFi] = L("Khoa học viễn tưởng"),
                [Genre.Thriller] = L("Giật gân"),
                [Genre.Documentary] = L("Tài liệu"),
            },
            [Language] = new Dictionary<string, StatusLabel>
            {
                [LanguageFormat.Subtitled] = L("Phụ đề"),
                [LanguageFormat.Dubbed] = L("Lồng tiếng"),
                [LanguageFormat.Original] = L("Nguyên bản"),
            },
            [Warning] = new Dictionary<string, StatusLabel>
            {
                [ContentWarning.Theme] = L("Chủ đề nhạy cảm", "warning"),
                [ContentWarning.Violence] = L("Bạo lực", "warning"),
                [ContentWarning.NuditySex] = L("Khỏa thân, tình dục", "warning"),
                [ContentWarning.Drugs] = L("Ma túy", "warning"),
                [ContentWarning.Horror] = L("Kinh dị", "warning"),
                [ContentWarning.Language] = L("Ngôn ngữ thô tục", "warning"),
                [ContentWarning.DangerousBehavior] = L("Hành vi nguy hiểm", "warning"),
            },
            [Channel] = new Dictionary<string, StatusLabel>
            {
                [BillChannel.Pos] = L("Quầy"),
                [BillChannel.Online] = L("Online", "info"),
                [BillChannel.Corporate] = L("Doanh nghiệp", "warning"),
            },
            [Payment] = new Dictionary<string, StatusLabel>
            {
                [PaymentMethod.Cash] = L("Tiền mặt"),
                [PaymentMethod.VietQrSim] = L("VietQR (mô phỏng)", "info"),
                [PaymentMethod.Wallet] = L("Ví FPoly", "info"),
            },
            [Seat] = new Dictionary<string, StatusLabel>
            {
                [SeatType.Standard] = L("Ghế thường"),
                [SeatType.Vip] = L("Ghế VIP", "warning"),
                [SeatType.Sweetbox] = L("Ghế đôi", "danger"),
                [SeatType.Wheelchair] = L("Chỗ xe lăn", "info"),
            },
            [Audience] = new Dictionary<string, StatusLabel>
            {
                [AudienceType.Adult] = L("Người lớn"),
                [AudienceType.U22] = L("Khách dưới 22 tuổi"),
                [AudienceType.ChildU13] = L("Trẻ em dưới 13 tuổi"),
                [AudienceType.Child13To15] = L("Trẻ em từ 13 đến dưới 16 tuổi"),
                [AudienceType.Senior] = L("Người cao tuổi"),
                [AudienceType.Merit] = L("Người có công với cách mạng"),
                [AudienceType.Hardship] = L("Người có hoàn cảnh đặc biệt khó khăn"),
                [AudienceType.DisabledSevere] = L("Người khuyết tật nặng"),
                [AudienceType.DisabledExtreme] = L("Người khuyết tật đặc biệt nặng"),
            },
            [Wastage] = new Dictionary<string, StatusLabel>
            {
                [WastageStatus.Pending] = L("Chờ duyệt", "warning"),
                [WastageStatus.Approved] = L("Đã duyệt", "success"),
                [WastageStatus.Rejected] = L("Từ chối", "danger"),
            },
        };

    /// <summary>Nhãn của một giá trị; giá trị lạ trả "Không xác định" (không bao giờ trả lại chuỗi enum gốc).</summary>
    public static StatusLabel Of(string group, string? value)
    {
        if (value != null && All.TryGetValue(group, out var map) && map.TryGetValue(value, out var label)) return label;
        return new StatusLabel("Không xác định", "neutral");
    }

    public static StatusLabel Of(string group, bool value) => Of(group, value ? "true" : "false");

    /// <summary>Tên hiển thị của vai trò (CONTRACTS mục 1, cột Hiển thị).</summary>
    public static string RoleLabel(string? role) => Of(Role, role).Label;

    /// <summary>Câu giải thích của nhãn độ tuổi (Thông tư 05/2023), ví dụ "T16: Phim dành cho khán giả từ đủ 16 tuổi trở lên".</summary>
    public static string AgeExplanation(string? rating) => Of(Age, rating).Label;

    /// <summary>Danh sách nhãn của một cột lưu nhiều giá trị cách nhau dấu phẩy (Genres, LanguageFormats, ContentWarnings).</summary>
    public static IReadOnlyList<string> LabelsOfCsv(string group, string? csv) =>
        CsvList.Split(csv).Select(v => Of(group, v).Label).ToList();
}
