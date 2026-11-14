using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Quản lý loại đồ uống (UC28): danh sách, thêm, sửa, ngưng hoặc mở lại, xóa theo SPEC 3.14.</summary>
public class CategoryService
{
    public PagedResult<CategoryRow> Search(string? query, bool? active, int page, int pageSize)
    {
        var items = CategoryDAL.Search(query, active, Paging.Offset(page, pageSize), pageSize, out int total);
        return new PagedResult<CategoryRow>(items, page, pageSize, total);
    }

    public Category Get(int id) => CategoryDAL.GetById(id) ?? throw NotFound();

    public Category Save(int? id, string? name, bool active)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length == 0) throw FieldErrors.Of("Name", "Bạn chưa nhập tên loại đồ uống.");
        if (n.Length > 100) throw FieldErrors.Of("Name", "Tên loại tối đa 100 ký tự.");
        if (CategoryDAL.NameExists(n, id)) throw FieldErrors.Of("Name", "Đã có loại đồ uống cùng tên. Bạn đặt tên khác.");
        if (id is null)
        {
            var c = new Category { Name = n, Active = active };
            CategoryDAL.Insert(c);
            return c;
        }
        var item = Get(id.Value);
        item.Name = n;
        item.Active = active;
        CategoryDAL.Update(item);
        return item;
    }

    public DeleteDecision CheckDelete(int id)
    {
        var item = Get(id);
        return DeleteRules.Category(CategoryDAL.CountDrinks(id), item.Active);
    }

    /// <summary>Xóa loại: chỉ khi chưa có đồ uống nào; bị chặn thì DELETE_BLOCKED kèm lý do và gợi ý ngưng hoạt động.</summary>
    public void Delete(int id)
    {
        DBUtil.InTransaction((conn, tx) =>
        {
            var item = CategoryDAL.GetById(id, conn, tx) ?? throw NotFound();
            var decision = DeleteRules.Category(CategoryDAL.CountDrinks(id, conn, tx), item.Active);
            if (!decision.Allowed) throw Blocked(decision);
            CategoryDAL.Delete(id, conn, tx);
            return 0;
        });
    }

    public void SetActive(int id, bool active)
    {
        if (CategoryDAL.SetActive(id, active) == 0) throw NotFound();
    }

    internal static BusinessRuleException NotFound() => new(ErrorCodes.NotFound, ApiResponseText.NotFound);

    internal static BusinessRuleException NotFound(string message) => new(ErrorCodes.NotFound, message);

    internal static BusinessRuleException Blocked(DeleteDecision d) =>
        new(ErrorCodes.DeleteBlocked, d.Reason ?? "Không xóa được mục này. Bạn tải lại trang để xem trạng thái mới nhất.", new { reason = d.Reason, canDeactivate = d.CanDeactivate, suggestion = d.CanDeactivate ? "deactivate" : null });
}

/// <summary>Một dòng định mức gửi lên từ form món: nguyên liệu (của một rạp) và lượng dùng cho một phần.</summary>
public sealed record RecipeInput(int RawMaterialId, decimal Quantity);

/// <summary>Dữ liệu form thêm, sửa món.</summary>
public sealed record DrinkInput(int CategoryId, string? Name, long Price, string? Description, string? Image, bool IsCombo, bool Active, IReadOnlyList<RecipeInput> Recipes);

/// <summary>
/// Quản lý đồ uống và định mức (UC29, SPEC 3.11, 3.14): món dùng chung cho chuỗi; định mức trỏ tới nguyên liệu của từng rạp
/// (RawMaterials quản lý theo rạp). Combo là một món IsCombo = 1 với định mức gộp.
/// </summary>
public class DrinkService
{
    private readonly UploadService _uploads;

    public DrinkService(UploadService uploads) => _uploads = uploads;

    public PagedResult<DrinkRow> Search(string? query, int? categoryId, bool? active, int page, int pageSize)
    {
        var items = DrinkDAL.Search(query, categoryId, active, Paging.Offset(page, pageSize), pageSize, out int total);
        return new PagedResult<DrinkRow>(items, page, pageSize, total);
    }

    public Drink Get(int id) => DrinkDAL.GetById(id) ?? throw CategoryService.NotFound();

    public List<RecipeLine> Recipes(int drinkId) => ProductRecipeDAL.GetByDrink(drinkId);

    /// <summary>Nguyên liệu đang dùng của một rạp (RawMaterials quản lý theo rạp) cho bảng định mức.</summary>
    public List<RawMaterial> MaterialsOf(int cinemaId) => RawMaterialDAL.GetActiveByCinema(cinemaId);

    /// <summary>Lưu món và thay toàn bộ định mức trong một transaction. Đổi ảnh thì xóa ảnh cũ không còn ai dùng.</summary>
    public Drink Save(int? id, DrinkInput input, DateTime now)
    {
        var errors = new Dictionary<string, string>();
        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length == 0) errors["Name"] = "Bạn chưa nhập tên món.";
        else if (name.Length > 150) errors["Name"] = "Tên món tối đa 150 ký tự.";
        var category = CategoryDAL.GetById(input.CategoryId);
        if (category == null) errors["CategoryId"] = "Bạn chọn loại đồ uống cho món.";
        if (input.Price < 0) errors["Price"] = "Giá bán không được âm. Bạn nhập số tiền từ 0 trở lên.";
        else if (input.Price > 100_000_000) errors["Price"] = "Giá bán quá lớn, bạn kiểm tra lại.";
        if ((input.Description ?? string.Empty).Length > 1000) errors["Description"] = "Mô tả tối đa 1000 ký tự.";
        var image = string.IsNullOrWhiteSpace(input.Image) ? null : input.Image.Trim();
        if (image != null && !UploadService.IsUploadPath(image)) errors["Image"] = "Ảnh không hợp lệ. Bạn tải ảnh lên lại.";

        var materials = new Dictionary<int, RawMaterial>();
        var seen = new HashSet<int>();
        for (int i = 0; i < input.Recipes.Count; i++)
        {
            var r = input.Recipes[i];
            var m = RawMaterialDAL.GetById(r.RawMaterialId);
            if (m == null || !m.Active) { errors[$"Recipes[{i}].RawMaterialId"] = "Bạn chọn nguyên liệu cho dòng này."; continue; }
            if (!seen.Add(r.RawMaterialId)) { errors[$"Recipes[{i}].RawMaterialId"] = $"Nguyên liệu {m.Name} bị nhập hai lần. Bạn gộp vào một dòng."; continue; }
            if (r.Quantity <= 0) errors[$"Recipes[{i}].Quantity"] = "Định mức phải lớn hơn 0.";
            else if (r.Quantity > 1_000_000) errors[$"Recipes[{i}].Quantity"] = "Định mức quá lớn, bạn kiểm tra lại.";
            materials[r.RawMaterialId] = m;
        }
        if (errors.Count > 0) throw FieldErrors.Of(errors);

        string? oldImage = null;
        var saved = DBUtil.InTransaction((conn, tx) =>
        {
            Drink drink;
            if (id is null)
            {
                drink = new Drink { CreatedAt = now, IsSoldOut = false };
            }
            else
            {
                drink = DrinkDAL.GetById(id.Value, conn, tx) ?? throw CategoryService.NotFound();
                oldImage = drink.Image;
            }
            drink.CategoryId = input.CategoryId;
            drink.Name = name;
            drink.Price = input.Price;
            drink.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            drink.Image = image;
            drink.IsCombo = input.IsCombo;
            drink.Active = input.Active;
            if (id is null) DrinkDAL.Insert(drink, conn, tx);
            else DrinkDAL.Update(drink, conn, tx);

            ProductRecipeDAL.DeleteByDrink(drink.Id, conn, tx);
            foreach (var r in input.Recipes)
                ProductRecipeDAL.Insert(new ProductRecipe { DrinkId = drink.Id, RawMaterialId = r.RawMaterialId, Quantity = Math.Round(r.Quantity, 3) }, conn, tx);
            return drink;
        });
        if (oldImage != null && oldImage != saved.Image) _uploads.DeleteIfUnused(oldImage);
        return saved;
    }

    public DeleteDecision CheckDelete(int id)
    {
        var item = Get(id);
        return DeleteRules.Drink(DrinkDAL.CountBillLines(id), ProductRecipeDAL.CountByDrink(id), item.Active);
    }

    /// <summary>Xóa cứng chỉ khi món chưa có trong hóa đơn; xóa kèm định mức và ảnh. Bị chặn thì DELETE_BLOCKED, gợi ý ngưng bán.</summary>
    public void Delete(int id)
    {
        var image = DBUtil.InTransaction((conn, tx) =>
        {
            var item = DrinkDAL.GetById(id, conn, tx) ?? throw CategoryService.NotFound();
            var decision = DeleteRules.Drink(DrinkDAL.CountBillLines(id, conn, tx), ProductRecipeDAL.CountByDrink(id, conn, tx), item.Active);
            if (!decision.Allowed) throw CategoryService.Blocked(decision);
            ProductRecipeDAL.DeleteByDrink(id, conn, tx);
            DrinkDAL.Delete(id, conn, tx);
            return item.Image;
        });
        if (image != null) _uploads.DeleteIfUnused(image);
    }

    public void SetActive(int id, bool active)
    {
        if (DrinkDAL.SetActive(id, active) == 0) throw CategoryService.NotFound();
    }
}
