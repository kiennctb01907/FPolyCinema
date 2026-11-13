using System.Text.Json;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Quản lý phòng chiếu và công cụ thiết kế sơ đồ ghế (UC32, UI A4; SPEC 3.2, 3.14, 8.3): công cụ tô ghế thường, VIP, cặp ghế đôi,
/// xe lăn, lối đi, ghế nội bộ, bảo trì; "Tự đánh số", "Gợi ý ghế nội bộ", heatmap BaseViewScore. Chỉ quản lý rạp (cấu hình).
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("admin/auditoriums")]
public class AuditoriumsController : AppControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly AuditoriumService _service;
    private readonly CinemaContextService _cinemas;
    private readonly CinemaRules _rules;
    private readonly IClock _clock;

    public AuditoriumsController(AuditoriumService service, CinemaContextService cinemas, CinemaRules rules, IClock clock)
    {
        _service = service;
        _cinemas = cinemas;
        _rules = rules;
        _clock = clock;
    }

    [HttpGet("")]
    public IActionResult Index(int? cinemaId)
    {
        var allowed = _cinemas.Cinemas.Select(c => c.Id).ToHashSet();
        int? filter = cinemaId is int c && allowed.Contains(c) ? c : _cinemas.BoundCinemaId;
        var rows = _service.List(filter).Where(r => allowed.Contains(r.Auditorium.CinemaId)).ToList();
        return View(new AuditoriumListVm { Rows = rows, Cinemas = _cinemas.Cinemas.ToList(), CinemaId = filter });
    }

    [HttpGet("create")]
    public IActionResult Create(int? cinemaId) => FormView(new AuditoriumFormVm { CinemaId = cinemaId ?? _cinemas.CurrentCinemaId ?? _cinemas.Cinemas.FirstOrDefault()?.Id ?? 0 });

    [HttpPost("create")]
    public IActionResult Create(AuditoriumFormVm vm)
    {
        vm.Id = null;
        if (!ModelState.IsValid) return FormView(vm);
        if (_cinemas.Cinemas.All(c => c.Id != vm.CinemaId)) { ModelState.AddModelError(nameof(vm.CinemaId), "Bạn chọn rạp của phòng."); return FormView(vm); }
        try
        {
            var a = _service.Create(new AuditoriumInput(vm.CinemaId, vm.Name, vm.Format, vm.TotalRows, vm.TotalCols, vm.IdealRowRatio, vm.Active));
            Toast("success", $"Đã tạo “{a.Name}” với lưới {a.TotalRows} × {a.TotalCols} ghế thường. Tô lối đi và các loại ghế rồi bấm Lưu sơ đồ.");
            return Redirect($"/admin/auditoriums/{a.Id}/design");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return FormView(vm);
        }
    }

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        try
        {
            var a = _service.Get(id);
            return FormView(new AuditoriumFormVm { Id = a.Id, CinemaId = a.CinemaId, Name = a.Name, Format = a.Format, TotalRows = a.TotalRows, TotalCols = a.TotalCols, IdealRowRatio = a.IdealRowRatio, Active = a.Active });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, AuditoriumFormVm vm)
    {
        vm.Id = id;
        ModelState.Remove(nameof(vm.TotalRows));
        ModelState.Remove(nameof(vm.TotalCols));
        if (!ModelState.IsValid) return FormView(vm);
        try
        {
            var a = _service.UpdateInfo(id, new AuditoriumInput(vm.CinemaId, vm.Name, vm.Format, 0, 0, vm.IdealRowRatio, vm.Active));
            Toast("success", "Đã lưu thay đổi");
            return Redirect($"/admin/auditoriums/{a.Id}/design");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return FormView(vm);
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    private IActionResult FormView(AuditoriumFormVm vm)
    {
        vm.Cinemas = _cinemas.Cinemas.ToList();
        vm.CinemaName = vm.Cinemas.FirstOrDefault(c => c.Id == vm.CinemaId)?.Name;
        return View("Form", vm);
    }

    /// <summary>A4. Công cụ thiết kế phòng chiếu.</summary>
    [HttpGet("{id:int}/design")]
    public IActionResult Design(int id)
    {
        try
        {
            var a = _service.Get(id);
            if (_cinemas.Cinemas.All(c => c.Id != a.CinemaId)) return NotFoundPage();
            var seats = _service.Seats(id);
            var cells = seats.Select(s => new LayoutCell(s.RowIndex, s.ColIndex, s.SeatType, s.IsHouseSeat, s.IsMaintenance)).ToList();
            return View(new AuditoriumDesignVm
            {
                Auditorium = a,
                CinemaName = _cinemas.Cinemas.First(c => c.Id == a.CinemaId).Name,
                Cells = seats.Select(s => new DesignCellVm(s.RowIndex, s.ColIndex, s.SeatType, s.IsHouseSeat, s.IsMaintenance, s.Label, s.BaseViewScore, AuditoriumLayout.HeatLevel(s.BaseViewScore))).ToList(),
                StructureLocked = _service.IsStructureLocked(id, _clock.Now),
                HouseSeatsPerAuditorium = _rules.HouseSeatsPerAuditorium,
                Stats = AuditoriumLayout.Stats(cells),
            });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    /// <summary>Sơ đồ gửi từ công cụ thiết kế: {"rows":8,"cols":12,"cells":[{"r":1,"c":1,"t":"STANDARD","h":false,"m":false}]}.</summary>
    public sealed class LayoutDto
    {
        public int Rows { get; set; }
        public int Cols { get; set; }
        public List<CellDto> Cells { get; set; } = new();
    }

    public sealed class CellDto
    {
        public int R { get; set; }
        public int C { get; set; }
        public string T { get; set; } = SeatType.Standard;
        public bool H { get; set; }
        public bool M { get; set; }
    }

    private static LayoutDto ParseLayout(string? layout)
    {
        try { return JsonSerializer.Deserialize<LayoutDto>(layout ?? string.Empty, JsonOpts) ?? throw new JsonException(); }
        catch (JsonException) { throw FieldErrors.Of("layout", "Sơ đồ gửi lên không đọc được. Bạn tải lại trang rồi thử lại."); }
    }

    private static List<LayoutCell> CellsOf(LayoutDto dto) => dto.Cells.Select(c => new LayoutCell(c.R, c.C, c.T, c.H, c.M)).ToList();

    /// <summary>Xem trước (Tự đánh số, heatmap, Gợi ý ghế nội bộ, thống kê) cho sơ đồ chưa lưu.</summary>
    [HttpPost("{id:int}/preview")]
    public IActionResult Preview(int id, string? layout)
    {
        var dto = ParseLayout(layout);
        var p = _service.Preview(id, dto.Rows, dto.Cols, CellsOf(dto));
        return ApiOk(new
        {
            errors = p.Errors,
            seats = p.Seats.Select(s => new { r = s.Row, c = s.Col, label = s.Label, score = s.Score, heat = s.HeatLevel, pair = s.PairIndex }),
            suggestedHouse = p.SuggestedHouse.Select(x => new { r = x.Row, c = x.Col }),
            stats = new { total = p.Stats.Total, byType = p.Stats.ByType, house = p.Stats.House, maintenance = p.Stats.Maintenance, sellable = p.Stats.Sellable },
        });
    }

    /// <summary>Lưu sơ đồ: tính lại nhãn, cặp ghế đôi, BaseViewScore. Phòng đã bán vé suất tương lai thì chỉ lưu ghế bảo trì.</summary>
    [HttpPost("{id:int}/layout")]
    public IActionResult SaveLayout(int id, string? layout)
    {
        var dto = ParseLayout(layout);
        bool structural = _service.SaveLayout(id, dto.Rows, dto.Cols, CellsOf(dto), _clock.Now);
        return ApiOk(new { id, structural, message = structural ? "Đã lưu sơ đồ và tính lại điểm góc nhìn của từng ghế." : "Đã lưu ghế bảo trì." });
    }

    [HttpGet("{id:int}/delete-check")]
    public IActionResult DeleteCheck(int id)
    {
        var d = _service.CheckDelete(id);
        return ApiOk(new { allowed = d.Allowed, reason = d.Reason, canDeactivate = d.CanDeactivate, consequences = d.Consequences });
    }

    /// <summary>Xóa phòng chỉ khi chưa có suất chiếu nào (SPEC 3.14).</summary>
    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        _service.Delete(id);
        return ApiOk(new { id });
    }

    [HttpPost("{id:int}/set-active")]
    public IActionResult SetActive(int id, bool active)
    {
        _service.SetActive(id, active);
        return ApiOk(new { id, active });
    }
}
