using System.Text.RegularExpressions;

namespace FPolyCinema.Tests;

/// <summary>
/// Kiểm tra cấu trúc thư mục (SPEC 2.2) và mô hình 3 lớp bằng cách quét mã nguồn:
/// lớp giao diện → lớp nghiệp vụ (Services, Domain) → lớp dữ liệu (DAL, Entities, Utils/DBUtil).
/// Domain/Constants là bộ tên dùng chung cho mọi lớp (CONTRACTS mục 3), không chứa logic nên không tính là lớp.
/// </summary>
public class ArchitectureTests
{
    private static IEnumerable<(string File, string Code)> Sources(params string[] folder)
    {
        var dir = Path.Combine(new[] { RepoPaths.Web }.Concat(folder).ToArray());
        if (!Directory.Exists(dir)) return Array.Empty<(string, string)>();
        return Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Select(f => (Path.GetRelativePath(RepoPaths.Web, f), StripComments(System.IO.File.ReadAllText(f))));
    }

    private static string StripComments(string code) =>
        Regex.Replace(Regex.Replace(code, @"/\*[\s\S]*?\*/", ""), @"//[^\n]*", "");

    private static void AssertNone(IEnumerable<(string File, string Code)> files, string pattern, string why)
    {
        var bad = files.Where(f => Regex.IsMatch(f.Code, pattern)).Select(f => f.File).ToList();
        Assert.True(bad.Count == 0, $"{why}: {string.Join(", ", bad)}");
    }

    [Fact]
    public void Structure_FoldersAndFilesMatchSpec22()
    {
        Assert.True(System.IO.File.Exists(Path.Combine(RepoPaths.Root, "FPolyCinema.sln")));
        Assert.True(System.IO.File.Exists(Path.Combine(RepoPaths.Web, "FPolyCinema.Web.csproj")));
        Assert.True(System.IO.File.Exists(Path.Combine(RepoPaths.Root, "tests", "FPolyCinema.Tests", "FPolyCinema.Tests.csproj")));
        foreach (var d in new[] { "Controllers", "Views", "ViewModels", "Entities", "DAL", "Utils", "Services", Path.Combine("Domain", "Constants"),
                     Path.Combine("Domain", "Rules"), "Hubs", "Background", "Infrastructure", "Database", Path.Combine("wwwroot", "css"), Path.Combine("wwwroot", "js") })
            Assert.True(Directory.Exists(Path.Combine(RepoPaths.Web, d)), $"Thiếu thư mục {d}");
        foreach (var f in new[] { Path.Combine("Utils", "DBUtil.cs"), Path.Combine("Utils", "FileUtil.cs"), Path.Combine("Infrastructure", "IClock.cs"),
                     Path.Combine("Infrastructure", "DbInitializer.cs"), Path.Combine("Infrastructure", "DbSeeder.cs"), Path.Combine("Infrastructure", "SvgArtGenerator.cs"),
                     Path.Combine("Database", "01_schema.sql"), Path.Combine("Database", "02_procedures.sql") })
            Assert.True(System.IO.File.Exists(Path.Combine(RepoPaths.Web, f)), $"Thiếu file {f}");
        foreach (var f in new[] { "CLAUDE.md", "LAB-PROMPT.md", "PROGRESS.md", "README.md", Path.Combine("docs", "README.md"), Path.Combine("docs", "CONTRACTS.md"),
                     Path.Combine("docs", "TEST-SCENARIOS.md"), Path.Combine("docs", "BUG-LOG.md"), Path.Combine("tools", "spec-check", "check.mjs") })
            Assert.True(System.IO.File.Exists(Path.Combine(RepoPaths.Root, f)), $"Thiếu file {f}");
    }

    [Fact]
    public void Layers_DataLayerDoesNotCallBusinessOrPresentation()
    {
        var data = Sources("DAL").Concat(Sources("Entities")).Concat(Sources("Utils")).ToList();
        Assert.NotEmpty(data);
        AssertNone(data, @"\busing\s+FPolyCinema\.Web\.(Services|Controllers|ViewModels|Hubs|Domain\.Rules)\b", "Lớp dữ liệu không được gọi lên lớp trên");
        AssertNone(data, @"\b(BillStateMachine|PricingCalculator|SeatScoring|BillStatusService)\b", "Lớp dữ liệu không dùng quy tắc nghiệp vụ");
    }

    [Fact]
    public void Layers_EntitiesArePlainData()
    {
        AssertNone(Sources("Entities"), @"\b(DBUtil|SqlConnection|SqlCommand)\b|\busing\s+FPolyCinema\.Web\.DAL\b", "Entity chỉ chứa dữ liệu");
    }

    [Fact]
    public void Layers_DomainRulesArePureLogic()
    {
        var rules = Sources("Domain").ToList();
        Assert.NotEmpty(rules);
        AssertNone(rules, @"\b(DBUtil|SqlConnection|SqlCommand|DataTable)\b|\busing\s+FPolyCinema\.Web\.(DAL|Utils|Services)\b|\busing\s+Microsoft\.Data\.SqlClient\b",
            "Domain là logic thuần, không truy cập DB");
        AssertNone(rules, @"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b", "Domain không đọc đồng hồ máy");
    }

    [Fact]
    public void Layers_ServicesCallDalNotDBUtil()
    {
        var services = Sources("Services").ToList();
        Assert.NotEmpty(services);
        // Service gom nhiều bước bằng DBUtil.InTransaction (SPEC 2.3, lớp dữ liệu ngay dưới), nhưng câu SQL luôn nằm trong DAL
        AssertNone(services, @"\bDBUtil\.(QueryDataTable|queryDataTable|ExecuteScalar|executeScalar|ExecuteNonQuery|InParams)\s*\(", "Service gọi DAL, không tự chạy câu SQL");
        AssertNone(services, @"\busing\s+FPolyCinema\.Web\.(Controllers|ViewModels)\b", "Service không gọi lớp giao diện");
    }

    [Fact]
    public void Layers_ControllersAndViewsNeverCallDBUtil()
    {
        AssertNone(Sources("Controllers"), @"\bDBUtil\.\w+\s*\(|\bSqlConnection\b", "Controller không gọi thẳng DBUtil");
        var views = Directory.Exists(Path.Combine(RepoPaths.Web, "Views"))
            ? Directory.GetFiles(Path.Combine(RepoPaths.Web, "Views"), "*.cshtml", SearchOption.AllDirectories).Select(f => (f, System.IO.File.ReadAllText(f)))
            : Array.Empty<(string, string)>();
        AssertNone(views, @"\bDBUtil\b|\w+DAL\.\w+\(|SqlConnection", "View không truy cập dữ liệu");
    }

    [Fact]
    public void BillStatus_OnlyChangedThroughBillStatusService()
    {
        var all = Sources().ToList();
        // Chỉ BillStatusService gọi BillDAL.UpdateStatus (sau khi BillStateMachine.CanTransition cho phép)
        var callers = all.Where(f => Regex.IsMatch(f.Code, @"\bBillDAL\.UpdateStatus\s*\(")).Select(f => Path.GetFileName(f.File)).ToList();
        Assert.Equal(new[] { "BillStatusService.cs" }, callers);
        Assert.Contains(all, f => f.File.EndsWith("BillStatusService.cs") && f.Code.Contains("BillStateMachine.EnsureTransition("));
        // Chỉ BillDAL.UpdateStatus có câu SQL đổi cột Status của Bills
        var writers = all.Where(f => Regex.IsMatch(f.Code, @"UPDATE\s+Bills\s+SET[^""]*\bStatus\s*=", RegexOptions.IgnoreCase)).Select(f => Path.GetFileName(f.File)).ToList();
        Assert.Equal(new[] { "BillDAL.cs" }, writers);
        var bill = all.Single(f => f.File.EndsWith(Path.Combine("DAL", "BillDAL.cs"))).Code;
        Assert.Single(Regex.Matches(bill, @"UPDATE\s+Bills\s+SET\s+Status\s*=", RegexOptions.IgnoreCase));
        // Câu UPDATE chung của BillDAL.Update không đụng Status, CancelReason, PaidAt, CancelledAt
        var generalUpdate = Regex.Match(bill, @"UPDATE Bills SET Code[^""]*").Value;
        Assert.NotEmpty(generalUpdate);
        Assert.DoesNotMatch(@"\b(Status|CancelReason|PaidAt|CancelledAt)\s*=", generalUpdate);
    }

    [Fact]
    public void Data_OnlyDBUtilAndDbInitializerOpenConnections()
    {
        var all = Sources().Where(f => !f.File.EndsWith("DBUtil.cs") && !f.File.EndsWith("DbInitializer.cs")).ToList();
        AssertNone(all, @"new\s+Sql(Connection|Command|DataAdapter)\s*\(", "Chỉ Utils/DBUtil (và DbInitializer khi tạo DB qua master) được mở kết nối");
    }
}
