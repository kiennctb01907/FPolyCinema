using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Quản lý nguyên liệu và nhập kho (UC30, SPEC 3.11-14; ManagerOnly; UI A2, A3): danh sách theo rạp có lọc, thêm và sửa nguyên liệu (tên, đơn vị, ngưỡng tối thiểu),
/// ngưng hoặc dùng lại, phiếu nhập kho đơn giản (IMPORT). Tồn kho chỉ đổi qua giao dịch kho, không sửa tay.
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("admin/materials")]
public class MaterialsController : AppControllerBase
{
    private readonly CinemaContextService _cinema;
    private readonly InventoryService _inventory;
    private readonly IClock _clock;
    private readonly RealtimeNotifier _notifier;

    public MaterialsController(CinemaContextService cinema, InventoryService inventory, IClock clock, RealtimeNotifier notifier)
    {
        _cinema = cinema;
        _inventory = inventory;
        _clock = clock;
        _notifier = notifier;
    }

    [HttpGet("")]
    public IActionResult Index(string? q, string? status, bool low = false)
    {
        var vm = new MaterialListVm
        {
            Rows = RawMaterialDAL.Search(_cinema.CurrentCinemaId, q, ParseStatus(status), low), Q = q, Status = status, LowOnly = low,
            ScopeName = _cinema.Current?.Name ?? "Toàn chuỗi",
        };
        return IsPartialRequest ? PartialView("_Table", vm) : View(vm);
    }

    private MaterialFormVm Form(RawMaterial? m) => new()
    {
        Id = m?.Id, CinemaId = m?.CinemaId ?? _cinema.CurrentCinemaId ?? _cinema.Cinemas.First().Id, Name = m?.Name, Unit = m?.Unit, MinThreshold = m?.MinThreshold ?? 0,
        Active = m?.Active ?? true, ActualStock = m?.ActualStock ?? 0, ReservedStock = m?.ReservedStock ?? 0, Cinemas = _cinema.Cinemas.ToList(),
        CanChooseCinema = m == null && _cinema.CanChoose, History = m == null ? new() : InventoryTransactionDAL.ByMaterial(m.Id, 20),
    };

    [HttpGet("create")]
    public IActionResult Create() => View("Form", Form(null));

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        var m = RawMaterialDAL.GetById(id);
        if (m == null || (_cinema.BoundCinemaId != null && m.CinemaId != _cinema.BoundCinemaId)) return NotFoundPage("Không tìm thấy nguyên liệu.");
        return View("Form", Form(m));
    }

    [HttpPost("create")]
    public IActionResult Create(MaterialFormVm input) => Save(null, input);

    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, MaterialFormVm input) => Save(id, input);

    private IActionResult Save(int? id, MaterialFormVm input)
    {
        var existing = id is int i ? RawMaterialDAL.GetById(i) : null;
        if (id != null && existing == null) return NotFoundPage("Không tìm thấy nguyên liệu.");
        int cinemaId = existing?.CinemaId ?? (_cinema.BoundCinemaId ?? input.CinemaId);
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 100) ModelState.AddModelError(nameof(input.Name), "Tên nguyên liệu từ 2 đến 100 ký tự.");
        else if (RawMaterialDAL.NameExists(cinemaId, name, id)) ModelState.AddModelError(nameof(input.Name), "Rạp đã có nguyên liệu tên này.");
        if (string.IsNullOrWhiteSpace(input.Unit) || input.Unit.Trim().Length > 20) ModelState.AddModelError(nameof(input.Unit), "Bạn nhập đơn vị (ví dụ: g, ml, cái).");
        if (input.MinThreshold < 0) ModelState.AddModelError(nameof(input.MinThreshold), "Ngưỡng tối thiểu không được âm.");
        if (!_cinema.Cinemas.Any(c => c.Id == cinemaId)) ModelState.AddModelError(nameof(input.CinemaId), "Bạn chọn rạp.");
        foreach (var k in ModelState.Keys.Where(k => k is nameof(input.ActualStock) or nameof(input.ReservedStock) or nameof(input.History) or nameof(input.Cinemas)).ToList()) ModelState.Remove(k);
        if (!ModelState.IsValid)
        {
            var vm = Form(existing);
            vm.Name = input.Name; vm.Unit = input.Unit; vm.MinThreshold = input.MinThreshold; vm.Active = input.Active; vm.CinemaId = cinemaId;
            return View("Form", vm);
        }
        var m = existing ?? new RawMaterial { CinemaId = cinemaId };
        m.Name = name;
        m.Unit = input.Unit!.Trim();
        m.MinThreshold = Math.Round(input.MinThreshold, 3);
        m.Active = input.Active;
        if (existing == null) RawMaterialDAL.Insert(m); else RawMaterialDAL.Update(m);
        Toast("success", existing == null ? $"Đã thêm nguyên liệu {m.Name}." : "Đã lưu thay đổi");
        return Redirect("/admin/materials");
    }

    [HttpPost("{id:int}/set-active")]
    public IActionResult SetActive(int id, bool active)
    {
        var m = RawMaterialDAL.GetById(id) ?? throw CategoryService.NotFound("Không tìm thấy nguyên liệu.");
        RawMaterialDAL.SetActive(m.Id, active);
        return ApiOk(new { message = active ? $"Đã dùng lại {m.Name}." : $"Đã ngưng {m.Name}." });
    }

    // ------------------------------------------------------------------ nhập kho

    [HttpGet("import")]
    public IActionResult Import(int? cinemaId) => View(ImportVm(cinemaId, null));

    private StockFormVm ImportVm(int? cinemaId, string? note)
    {
        int id = _cinema.BoundCinemaId ?? (cinemaId is int c && _cinema.Cinemas.Any(x => x.Id == c) ? c : _cinema.CurrentCinemaId ?? _cinema.Cinemas.First().Id);
        return new StockFormVm
        {
            CinemaId = id, CinemaName = _cinema.Cinemas.FirstOrDefault(c => c.Id == id)?.Name ?? string.Empty, IsImport = true, Note = note,
            Materials = RawMaterialDAL.ByCinema(id).OrderBy(m => m.Name).ToList(),
        };
    }

    /// <summary>Phiếu nhập kho (IMPORT): cộng ActualStock từng dòng; món khả dụng lại thì phát PRODUCT_AVAILABLE.</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(int cinemaId, [FromForm] int[]? materialIds, [FromForm] string[]? quantities, string? note)
    {
        int id = _cinema.BoundCinemaId ?? cinemaId;
        if (!_cinema.Cinemas.Any(c => c.Id == id)) throw FieldErrors.Of("cinemaId", "Bạn chọn rạp.");
        var before = InventoryService.Snapshot(id);
        int n = _inventory.Import(id, KitchenController.StockLines(materialIds, quantities, null), note, CurrentUserId, _clock.Now);
        var alerts = await InventoryService.PublishAlerts(_notifier, before);
        return ApiOk(new { message = $"Đã nhập kho {n} nguyên liệu." + (alerts.Available.Count > 0 ? $" {alerts.Available.Count} món bán lại được." : string.Empty) });
    }
}
