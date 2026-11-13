namespace FPolyCinema.Web.Entities.ReadModels;

// Bản ghi đọc (không ứng với một bảng) mà DAL trả về cho các màn hình danh sách của Phase 2.

/// <summary>Một dòng danh sách loại đồ uống kèm số món.</summary>
public sealed record CategoryRow(Category Category, int DrinkCount);

/// <summary>Một dòng danh sách đồ uống kèm tên loại và số dòng định mức.</summary>
public sealed record DrinkRow(Drink Drink, string CategoryName, int RecipeCount);

/// <summary>Một dòng định mức kèm thông tin nguyên liệu (rạp, tên, đơn vị).</summary>
public sealed record RecipeLine(int Id, int DrinkId, int RawMaterialId, decimal Quantity, int CinemaId, string MaterialName, string Unit);

/// <summary>Một yêu cầu phê duyệt đang chờ, kèm mã hóa đơn và tên người yêu cầu (chuông phê duyệt của topbar).</summary>
public sealed record PendingApprovalRow(int Id, string BillCode, string RequesterName, string Reason, DateTime CreatedAt);
