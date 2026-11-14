using System.Globalization;
using FPolyCinema.Web.DAL;
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
/// Bếp và quầy bắp nước (SPEC 3.6-19, 3.11; UI P4; PosStaff): màn hình bếp hai cột; quét QR hóa đơn online để nhận bắp nước (bắt đầu làm món);
/// "Sẵn sàng giao", "Đã giao"; lập phiếu hủy hàng hỏng (UC20).
/// </summary>
[Authorize(Policy = Policies.PosStaff)]
[Route("pos")]
public class KitchenController : AppControllerBase
{
    private readonly KdsService _kds;
    private readonly InventoryService _inventory;
    private readonly CinemaContextService _cinema;
    private readonly IClock _clock;

    public KitchenController(KdsService kds, InventoryService inventory, CinemaContextService cinema, IClock clock)
    {
        _kds = kds;
        _inventory = inventory;
        _cinema = cinema;
        _clock = clock;
    }

    private int CinemaId => _cinema.CurrentCinemaId ?? _cinema.Cinemas.First().Id;

    private string CinemaName => _cinema.Cinemas.FirstOrDefault(c => c.Id == CinemaId)?.Name ?? string.Empty;

    /// <summary>Màn hình bếp (UI P4): "Đang chuẩn bị" và "Sẵn sàng giao".</summary>
    [HttpGet("fnb-kds")]
    public IActionResult Kds() => View(new KdsVm { CinemaId = CinemaId, CinemaName = CinemaName, Orders = KdsService.Orders(CinemaId), Now = _clock.Now });

    /// <summary>Danh sách đơn (tải lại sau khi kết nối lại).</summary>
    [HttpGet("fnb-kds/orders")]
    public IActionResult Orders() => ApiOk(new { orders = KdsService.Orders(CinemaId), now = _clock.Now.ToString("yyyy-MM-ddTHH:mm:ss") });

    /// <summary>"Sẵn sàng giao": số gọi lên bảng, khách nhận FNB_STATUS_CHANGED.</summary>
    [HttpPost("fnb-kds/{billId:int}/ready")]
    public async Task<IActionResult> Ready(int billId) => ApiOk(await _kds.Advance(billId, FnbStatus.ReadyForPickup, CinemaId));

    /// <summary>"Đã giao": đơn hoàn tất.</summary>
    [HttpPost("fnb-kds/{billId:int}/served")]
    public async Task<IActionResult> Served(int billId) => ApiOk(await _kds.Advance(billId, FnbStatus.Fulfilled, CinemaId));

    /// <summary>Quét QR hóa đơn online (mã hóa đơn hoặc mã vé) tại quầy bắp nước để nhận bắp nước: bắt đầu làm món (SPEC 3.6-19).</summary>
    [HttpPost("fnb-kds/pickup")]
    public async Task<IActionResult> Pickup(string? code)
    {
        var (bill, _) = GateService.Find(code);
        if (bill == null || bill.CinemaId != CinemaId) throw FieldErrors.Of("code", "Không tìm thấy hóa đơn của rạp này với mã vừa quét.");
        var order = await _kds.StartOnline(bill.Id, null, CinemaId, CurrentUserId);
        return ApiOk(new { order, message = $"Đã nhận đơn {order.Code}, số gọi {order.PickupNumber}. Bếp bắt đầu làm món." });
    }

    // ------------------------------------------------------------------ phiếu hủy hàng hỏng (UC20)

    /// <summary>Lập phiếu hủy hàng hỏng (PosStaff): chọn nguyên liệu, số lượng, lý do; phiếu chờ quản lý ca duyệt.</summary>
    [HttpGet("wastage")]
    public IActionResult Wastage() => View(WastageForm(null));

    private StockFormVm WastageForm(string? note) => new()
    {
        CinemaId = CinemaId, CinemaName = CinemaName, Materials = RawMaterialDAL.ByCinema(CinemaId).OrderBy(m => m.Name).ToList(),
        Recent = WastageLogDAL.Search(CinemaId, null, 10), Note = note,
    };

    [HttpPost("wastage")]
    public IActionResult Wastage([FromForm] int[]? materialIds, [FromForm] string[]? quantities, [FromForm] string[]? reasons, string? note)
    {
        var lines = StockLines(materialIds, quantities, reasons);
        int id = _inventory.CreateWastage(CinemaId, lines, note, CurrentUserId, _clock.Now);
        return ApiOk(new { id, message = $"Đã lập phiếu hủy hàng #{id}. Phiếu chờ quản lý ca duyệt." });
    }

    /// <summary>Ghép các ô của form thành dòng phiếu (số lượng nhập kiểu Việt Nam hoặc dấu chấm).</summary>
    public static List<StockLineInput> StockLines(int[]? materialIds, string[]? quantities, string[]? reasons)
    {
        var list = new List<StockLineInput>();
        for (int i = 0; i < (materialIds?.Length ?? 0); i++)
        {
            var raw = (quantities != null && i < quantities.Length ? quantities[i] : null)?.Trim().Replace(',', '.') ?? string.Empty;
            decimal q = decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : (raw.Length > 0 ? -1 : 0);
            list.Add(new StockLineInput(materialIds![i], q, reasons != null && i < reasons.Length ? reasons[i] : null));
        }
        return list;
    }
}
