using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Seats (ghế trong phòng). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class SeatDAL
{
    public static Seat FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        AuditoriumId = row.GetInt("AuditoriumId"),
        RowIndex = row.GetInt("RowIndex"),
        ColIndex = row.GetInt("ColIndex"),
        Label = row.GetString("Label"),
        SeatType = row.GetString("SeatType"),
        PairGroupId = row.GetIntN("PairGroupId"),
        IsHouseSeat = row.GetBool("IsHouseSeat"),
        IsMaintenance = row.GetBool("IsMaintenance"),
        BaseViewScore = row.GetInt("BaseViewScore"),
    };

    public static List<Seat> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Seat> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, AuditoriumId, RowIndex, ColIndex, Label, SeatType, PairGroupId, IsHouseSeat, IsMaintenance, BaseViewScore FROM Seats ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Seat? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, AuditoriumId, RowIndex, ColIndex, Label, SeatType, PairGroupId, IsHouseSeat, IsMaintenance, BaseViewScore FROM Seats WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Seat item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Seats (AuditoriumId, RowIndex, ColIndex, Label, SeatType, PairGroupId, IsHouseSeat, IsMaintenance, BaseViewScore) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.AuditoriumId, item.RowIndex, item.ColIndex, item.Label, item.SeatType, DBUtil.Value(item.PairGroupId), item.IsHouseSeat, item.IsMaintenance, item.BaseViewScore };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Seat item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Seats SET AuditoriumId = @1, RowIndex = @2, ColIndex = @3, Label = @4, SeatType = @5, PairGroupId = @6, IsHouseSeat = @7, IsMaintenance = @8, BaseViewScore = @9 WHERE Id = @10";
        var ps = new List<object> { item.AuditoriumId, item.RowIndex, item.ColIndex, item.Label, item.SeatType, DBUtil.Value(item.PairGroupId), item.IsHouseSeat, item.IsMaintenance, item.BaseViewScore, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Seats WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
