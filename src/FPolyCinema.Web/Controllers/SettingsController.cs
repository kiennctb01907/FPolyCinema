using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>Cấu hình rạp (UI A10, ManagerOnly): xem hệ thống rạp và tham số nghiệp vụ CinemaRules đang áp dụng; chỉ đọc, không có thao tác sửa.</summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("admin/settings")]
public class SettingsController : AppControllerBase
{
    private readonly SettingsService _settings;

    public SettingsController(SettingsService settings) => _settings = settings;

    [HttpGet("")]
    public IActionResult Index() => View(_settings.Build());
}
