using System.ComponentModel.DataAnnotations;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Danh sách loại đồ uống (A2).</summary>
public class CategoryListVm
{
    public PagedResult<CategoryRow> Result { get; set; } = new(Array.Empty<CategoryRow>(), 1, Paging.DefaultPageSize, 0);
    public string? Query { get; set; }
    public string? Status { get; set; }
    public bool CanManage { get; set; }
    public bool HasFilter => !string.IsNullOrWhiteSpace(Query) || !string.IsNullOrEmpty(Status);
}

/// <summary>Form thêm, sửa loại đồ uống (A3).</summary>
public class CategoryFormVm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Bạn chưa nhập tên loại đồ uống.")]
    [StringLength(100, ErrorMessage = "Tên loại tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    public bool Active { get; set; } = true;
}

/// <summary>Chi tiết loại đồ uống: thông tin và các món thuộc loại.</summary>
public class CategoryDetailsVm
{
    public Category Category { get; set; } = new();
    public List<DrinkRow> Drinks { get; set; } = new();
    public int DrinkTotal { get; set; }
    public bool CanManage { get; set; }
}

/// <summary>Danh sách đồ uống (A2): lọc theo loại, tìm theo tên (Lab 5 bài 3), lọc trạng thái.</summary>
public class DrinkListVm
{
    public PagedResult<DrinkRow> Result { get; set; } = new(Array.Empty<DrinkRow>(), 1, Paging.DefaultPageSize, 0);
    public string? Query { get; set; }
    public int? CategoryId { get; set; }
    public string? Status { get; set; }
    public List<Category> Categories { get; set; } = new();
    public bool CanManage { get; set; }
    public bool HasFilter => !string.IsNullOrWhiteSpace(Query) || CategoryId.HasValue || !string.IsNullOrEmpty(Status);
}

/// <summary>Một dòng định mức trên form món.</summary>
public class RecipeRowVm
{
    public int CinemaId { get; set; }
    public int RawMaterialId { get; set; }
    public decimal Quantity { get; set; }
}

/// <summary>Nguyên liệu của một rạp để chọn trong bảng định mức.</summary>
public sealed record CinemaMaterialsVm(int CinemaId, string CinemaName, IReadOnlyList<RawMaterial> Materials);

/// <summary>Form thêm, sửa đồ uống (A3): thông tin, ảnh 1:1 qua FileController, định mức theo từng rạp.</summary>
public class DrinkFormVm
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Bạn chọn loại đồ uống cho món.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Bạn chưa nhập tên món.")]
    [StringLength(150, ErrorMessage = "Tên món tối đa 150 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100_000_000, ErrorMessage = "Giá bán từ 0 đến 100.000.000 ₫.")]
    public long Price { get; set; }

    [StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string? Description { get; set; }

    public string? Image { get; set; }
    public bool IsCombo { get; set; }
    public bool Active { get; set; } = true;
    public List<RecipeRowVm> Recipes { get; set; } = new();

    // Dữ liệu cho ô chọn
    public List<Category> Categories { get; set; } = new();
    public List<CinemaMaterialsVm> CinemaMaterials { get; set; } = new();
}

/// <summary>Chi tiết đồ uống: thông tin và định mức theo rạp.</summary>
public class DrinkDetailsVm
{
    public Drink Drink { get; set; } = new();
    public string CategoryName { get; set; } = string.Empty;
    public List<(string CinemaName, List<RecipeLine> Lines)> RecipesByCinema { get; set; } = new();
    public bool CanManage { get; set; }
}

/// <summary>Danh sách nhân viên (A2): lọc theo vai trò và rạp.</summary>
public class StaffListVm
{
    public PagedResult<User> Result { get; set; } = new(Array.Empty<User>(), 1, Paging.DefaultPageSize, 0);
    public string? Query { get; set; }
    public string? Role { get; set; }
    public int? CinemaId { get; set; }
    public string? Status { get; set; }
    public List<Cinema> Cinemas { get; set; } = new();
    public int CurrentUserId { get; set; }
    public bool HasFilter => !string.IsNullOrWhiteSpace(Query) || !string.IsNullOrEmpty(Role) || CinemaId.HasValue || !string.IsNullOrEmpty(Status);
}

/// <summary>Form thêm, sửa nhân viên (A3).</summary>
public class StaffFormVm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Bạn chưa nhập email.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng, ví dụ ten@fpolycinema.vn.")]
    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bạn chưa nhập họ tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression(@"^\+?\d{9,11}$", ErrorMessage = "Số điện thoại gồm 9–11 chữ số, ví dụ 0901234567.")]
    public string? Phone { get; set; }

    /// <summary>PIN quản lý 6 số (quản lý ca, quản lý rạp); để trống khi sửa là giữ PIN cũ.</summary>
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN gồm đúng 6 chữ số.")]
    public string? ManagerPin { get; set; }

    /// <summary>Tài khoản đang sửa đã có PIN quản lý.</summary>
    public bool HasPin { get; set; }

    [Required(ErrorMessage = "Bạn chọn vai trò cho nhân viên.")]
    public string Role { get; set; } = string.Empty;

    public int? CinemaId { get; set; }

    public bool IsSelf { get; set; }
    public List<Cinema> Cinemas { get; set; } = new();
}

/// <summary>Chi tiết nhân viên.</summary>
public class StaffDetailsVm
{
    public User User { get; set; } = new();
    public string? CinemaName { get; set; }
    public int BillsCreated { get; set; }
    public bool IsSelf { get; set; }
}

/// <summary>Hộp thư thử /dev/mailbox (UI D2).</summary>
public class MailboxVm
{
    public List<EmailOutbox> Items { get; set; } = new();
    public EmailOutbox? Selected { get; set; }
    public int Total { get; set; }
}

/// <summary>Trang /dev/style-guide (UI D1): dữ liệu thật từ CSDL cho thẻ phim, thẻ món, kèm ví dụ dài để thử bố cục.</summary>
public class StyleGuideVm
{
    public List<MovieCardVm> Movies { get; set; } = new();
    public List<FoodCardVm> Foods { get; set; } = new();
    public SeatMapVm SeatMap { get; set; } = new();
    public QrBlockVm? Qr { get; set; }
    public PagedResult<DrinkRow> SampleTable { get; set; } = new(Array.Empty<DrinkRow>(), 1, 5, 0);
    public DateTime Now { get; set; }
}
