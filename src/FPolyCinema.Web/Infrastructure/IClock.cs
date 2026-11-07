namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Nguồn thời gian duy nhất của hệ thống (SPEC 2.7). Mọi chỗ cần "bây giờ" đều dùng IClock,
/// không dùng DateTime.Now. Unit test dùng đồng hồ giả.
/// </summary>
public interface IClock
{
    /// <summary>Giờ Việt Nam hiện tại (DateTimeKind.Unspecified, đúng kiểu lưu ở cột DATETIME2(0)).</summary>
    DateTime Now { get; }

    /// <summary>Ngày hiện tại theo giờ Việt Nam.</summary>
    DateTime Today { get; }
}
