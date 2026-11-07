using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng SupervisorApprovals (yêu cầu phê duyệt của quản lý). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class SupervisorApprovalDAL
{
    public static SupervisorApproval FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        Type = row.GetString("Type"),
        BillId = row.GetInt("BillId"),
        RequestedBy = row.GetInt("RequestedBy"),
        Reason = row.GetString("Reason"),
        Status = row.GetString("Status"),
        Method = row.GetStringN("Method"),
        DecidedBy = row.GetIntN("DecidedBy"),
        DecidedAt = row.GetDateTimeN("DecidedAt"),
        ExpiresAt = row.GetDateTime("ExpiresAt"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<SupervisorApproval> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<SupervisorApproval> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Type, BillId, RequestedBy, Reason, Status, Method, DecidedBy, DecidedAt, ExpiresAt, CreatedAt FROM SupervisorApprovals ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static SupervisorApproval? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Type, BillId, RequestedBy, Reason, Status, Method, DecidedBy, DecidedAt, ExpiresAt, CreatedAt FROM SupervisorApprovals WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(SupervisorApproval item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO SupervisorApprovals (Type, BillId, RequestedBy, Reason, Status, Method, DecidedBy, DecidedAt, ExpiresAt, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.Type, item.BillId, item.RequestedBy, item.Reason, item.Status, DBUtil.Value(item.Method), DBUtil.Value(item.DecidedBy), DBUtil.Value(item.DecidedAt), item.ExpiresAt, item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(SupervisorApproval item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE SupervisorApprovals SET Type = @1, BillId = @2, RequestedBy = @3, Reason = @4, Status = @5, Method = @6, DecidedBy = @7, DecidedAt = @8, ExpiresAt = @9, CreatedAt = @10 WHERE Id = @11";
        var ps = new List<object> { item.Type, item.BillId, item.RequestedBy, item.Reason, item.Status, DBUtil.Value(item.Method), DBUtil.Value(item.DecidedBy), DBUtil.Value(item.DecidedAt), item.ExpiresAt, item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM SupervisorApprovals WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
