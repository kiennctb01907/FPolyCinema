using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Utils;

/// <summary>
/// Lớp truy cập CSDL duy nhất của hệ thống (SPEC 2.3), giữ đúng tên hàm của slide và thêm transaction.
/// - Tham số đặt tên @1, @2, … theo thứ tự trong danh sách parameters.
/// - null chuyển thành DBNull, DateTime dùng kiểu DateTime2, long dùng BigInt.
/// - Mỗi hàm có bản nạp chồng nhận thêm (SqlConnection conn, SqlTransaction tx) để service gom nhiều bước
///   vào một transaction; truyền conn = null thì hàm tự mở kết nối riêng.
/// - Không ghép dữ liệu người dùng vào câu SQL. Mệnh đề IN (...) dùng InParams.
/// </summary>
public static class DBUtil
{
    private static string? _connectionString;

    /// <summary>Thời gian chờ của mỗi lệnh (giây).</summary>
    public static int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>Gọi một lần lúc khởi động (Program.cs) với ConnectionStrings:Default.</summary>
    public static void Init(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:Default (appsettings.Development.json hoặc biến môi trường ConnectionStrings__Default).");
        _connectionString = connectionString;
    }

    public static string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("DBUtil chưa được khởi tạo: gọi DBUtil.Init lúc khởi động.");

    public static bool IsInitialized => _connectionString != null;

    // ------------------------------------------------------------------ QueryDataTable

    /// <summary>Chạy câu SELECT, trả về DataTable.</summary>
    public static DataTable QueryDataTable(string sql, List<object>? parameters = null)
    {
        using var conn = OpenConnection();
        return QueryCore(conn, null, sql, parameters);
    }

    /// <summary>Như trên nhưng chạy trong kết nối và transaction có sẵn (conn = null thì tự mở kết nối).</summary>
    public static DataTable QueryDataTable(string sql, List<object>? parameters, SqlConnection? conn, SqlTransaction? tx)
        => conn == null ? QueryDataTable(sql, parameters) : QueryCore(conn, tx, sql, parameters);

    /// <summary>Bí danh đúng tên trong slide.</summary>
    public static DataTable queryDataTable(string sql, List<object>? parameters = null) => QueryDataTable(sql, parameters);

    public static DataTable queryDataTable(string sql, List<object>? parameters, SqlConnection? conn, SqlTransaction? tx)
        => QueryDataTable(sql, parameters, conn, tx);

    // ------------------------------------------------------------------ ExecuteScalar

    /// <summary>
    /// Trả về giá trị đầu tiên: dùng cho SELECT một giá trị, INSERT kèm SELECT SCOPE_IDENTITY(),
    /// UPDATE hoặc DELETE kèm SELECT @@ROWCOUNT. DBNull trả về null.
    /// </summary>
    public static object? ExecuteScalar(string sql, List<object>? parameters = null)
    {
        using var conn = OpenConnection();
        return ScalarCore(conn, null, sql, parameters);
    }

    public static object? ExecuteScalar(string sql, List<object>? parameters, SqlConnection? conn, SqlTransaction? tx)
        => conn == null ? ExecuteScalar(sql, parameters) : ScalarCore(conn, tx, sql, parameters);

    /// <summary>Bí danh đúng tên trong slide.</summary>
    public static object? executeScalar(string sql, List<object>? parameters = null) => ExecuteScalar(sql, parameters);

    public static object? executeScalar(string sql, List<object>? parameters, SqlConnection? conn, SqlTransaction? tx)
        => ExecuteScalar(sql, parameters, conn, tx);

    // ------------------------------------------------------------------ ExecuteNonQuery

    /// <summary>Chạy INSERT, UPDATE, DELETE; trả về số dòng bị ảnh hưởng.</summary>
    public static int ExecuteNonQuery(string sql, List<object>? parameters = null)
    {
        using var conn = OpenConnection();
        return NonQueryCore(conn, null, sql, parameters);
    }

    public static int ExecuteNonQuery(string sql, List<object>? parameters, SqlConnection? conn, SqlTransaction? tx)
        => conn == null ? ExecuteNonQuery(sql, parameters) : NonQueryCore(conn, tx, sql, parameters);

    // ------------------------------------------------------------------ Transaction

    /// <summary>
    /// Mở kết nối, bắt đầu transaction, chạy work rồi commit; có lỗi thì rollback và ném lại lỗi.
    /// </summary>
    public static T InTransaction<T>(Func<SqlConnection, SqlTransaction, T> work)
    {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var result = work(conn, tx);
            tx.Commit();
            return result;
        }
        catch
        {
            try
            {
                tx.Rollback();
            }
            catch (InvalidOperationException)
            {
                // Transaction đã bị SQL Server hủy (ví dụ lỗi nghiêm trọng): không còn gì để rollback.
            }
            throw;
        }
    }

    /// <summary>Bản không trả giá trị của InTransaction.</summary>
    public static void InTransaction(Action<SqlConnection, SqlTransaction> work)
        => InTransaction<bool>((c, t) => { work(c, t); return true; });

    // ------------------------------------------------------------------ Tham số

    /// <summary>
    /// Thêm các giá trị vào danh sách tham số và trả chuỗi "@k, @k+1, …" để ghép vào mệnh đề IN (...).
    /// Đây là chỗ duy nhất được ghép chuỗi vào SQL; chuỗi ghép chỉ gồm tên tham số, không gồm dữ liệu.
    /// Danh sách rỗng trả "NULL" để IN (NULL) không khớp dòng nào.
    /// </summary>
    public static string InParams(IEnumerable<object> values, List<object> parameters)
    {
        var sb = new StringBuilder();
        foreach (var v in values)
        {
            parameters.Add(v);
            if (sb.Length > 0) sb.Append(", ");
            sb.Append('@').Append(parameters.Count);
        }
        return sb.Length == 0 ? "NULL" : sb.ToString();
    }

    /// <summary>Đổi null thành DBNull.Value để đưa giá trị có thể rỗng vào danh sách tham số.</summary>
    public static object Value(object? value) => value ?? DBNull.Value;

    /// <summary>Tạo SqlParameter @1, @2, … theo đúng thứ tự (tách riêng để unit test).</summary>
    public static List<SqlParameter> BuildParameters(List<object>? parameters)
    {
        var list = new List<SqlParameter>();
        if (parameters == null) return list;
        for (int i = 0; i < parameters.Count; i++)
        {
            list.Add(ToParameter("@" + (i + 1), parameters[i]));
        }
        return list;
    }

    private static SqlParameter ToParameter(string name, object? value)
    {
        switch (value)
        {
            case null:
            case DBNull:
                return new SqlParameter(name, SqlDbType.NVarChar) { Value = DBNull.Value };
            case DateTime dt:
                return new SqlParameter(name, SqlDbType.DateTime2) { Value = dt, Scale = 0 };
            case DateOnly d:
                return new SqlParameter(name, SqlDbType.Date) { Value = d.ToDateTime(TimeOnly.MinValue) };
            case TimeSpan ts:
                return new SqlParameter(name, SqlDbType.Time) { Value = ts };
            case long l:
                return new SqlParameter(name, SqlDbType.BigInt) { Value = l };
            case int n:
                return new SqlParameter(name, SqlDbType.Int) { Value = n };
            case bool b:
                return new SqlParameter(name, SqlDbType.Bit) { Value = b };
            case decimal m:
                return new SqlParameter(name, SqlDbType.Decimal) { Value = m, Precision = 19, Scale = 4 };
            case string s:
                return new SqlParameter(name, SqlDbType.NVarChar, s.Length > 4000 ? -1 : 4000) { Value = s };
            default:
                return new SqlParameter(name, value);
        }
    }

    // ------------------------------------------------------------------ Lõi

    private static SqlConnection OpenConnection()
    {
        var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    private static SqlCommand CreateCommand(SqlConnection conn, SqlTransaction? tx, string sql, List<object>? parameters)
    {
        var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = CommandTimeoutSeconds };
        cmd.Parameters.AddRange(BuildParameters(parameters).ToArray());
        return cmd;
    }

    private static DataTable QueryCore(SqlConnection conn, SqlTransaction? tx, string sql, List<object>? parameters)
    {
        using var cmd = CreateCommand(conn, tx, sql, parameters);
        using var adapter = new SqlDataAdapter(cmd);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }

    private static object? ScalarCore(SqlConnection conn, SqlTransaction? tx, string sql, List<object>? parameters)
    {
        using var cmd = CreateCommand(conn, tx, sql, parameters);
        var result = cmd.ExecuteScalar();
        return result == DBNull.Value ? null : result;
    }

    private static int NonQueryCore(SqlConnection conn, SqlTransaction? tx, string sql, List<object>? parameters)
    {
        using var cmd = CreateCommand(conn, tx, sql, parameters);
        return cmd.ExecuteNonQuery();
    }
}
