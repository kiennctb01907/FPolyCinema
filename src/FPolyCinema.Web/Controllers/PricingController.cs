using System.Globalization;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Bảng giá 6 chiều theo rạp, đối tượng ưu đãi (chặn mức dưới tối thiểu theo luật), ngày lễ và trang thử tính giá
/// (UC33, SPEC 3.1 mục 5, 3.4, 8.3). Sửa bảng giá ghi AuditLogs. Chỉ quản lý rạp (cấu hình).
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("admin/pricing")]
public class PricingController : AppControllerBase
{
    private readonly PricingService _service;
    private readonly CinemaContextService _cinemas;
    private readonly IClock _clock;

    public PricingController(PricingService service, CinemaContextService cinemas, IClock clock)
    {
        _service = service;
        _cinemas = cinemas;
        _clock = clock;
    }

    private int PickCinema(int? cinemaId) =>
        cinemaId is int c && _cinemas.Cinemas.Any(x => x.Id == c) ? c : _cinemas.CurrentCinemaId ?? _cinemas.Cinemas.FirstOrDefault()?.Id ?? 0;

    [HttpGet("")]
    public IActionResult Index(int? cinemaId)
    {
        var id = PickCinema(cinemaId);
        return View(new PricingVm { Cinemas = _cinemas.Cinemas.ToList(), CinemaId = id, Cells = _service.Table(id) });
    }

    /// <summary>Lưu bảng giá: các ô tên Amounts[RuleType.RuleKey].</summary>
    [HttpPost("")]
    public IActionResult Save(int cinemaId, Dictionary<string, string?> amounts)
    {
        var id = PickCinema(cinemaId);
        var cells = new List<PriceCell>();
        var errors = new Dictionary<string, string>();
        foreach (var c in _service.Table(id))
        {
            var key = $"{c.RuleType}.{c.RuleKey}";
            var raw = amounts.TryGetValue(key, out var v) ? (v ?? string.Empty).Replace(".", string.Empty).Replace(" ", string.Empty).Trim() : null;
            if (raw is null) { cells.Add(c); continue; }
            if (raw.Length == 0) raw = "0";
            if (!long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)) { errors[key] = "Bạn nhập số tiền bằng chữ số, có thể có dấu trừ."; cells.Add(c); continue; }
            cells.Add(c with { Amount = amount });
        }
        try
        {
            if (errors.Count > 0) throw FieldErrors.Of(errors);
            int changed = _service.SaveTable(id, cells, CurrentUserId == 0 ? null : CurrentUserId, _clock.Now);
            Toast("success", changed == 0 ? "Bảng giá không có thay đổi nào." : $"Đã lưu {changed} dòng giá và ghi vào nhật ký.");
            return Redirect($"/admin/pricing?cinemaId={id}");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return View("Index", new PricingVm { Cinemas = _cinemas.Cinemas.ToList(), CinemaId = id, Cells = cells });
        }
    }

    [HttpGet("audiences")]
    public IActionResult Audiences() => View(new AudienceDiscountsVm { Discounts = _service.Discounts() });

    /// <summary>Lưu mức giảm theo đối tượng: không cho lưu mức thấp hơn mức tối thiểu theo luật (SPEC 3.1 mục 5).</summary>
    [HttpPost("audiences")]
    public IActionResult Audiences(Dictionary<string, decimal> discounts)
    {
        try
        {
            int changed = _service.SaveDiscounts(discounts, CurrentUserId == 0 ? null : CurrentUserId, _clock.Now);
            Toast("success", changed == 0 ? "Mức giảm không có thay đổi nào." : $"Đã lưu {changed} mức giảm và ghi vào nhật ký.");
            return Redirect("/admin/pricing/audiences");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            return View(new AudienceDiscountsVm { Discounts = _service.Discounts(), Posted = discounts });
        }
    }

    [HttpGet("holidays")]
    public IActionResult Holidays(int? year)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : _clock.Today.Year;
        return View(HolidaysVm(y));
    }

    private HolidaysVm HolidaysVm(int year)
    {
        var years = _service.HolidayYears().Union(new[] { year, _clock.Today.Year, _clock.Today.Year + 1 }).Distinct().OrderBy(x => x).ToList();
        return new HolidaysVm { Year = year, Years = years, Holidays = _service.Holidays(year) };
    }

    [HttpPost("holidays")]
    public IActionResult AddHoliday(DateTime? holidayDate, string? name)
    {
        try
        {
            var h = _service.AddHoliday(holidayDate, name);
            Toast("success", $"Đã thêm ngày lễ {Fmt.Date(h.HolidayDate)}. Giá vé ngày này tính theo HOLIDAY.");
            return Redirect($"/admin/pricing/holidays?year={h.HolidayDate.Year}");
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed)
        {
            AddErrors(ex);
            var vm = HolidaysVm(holidayDate?.Year ?? _clock.Today.Year);
            vm.NewDate = holidayDate;
            vm.NewName = name;
            return View("Holidays", vm);
        }
    }

    /// <summary>Xóa một ngày lễ (AJAX); ngày đó tính lại theo thứ trong tuần.</summary>
    [HttpPost("holidays/{date}/delete")]
    public IActionResult DeleteHoliday(string date)
    {
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return ApiFail(ErrorCodes.ValidationFailed, "Ngày không hợp lệ. Bạn tải lại trang rồi thử lại.");
        _service.DeleteHoliday(d);
        return ApiOk(new { date });
    }

    /// <summary>Trang thử tính giá: chọn rạp, giờ chiếu, định dạng, loại ghế, phim bom tấn, đối tượng.</summary>
    [HttpGet("try")]
    public IActionResult Try(int? cinemaId, DateTime? startTime, string? format, string? seatType, bool blockbuster = false, string? audienceType = null)
    {
        var vm = new PriceTryVm
        {
            Cinemas = _cinemas.Cinemas.ToList(), CinemaId = PickCinema(cinemaId),
            StartTime = startTime ?? _clock.Today.AddHours(19),
            Format = AuditoriumFormat.All.Contains(format) ? format! : AuditoriumFormat.F2D,
            SeatType = SeatType.All.Contains(seatType) ? seatType! : SeatType.Standard,
            IsBlockbuster = blockbuster,
            AudienceType = AudienceType.All.Contains(audienceType) ? audienceType! : AudienceType.Adult,
            Discounts = _service.Discounts(),
        };
        if (startTime != null)
        {
            try { vm.Result = _service.Try(new PriceTryInput(vm.CinemaId, vm.StartTime, vm.Format, vm.SeatType, vm.IsBlockbuster, vm.AudienceType)); }
            catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.ValidationFailed) { AddErrors(ex); }
        }
        return View(vm);
    }
}
