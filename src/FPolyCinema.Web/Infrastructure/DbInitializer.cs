using System.Text.RegularExpressions;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Khởi tạo CSDL khi ứng dụng khởi động (SPEC 2.4):
/// 1. Kết nối master, tạo DB nếu chưa có (resetDb = true thì xóa DB cũ trước).
/// 2. Chạy các file Database/*.sql theo thứ tự tên, tách batch theo dòng chỉ chứa chữ GO (SqlClient không hiểu GO).
/// 3. Ghi phiên bản (tên file) vào bảng SchemaVersions. File đã chạy thì bỏ qua, trừ file chỉ gồm
///    CREATE OR ALTER (stored procedure) được chạy lại mỗi lần để luôn cập nhật.
/// </summary>
public sealed class DbInitializer
{
    private static readonly Regex GoLine = new(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private readonly string _connectionString;
    private readonly string _scriptsDir;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DbInitializer(string connectionString, string scriptsDir, IClock clock, ILogger logger)
    {
        _connectionString = connectionString;
        _scriptsDir = scriptsDir;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Tên DB lấy từ chuỗi kết nối (Initial Catalog / Database).</summary>
    public string DatabaseName => new SqlConnectionStringBuilder(_connectionString).InitialCatalog;

    /// <summary>Chạy khởi tạo; trả về true nếu DB vừa được tạo mới.</summary>
    public bool Initialize(bool resetDb)
    {
        var dbName = DatabaseName;
        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException("Chuỗi kết nối chưa có tên CSDL (Database=...).");

        if (resetDb)
        {
            DropDatabase(dbName);
            _logger.LogWarning("Đã xóa CSDL {Db} theo tham số --reset-db.", dbName);
        }

        bool created = CreateDatabaseIfMissing(dbName);
        if (created) _logger.LogInformation("Đã tạo CSDL {Db}.", dbName);

        RunScripts();
        return created;
    }

    /// <summary>Tách nội dung một file .sql thành các batch theo dòng chỉ có chữ GO; bỏ batch rỗng.</summary>
    public static List<string> SplitBatches(string script) =>
        GoLine.Split(script.Replace("\r\n", "\n"))
            .Select(b => b.Trim())
            .Where(b => b.Length > 0 && !IsOnlyComments(b))
            .ToList();

    /// <summary>File chỉ gồm CREATE OR ALTER thì chạy lại được nhiều lần (stored procedure).</summary>
    public static bool IsRerunnable(IEnumerable<string> batches) =>
        batches.All(b => Regex.IsMatch(StripComments(b), @"^\s*CREATE\s+OR\s+ALTER\s+", RegexOptions.IgnoreCase));

    private void RunScripts()
    {
        if (!Directory.Exists(_scriptsDir))
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục script CSDL: {_scriptsDir}");

        var files = Directory.GetFiles(_scriptsDir, "*.sql").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList();
        var applied = SchemaVersionsExist()
            ? SchemaVersionDAL.GetAll().ToDictionary(v => v.Version, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, SchemaVersion>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var version = Path.GetFileName(file);
            var batches = SplitBatches(File.ReadAllText(file));
            bool rerunnable = IsRerunnable(batches);
            if (applied.ContainsKey(version) && !rerunnable) continue;

            DBUtil.InTransaction((conn, tx) =>
            {
                foreach (var batch in batches) DBUtil.ExecuteNonQuery(batch, null, conn, tx);
                var now = _clock.Now;
                if (applied.TryGetValue(version, out var existing))
                {
                    existing.AppliedAt = now;
                    SchemaVersionDAL.Update(existing, conn, tx);
                }
                else
                {
                    SchemaVersionDAL.Insert(new SchemaVersion { Version = version, AppliedAt = now }, conn, tx);
                }
            });
            _logger.LogInformation("Đã chạy {File} ({Count} batch).", version, batches.Count);
        }
    }

    private static bool SchemaVersionsExist()
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql)) == 1;
    }

    private string MasterConnectionString()
    {
        var b = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
        return b.ConnectionString;
    }

    private bool CreateDatabaseIfMissing(string dbName)
    {
        using var conn = new SqlConnection(MasterConnectionString());
        conn.Open();
        using (var check = new SqlCommand("SELECT DB_ID(@1)", conn))
        {
            check.Parameters.AddWithValue("@1", dbName);
            if (check.ExecuteScalar() is not DBNull and not null) return false;
        }
        // Tên DB không truyền được bằng tham số trong CREATE DATABASE: dùng QUOTENAME phía SQL Server
        using var create = new SqlCommand("DECLARE @sql NVARCHAR(400) = N'CREATE DATABASE ' + QUOTENAME(@1); EXEC (@sql);", conn);
        create.Parameters.AddWithValue("@1", dbName);
        create.CommandTimeout = 120;
        create.ExecuteNonQuery();
        return true;
    }

    private void DropDatabase(string dbName)
    {
        SqlConnection.ClearAllPools();
        using var conn = new SqlConnection(MasterConnectionString());
        conn.Open();
        const string sql = "IF DB_ID(@1) IS NOT NULL BEGIN DECLARE @q NVARCHAR(400) = QUOTENAME(@1); " +
                           "EXEC (N'ALTER DATABASE ' + @q + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE'); " +
                           "EXEC (N'DROP DATABASE ' + @q); END";
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@1", dbName);
        cmd.ExecuteNonQuery();
    }

    private static string StripComments(string sql) =>
        Regex.Replace(Regex.Replace(sql, @"/\*[\s\S]*?\*/", string.Empty), @"--[^\n]*", string.Empty);

    private static bool IsOnlyComments(string batch) => StripComments(batch).Trim().Length == 0;
}
