using System.Data;
using System.Text.RegularExpressions;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Tests;

/// <summary>Kiểm quy ước dữ liệu của CONTRACTS mục 9 trên CSDL thật (tạo bằng 01_schema.sql) và trên dữ liệu mẫu.</summary>
[Collection("Database")]
public class ConventionTests
{
    private readonly DatabaseFixture _schemaDb;
    private readonly SeededDatabaseFixture _seedDb;

    public ConventionTests(DatabaseFixture schemaDb, SeededDatabaseFixture seedDb)
    {
        _schemaDb = schemaDb;
        _seedDb = seedDb;
    }

    private DataTable Schema(string sql)
    {
        DBUtil.Init(_schemaDb.ConnectionString);
        return DBUtil.QueryDataTable(sql);
    }

    private const string ColumnsSql =
        "SELECT t.name AS T, c.name AS C, ty.name AS Ty, c.precision AS P, c.scale AS S, c.is_identity AS Ident " +
        "FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id WHERE t.is_ms_shipped = 0";

    private static readonly HashSet<string> MoneyColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Price", "ListPrice", "FinalPrice", "Amount", "Total", "TicketSubtotal", "FnbSubtotal", "DiscountTotal", "WalletBalance", "BalanceAfter",
        "OpeningCashFloat", "ActualCashCounted", "ExpectedCash", "Discrepancy", "MinimumTicketPrice", "ContractAmount", "MinBillAmount", "LineFinalAmount",
    };

    [Fact]
    public void Naming_PluralPascalCaseTablesColumnsAndIdKeys()
    {
        var tables = Schema("SELECT name FROM sys.tables WHERE is_ms_shipped = 0").Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();
        Assert.Equal(35, tables.Count);
        // Bảng số nhiều PascalCase (EmailOutbox là tên spec đặt cho hộp thư)
        Assert.All(tables, t => Assert.Matches("^[A-Z][a-z]+([A-Z][a-z]+)*$", t));
        Assert.All(tables.Where(t => t != "EmailOutbox"), t => Assert.EndsWith("s", t));
        var cols = Schema(ColumnsSql).Rows.Cast<DataRow>().ToList();
        Assert.All(cols, r => Assert.Matches("^[A-Z][A-Za-z0-9]*$", (string)r["C"]));

        var pk = Schema("SELECT t.name AS T, c.name AS C FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id " +
                        "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id JOIN sys.tables t ON t.object_id = i.object_id WHERE i.is_primary_key = 1")
            .Rows.Cast<DataRow>().GroupBy(r => (string)r["T"]).ToDictionary(g => g.Key, g => g.Select(r => (string)r["C"]).OrderBy(x => x).ToArray());
        var exceptions = new Dictionary<string, string[]>
        {
            ["ShowtimeSeats"] = new[] { "SeatId", "ShowtimeId" },
            ["Holidays"] = new[] { "HolidayDate" },
            ["AudienceDiscounts"] = new[] { "AudienceType" },
        };
        foreach (var t in tables)
        {
            if (exceptions.TryGetValue(t, out var cols2)) { Assert.Equal(cols2, pk[t]); continue; }
            Assert.Equal(new[] { "Id" }, pk[t]);
            var id = cols.Single(r => (string)r["T"] == t && (string)r["C"] == "Id");
            Assert.Equal(("int", true), ((string)id["Ty"], (bool)id["Ident"]));
        }

        // Khóa ngoại tên {Tên}Id; các cột người thực hiện theo spec (CreatedBy, DecidedBy, RequestedBy, CheckedInBy) kết thúc bằng By
        var fks = Schema("SELECT c.name AS C FROM sys.foreign_key_columns fk JOIN sys.columns c ON c.object_id = fk.parent_object_id AND c.column_id = fk.parent_column_id")
            .Rows.Cast<DataRow>().Select(r => (string)r[0]).Distinct().ToList();
        Assert.NotEmpty(fks);
        Assert.All(fks, c => Assert.Matches("(Id|By)$", c));
    }

    [Fact]
    public void Types_MoneyBigintPercentDecimal52StockDecimal183()
    {
        var cols = Schema(ColumnsSql).Rows.Cast<DataRow>().ToList();
        string[] stockTables = { "RawMaterials", "ProductRecipes", "InventoryTransactions", "WastageLogItems" };
        foreach (var r in cols)
        {
            string t = (string)r["T"], c = (string)r["C"], ty = (string)r["Ty"];
            int p = Convert.ToInt32(r["P"]), s = Convert.ToInt32(r["S"]);
            if (MoneyColumns.Contains(c)) Assert.True(ty == "bigint", $"{t}.{c} là tiền nhưng kiểu {ty}");
            if (c.EndsWith("Percent") || c == "VatRate") Assert.True(ty == "decimal" && p == 5 && s == 2, $"{t}.{c} là phần trăm nhưng kiểu {ty}({p},{s})");
            if (stockTables.Contains(t) && c is "ActualStock" or "ReservedStock" or "MinThreshold" or "Quantity")
                Assert.True(ty == "decimal" && p == 18 && s == 3, $"{t}.{c} là tồn kho nhưng kiểu {ty}({p},{s})");
            Assert.NotEqual("money", ty);
            Assert.NotEqual("float", ty);
        }
    }

    [Fact]
    public void Types_TimesDatetime2ScaleZeroDatesDateNoServerClock()
    {
        var cols = Schema(ColumnsSql).Rows.Cast<DataRow>().ToList();
        foreach (var r in cols)
        {
            string t = (string)r["T"], c = (string)r["C"], ty = (string)r["Ty"];
            Assert.DoesNotContain(ty, new[] { "datetime", "smalldatetime", "datetimeoffset" });
            if (ty == "datetime2") Assert.True(Convert.ToInt32(r["S"]) == 0, $"{t}.{c} phải DATETIME2(0)");
            if (Regex.IsMatch(c, "(At|Time|Until|From)$")) Assert.True(ty == "datetime2", $"{t}.{c} là thời điểm nhưng kiểu {ty}");
            if (c.EndsWith("Date")) Assert.True(ty == "date", $"{t}.{c} là ngày nhưng kiểu {ty}");
        }
        // Không DEFAULT, CHECK hay procedure nào gọi đồng hồ của SQL Server: thời gian luôn do IClock truyền vào
        var defs = Schema("SELECT definition FROM sys.default_constraints UNION ALL SELECT definition FROM sys.check_constraints UNION ALL SELECT definition FROM sys.sql_modules")
            .Rows.Cast<DataRow>().Select(r => ((string)r[0]).ToLowerInvariant()).ToList();
        Assert.All(defs, d => Assert.DoesNotMatch(@"getdate|sysdatetime|getutcdate|sysutcdatetime|current_timestamp", d));
    }

    [Fact]
    public void Codes_SeededDataFollowsFormats()
    {
        DBUtil.Init(_seedDb.ConnectionString);
        var bills = DBUtil.QueryDataTable("SELECT Code, CreatedAt FROM Bills").Rows.Cast<DataRow>().ToList();
        Assert.All(bills, r =>
        {
            var code = (string)r["Code"];
            Assert.Matches("^HD\\d{14}[A-Z]{4}$", code);
            Assert.Equal(((DateTime)r["CreatedAt"]).ToString("yyyyMMddHHmmss"), code.Substring(2, 14));
        });
        Assert.All(DBUtil.QueryDataTable("SELECT QrCode FROM Tickets").Rows.Cast<DataRow>(), r => Assert.Matches("^TK[A-Z0-9]{12}$", (string)r[0]));
        Assert.All(DBUtil.QueryDataTable("SELECT MemberCode FROM Users WHERE Role = 'Customer'").Rows.Cast<DataRow>(), r => Assert.Matches("^TV\\d{8}$", (string)r[0]));
        Assert.Equal(0, Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Users WHERE Role <> 'Customer' AND MemberCode IS NOT NULL")));
        Assert.All(DBUtil.QueryDataTable("SELECT DISTINCT TerminalCode FROM PosShifts UNION SELECT DISTINCT TerminalCode FROM Bills WHERE TerminalCode IS NOT NULL").Rows.Cast<DataRow>(),
            r => Assert.Matches("^POS-\\d{2}$", (string)r[0]));
        // Số gọi bắp nước 1–999, đếm lại mỗi ngày theo rạp: trong một ngày của một rạp không trùng số
        Assert.Equal(0, Convert.ToInt32(DBUtil.ExecuteScalar(
            "SELECT COUNT(*) FROM (SELECT CinemaId, CAST(DATEADD(HOUR, -6, PaidAt) AS DATE) AS D, PickupNumber FROM Bills WHERE Channel = 'POS' AND PickupNumber IS NOT NULL GROUP BY CinemaId, CAST(DATEADD(HOUR, -6, PaidAt) AS DATE), PickupNumber HAVING COUNT(*) > 1) x")));
        Assert.Equal(1, Convert.ToInt32(DBUtil.ExecuteScalar("SELECT MIN(PickupNumber) FROM Bills WHERE PickupNumber IS NOT NULL")));
        Assert.True(Convert.ToInt32(DBUtil.ExecuteScalar("SELECT MAX(PickupNumber) FROM Bills")) <= 999);
        Assert.True(Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(DISTINCT CAST(PaidAt AS DATE)) FROM Bills WHERE PickupNumber = 1")) >= 25);
    }
}
