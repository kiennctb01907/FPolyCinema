using System.Data;
using System.Globalization;

namespace FPolyCinema.Web.DAL;

/// <summary>Đọc giá trị từ DataRow, đổi DBNull thành null cho cột có thể rỗng. Dùng trong FromDataRow của các DAL.</summary>
public static class DataRowExtensions
{
    private static object? Raw(DataRow row, string column)
    {
        var v = row[column];
        return v == DBNull.Value ? null : v;
    }

    public static int GetInt(this DataRow row, string column) => Convert.ToInt32(row[column], CultureInfo.InvariantCulture);

    public static int? GetIntN(this DataRow row, string column) =>
        Raw(row, column) is { } v ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : null;

    public static long GetLong(this DataRow row, string column) => Convert.ToInt64(row[column], CultureInfo.InvariantCulture);

    public static long? GetLongN(this DataRow row, string column) =>
        Raw(row, column) is { } v ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : null;

    public static decimal GetDecimal(this DataRow row, string column) => Convert.ToDecimal(row[column], CultureInfo.InvariantCulture);

    public static decimal? GetDecimalN(this DataRow row, string column) =>
        Raw(row, column) is { } v ? Convert.ToDecimal(v, CultureInfo.InvariantCulture) : null;

    public static bool GetBool(this DataRow row, string column) => Convert.ToBoolean(row[column], CultureInfo.InvariantCulture);

    public static DateTime GetDateTime(this DataRow row, string column) => Convert.ToDateTime(row[column], CultureInfo.InvariantCulture);

    public static DateTime? GetDateTimeN(this DataRow row, string column) =>
        Raw(row, column) is { } v ? Convert.ToDateTime(v, CultureInfo.InvariantCulture) : null;

    public static string GetString(this DataRow row, string column) => Convert.ToString(row[column], CultureInfo.InvariantCulture) ?? string.Empty;

    public static string? GetStringN(this DataRow row, string column) =>
        Raw(row, column) is { } v ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
}
