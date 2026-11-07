namespace FPolyCinema.Web.Domain.Constants;

/// <summary>Định dạng phòng chiếu (cột Auditoriums.Format).</summary>
public static class AuditoriumFormat
{
    public const string F2D = "2D";
    public const string F3D = "3D";
    public const string IMAX = "IMAX";
    public const string F4DX = "4DX";

    public static readonly string[] All = { F2D, F3D, IMAX, F4DX };
}

/// <summary>Loại ghế (cột Seats.SeatType).</summary>
public static class SeatType
{
    public const string Standard = "STANDARD";
    public const string Vip = "VIP";
    public const string Sweetbox = "SWEETBOX";
    public const string Wheelchair = "WHEELCHAIR";

    public static readonly string[] All = { Standard, Vip, Sweetbox, Wheelchair };
}

/// <summary>Trạng thái ghế của một suất (cột ShowtimeSeats.Status).</summary>
public static class ShowtimeSeatStatus
{
    public const string Available = "AVAILABLE";
    public const string Held = "HELD";
    public const string Booked = "BOOKED";
    public const string HouseHold = "HOUSE_HOLD";

    public static readonly string[] All = { Available, Held, Booked, HouseHold };
}

/// <summary>
/// Trạng thái hiển thị của ghế, tính khi đọc và gửi cho giao diện.
/// Giao diện tự hiển thị SELECTED cho ghế có holdBillId trùng hóa đơn của mình.
/// </summary>
public static class SeatDisplayStatus
{
    public const string Available = "AVAILABLE";
    public const string Held = "HELD";
    public const string Booked = "BOOKED";
    public const string HouseHold = "HOUSE_HOLD";
    public const string Maintenance = "MAINTENANCE";

    public static readonly string[] All = { Available, Held, Booked, HouseHold, Maintenance };
}

/// <summary>Trạng thái vận hành của suất chiếu (cột Showtimes.OperationalStatus).</summary>
public static class OperationalStatus
{
    public const string Scheduled = "SCHEDULED";
    public const string Boarding = "BOARDING";
    public const string AdsPlaying = "ADS_PLAYING";
    public const string FeaturePlaying = "FEATURE_PLAYING";
    public const string CreditsRolling = "CREDITS_ROLLING";
    public const string CleaningInProgress = "CLEANING_IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All = { Scheduled, Boarding, AdsPlaying, FeaturePlaying, CreditsRolling, CleaningInProgress, Completed, Cancelled };
}

/// <summary>Kiểu người xem dùng khi chấm điểm ghế (SPEC 4.1).</summary>
public static class SeatPersona
{
    public const string BestViewAudio = "BEST_VIEW_AUDIO";
    public const string NearScreen = "NEAR_SCREEN";
    public const string PrivateCouple = "PRIVATE_COUPLE";
    public const string NearAisle = "NEAR_AISLE";

    public static readonly string[] All = { BestViewAudio, NearScreen, PrivateCouple, NearAisle };
}
