using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Services;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Phim nổi bật ở hero trang chủ (UI C1).</summary>
public sealed record FeaturedMovieVm(int Id, string Title, string AgeRating, string? PosterUrl, string Meta, string? TrailerUrl, string BookUrl);

/// <summary>Trang chủ C1: hero, thanh mua vé nhanh, lưới phim đang chiếu và sắp chiếu, combo nổi bật, ưu đãi công khai.</summary>
public class HomeVm
{
    public FeaturedMovieVm? Featured { get; set; }
    public List<MovieCardVm> NowShowing { get; set; } = new();
    public List<MovieCardVm> ComingSoon { get; set; } = new();
    public List<FoodCardVm> Combos { get; set; } = new();
    public List<Voucher> Promos { get; set; } = new();
    public List<Cinema> Cinemas { get; set; } = new();
    public int? CinemaId { get; set; }
    public string Tab { get; set; } = "now";
}

/// <summary>Một chip giờ của lịch chiếu (UI C2): giờ, số ghế còn, hết vé, suất muộn (icon mặt trăng kèm lưu ý giờ chiếu cho trẻ em).</summary>
public sealed record ShowtimeChipVm(int ShowtimeId, DateTime StartTime, DateTime EndTime, int FreeSeats, bool SoldOut, string? LateNotice, string Url);

/// <summary>Nhóm suất theo "2D · Phụ đề".</summary>
public sealed record FormatGroupVm(string Label, List<ShowtimeChipVm> Showtimes);

/// <summary>Nhóm suất theo rạp (C2) hoặc theo phim (trang lịch chiếu).</summary>
public sealed record ScheduleGroupVm(string Title, string? Subtitle, string? AgeRating, string? PosterUrl, string? Link, List<FormatGroupVm> Formats);

/// <summary>Bộ lọc lịch chiếu: 7 ngày, định dạng, ngôn ngữ, rạp.</summary>
public class ScheduleFilterVm
{
    public List<DateTime> Days { get; set; } = new();
    public DateTime Date { get; set; }
    public string? Format { get; set; }
    public string? Language { get; set; }
    public int? CinemaId { get; set; }
    public List<string> Formats { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<Cinema> Cinemas { get; set; } = new();
    public string BasePath { get; set; } = "/showtimes";
    public DateTime Today { get; set; }

    /// <summary>Đường dẫn giữ các bộ lọc khác, đổi một tham số.</summary>
    public string Url(DateTime? date = null, string? format = "\0", string? language = "\0", int? cinemaId = -1)
    {
        var q = new List<string>();
        var d = date ?? Date;
        q.Add("date=" + d.ToString("yyyy-MM-dd"));
        var f = format == "\0" ? Format : format;
        var l = language == "\0" ? Language : language;
        var c = cinemaId == -1 ? CinemaId : cinemaId;
        if (!string.IsNullOrEmpty(f)) q.Add("format=" + Uri.EscapeDataString(f));
        if (!string.IsNullOrEmpty(l)) q.Add("lang=" + Uri.EscapeDataString(l));
        // cinemaId=0 là "Tất cả rạp" (không có thì trang dùng rạp đang chọn ở đầu trang)
        q.Add("cinemaId=" + (c ?? 0));
        return BasePath + "?" + string.Join("&", q);
    }
}

/// <summary>Chi tiết phim C2.</summary>
public class MovieDetailVm
{
    public Movie Movie { get; set; } = new();
    public string? DistributorName { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public ScheduleFilterVm Filter { get; set; } = new();
    public List<ScheduleGroupVm> Groups { get; set; } = new();
}

/// <summary>Trang lịch chiếu (mọi phim, lọc theo rạp, ngày, định dạng, ngôn ngữ).</summary>
public class ShowtimesVm
{
    public ScheduleFilterVm Filter { get; set; } = new();
    public List<ScheduleGroupVm> Groups { get; set; } = new();
}

/// <summary>Danh sách phim: tab đang chiếu, sắp chiếu.</summary>
public class MoviesVm
{
    public string Tab { get; set; } = "now";
    public List<MovieCardVm> NowShowing { get; set; } = new();
    public List<MovieCardVm> ComingSoon { get; set; } = new();
}

/// <summary>Thực đơn bắp nước theo rạp đang chọn.</summary>
public class MenuVm
{
    public List<Category> Categories { get; set; } = new();
    public List<(FoodCardVm Card, int CategoryId, string? Description)> Items { get; set; } = new();
    public string CinemaName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
}

/// <summary>Đặt vé C3: suất, sơ đồ ghế, món, giỏ, ô xác nhận, thông tin ví và điểm của khách.</summary>
public class BookingVm
{
    public ShowtimeInfo Info { get; set; } = null!;
    public string CinemaName { get; set; } = string.Empty;
    public SeatMapVm SeatMap { get; set; } = new();
    public Cart? Cart { get; set; }
    public bool LoggedIn { get; set; }
    public bool IsCustomer { get; set; }
    public List<FoodCardVm> Drinks { get; set; } = new();
    public List<OnlineConfirmation> Confirmations { get; set; } = new();
    public string? CurfewNotice { get; set; }
    public long WalletBalance { get; set; }
    public int LoyaltyPoints { get; set; }
    public long PointValueVnd { get; set; }
    public string Persona { get; set; } = "BEST_VIEW_AUDIO";
    public int Quantity { get; set; } = 2;
    public List<CustomerShowtimeRow> OtherTimes { get; set; } = new();
    public bool SalesOpen { get; set; }
    public int GoodClusterScore { get; set; }
    /// <summary>Số phút giữ ghế (CinemaRules.SeatHoldMinutes) cho câu hướng dẫn và hộp hết giờ.</summary>
    public int SeatHoldMinutes { get; set; }
    public string ReturnUrl => $"/booking/{Info.Showtime.Id}";
}

/// <summary>Màn hình QR chờ thanh toán (UI C3-13).</summary>
public class PayVm
{
    public Bill Bill { get; set; } = new();
    public ShowtimeInfo? Info { get; set; }
    public string QrDataUri { get; set; } = string.Empty;
    public string SimUrl { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Trang mô phỏng cổng thanh toán (/payment-sim/pay).</summary>
public class PaymentSimVm
{
    public Bill? Bill { get; set; }
    public string QrDataUri { get; set; } = string.Empty;
    public string SuccessPayload { get; set; } = string.Empty;
    public string SuccessSignature { get; set; } = string.Empty;
    public string FailPayload { get; set; } = string.Empty;
    public string FailSignature { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

/// <summary>Vé điện tử C4.</summary>
public class TicketPageVm
{
    public Bill Bill { get; set; } = new();
    public ShowtimeInfo? Info { get; set; }
    public string CinemaName { get; set; } = string.Empty;
    public List<TicketRow> Tickets { get; set; } = new();
    public List<BillLineRow> Lines { get; set; } = new();
    public SelfCancelCheck? Cancel { get; set; }
    public int MaxPerMonth { get; set; }
    public int MinMinutesBeforeStart { get; set; }
    public long Paid { get; set; }
    public DateTime Now { get; set; }
    public int ImHereWindowMinutes { get; set; }

    /// <summary>Nút "Tôi đã tới rạp" hiện được không: bắp nước chờ nhận và đang trong khung giờ cho phép.</summary>
    public bool CanImHere => Bill.FnbStatus == Domain.Constants.FnbStatus.PendingPickup && Info != null
        && KdsRules.InImHereWindow(Info.Showtime.StartTime, Info.Showtime.EndTime, Now, ImHereWindowMinutes);
}

/// <summary>Tài khoản C5.</summary>
public class AccountVm
{
    public User User { get; set; } = new();
    public string Tab { get; set; } = "tickets";
    public TierProgress Progress { get; set; } = null!;
    public string MemberQr { get; set; } = string.Empty;
    public List<CustomerBillRow> Upcoming { get; set; } = new();
    public List<CustomerBillRow> Past { get; set; } = new();
    public List<WalletTransaction> Wallet { get; set; } = new();
    public List<PointTransaction> Points { get; set; } = new();
    public List<Voucher> Vouchers { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
    public int Unread { get; set; }
    /// <summary>Quy đổi điểm theo cấu hình CinemaRules (PointValueVnd, MaxPointsRedeemPercent).</summary>
    public long PointValueVnd { get; set; }
    public int MaxPointsRedeemPercent { get; set; }
    public ProfileVm Profile { get; set; } = new();
    public DateTime Now { get; set; }
}
