using Microsoft.Extensions.Configuration;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>Đồng hồ giả cho unit test (SPEC 2.7): trả đúng thời điểm được đặt, không đọc đồng hồ máy.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; set; }

    public DateTime Today => Now.Date;

    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

/// <summary>
/// CSDL thử cho test tích hợp. Mỗi lượt `dotnet test` dùng tên DB riêng ({DB thật}_{loại}_{8 ký tự ngẫu nhiên})
/// để nhiều lượt chạy cùng lúc không xóa DB của nhau; DB thử còn sót quá 1 giờ được dọn khi tạo mới.
/// </summary>
public static class TestDatabase
{
    public static string BaseConnectionString()
    {
        var cfg = new ConfigurationBuilder()
            .AddJsonFile(RepoPaths.File("appsettings.json"))
            .AddJsonFile(RepoPaths.File("appsettings.Development.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();
        var cs = cfg.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Cần ConnectionStrings:Default (appsettings.Development.json hoặc biến môi trường) để chạy test CSDL.");
        return cs;
    }

    /// <summary>Chuỗi kết nối tới một DB thử mới có tên duy nhất (DB chưa được tạo).</summary>
    public static string NewConnectionString(string kind)
    {
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(BaseConnectionString());
        var prefix = b.InitialCatalog + "_" + kind + "_";
        DropStale(prefix);
        b.InitialCatalog = prefix + Guid.NewGuid().ToString("N")[..8];
        return b.ConnectionString;
    }

    public static void Drop(string connectionString)
    {
        Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
        var db = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).InitialCatalog;
        Master(cmd =>
        {
            cmd.CommandText = "IF DB_ID(@db) IS NOT NULL BEGIN DECLARE @q NVARCHAR(300) = QUOTENAME(@db); " +
                              "EXEC (N'ALTER DATABASE ' + @q + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE'); EXEC (N'DROP DATABASE ' + @q); END";
            cmd.Parameters.AddWithValue("@db", db);
            cmd.ExecuteNonQuery();
        });
    }

    private static void DropStale(string prefix)
    {
        var stale = new List<string>();
        Master(cmd =>
        {
            cmd.CommandText = "SELECT name FROM sys.databases WHERE name LIKE @p AND create_date < @before";
            cmd.Parameters.AddWithValue("@p", prefix.Replace("_", "[_]") + "%");
            cmd.Parameters.AddWithValue("@before", DateTime.Now.AddHours(-1));
            using var r = cmd.ExecuteReader();
            while (r.Read()) stale.Add(r.GetString(0));
        });
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(BaseConnectionString());
        foreach (var name in stale)
        {
            b.InitialCatalog = name;
            Drop(b.ConnectionString);
        }
    }

    private static void Master(Action<Microsoft.Data.SqlClient.SqlCommand> work)
    {
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(BaseConnectionString()) { InitialCatalog = "master" };
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(b.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 120;
        work(cmd);
    }
}

/// <summary>Tìm đường dẫn trong repo từ thư mục chạy test.</summary>
public static class RepoPaths
{
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !System.IO.File.Exists(Path.Combine(dir.FullName, "FPolyCinema.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy FPolyCinema.sln");
        }
    }

    public static string Web => Path.Combine(Root, "src", "FPolyCinema.Web");

    public static string File(params string[] parts) => Path.Combine(new[] { Web }.Concat(parts).ToArray());
}
