using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using FPolyCinema.Web.Controllers;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Infrastructure.Web;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.WebEncoders;
using Microsoft.Extensions.Options;

// Tham số riêng của ứng dụng (SPEC 2.4), tách ra trước khi đưa phần còn lại cho cấu hình dòng lệnh của ASP.NET Core
const string ResetDbArg = "--reset-db";
const string InitOnlyArg = "--init-only";
bool resetDb = args.Contains(ResetDbArg);
bool initOnly = args.Contains(InitOnlyArg);
var appArgs = args.Where(a => a != ResetDbArg && a != InitOnlyArg).ToArray();

var builder = WebApplication.CreateBuilder(appArgs);

// Mọi cấu hình đọc được từ biến môi trường, ví dụ ConnectionStrings__Default, CinemaRules__SeatHoldMinutes (SPEC 2.7)
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<CinemaRules>(builder.Configuration.GetSection(CinemaRules.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.SectionName));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CinemaRules>>().Value);
builder.Services.AddSingleton<IClock>(sp => new FPolyCinema.Web.Infrastructure.SystemClock(sp.GetRequiredService<CinemaRules>()));
builder.Services.AddHttpContextAccessor();

// Gửi email (SPEC 2.5): có Email:SmtpHost thì SMTP thật, không có thì ghi vào EmailOutbox (/dev/mailbox)
builder.Services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value.UseSmtp
    ? new SmtpEmailSender(sp.GetRequiredService<IOptions<EmailOptions>>(), sp.GetRequiredService<IClock>())
    : new OutboxEmailSender(sp.GetRequiredService<IClock>()));
builder.Services.AddScoped<BillStatusService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<UploadService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<DrinkService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<MovieService>();
builder.Services.AddScoped<DistributorService>();
builder.Services.AddScoped<AuditoriumService>();
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<CinemaContextService>();
builder.Services.AddScoped<ShellService>();
builder.Services.AddScoped<DevToolsService>();
// Phase 4: bán hàng tại quầy, hóa đơn, thống kê, thời gian thực
builder.Services.AddScoped<RealtimeNotifier>();
builder.Services.AddScoped<SeatMapService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<LoyaltyService>();
builder.Services.AddScoped<BillService>();
builder.Services.AddScoped<SeatHoldService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<ManagerPinService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<BillQueryService>();
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));
builder.Services.AddScoped<SeatSuggestService>();
builder.Services.AddScoped<OnlineBookingService>();
builder.Services.AddScoped<CustomerCatalogPages>();
builder.Services.AddScoped<ShowtimeOpsService>();
builder.Services.AddScoped<GateService>();
builder.Services.AddScoped<KdsService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<OpsManagementService>();
builder.Services.AddScoped<SeatSwapService>();
builder.Services.AddScoped<CustomerDisplayService>();
builder.Services.AddScoped<FPolyCinema.Web.Controllers.OpsDashboardPages>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<FPolyCinema.Web.Background.SeatHoldSweeper>();
builder.Services.AddHostedService<FPolyCinema.Web.Background.ShowtimeClockService>();

// ---------------------------------------------------------------- xác thực và phân quyền (SPEC 2.5, slide bài 4)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = false;
        options.LoginPath = "/Login/Login";
        options.LogoutPath = "/Login/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.Cookie.Name = "fpc_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events = new CookieAuthenticationEvents
        {
            // Phiên hết hiệu lực khi tài khoản bị khóa hoặc mật khẩu đã đổi ở nơi khác (UI A9: đổi mật khẩu thì đăng xuất các phiên khác)
            OnValidatePrincipal = async ctx =>
            {
                if (!AuthService.ValidateSession(ctx.Principal))
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            },
            // AJAX nhận JSON theo CONTRACTS mục 8 thay vì bị chuyển trang
            OnRedirectToLogin = ctx =>
            {
                if (RequestKinds.WantsJson(ctx.Request))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return ctx.Response.WriteAsJsonAsync(ApiResponse.Fail(ErrorCodes.AuthRequired, ApiResponse.DefaultMessage(ErrorCodes.AuthRequired)));
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                if (RequestKinds.WantsJson(ctx.Request))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return ctx.Response.WriteAsJsonAsync(ApiResponse.Fail(ErrorCodes.AccessDenied, ApiResponse.DefaultMessage(ErrorCodes.AccessDenied)));
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            },
        };
    });

// Policy theo CONTRACTS mục 2
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.EmployeeOrManager, p => p.RequireRole(Roles.POSCashier, Roles.TicketInspector, Roles.ShiftManager, Roles.Manager));
    options.AddPolicy(Policies.ManagerOnly, p => p.RequireRole(Roles.Manager));
    options.AddPolicy(Policies.PosStaff, p => p.RequireRole(Roles.POSCashier, Roles.ShiftManager, Roles.Manager));
    options.AddPolicy(Policies.GateStaff, p => p.RequireRole(Roles.TicketInspector, Roles.ShiftManager, Roles.Manager));
    options.AddPolicy(Policies.SupervisorOrManager, p => p.RequireRole(Roles.ShiftManager, Roles.Manager));
    options.AddPolicy(Policies.CustomerOnly, p => p.RequireRole(Roles.Customer));
});

// Anti-Forgery toàn cục; AJAX gửi token qua header RequestVerificationToken (SPEC 2.5)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "fpc_af";
});
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<AntiforgeryResultFilter>();
    // Câu báo lỗi mặc định của model binding bằng tiếng Việt (không để sót chữ tiếng Anh, UI mục 12)
    var m = options.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "Bạn chưa nhập ô này.");
    m.SetMissingBindRequiredValueAccessor(_ => "Bạn chưa nhập ô này.");
    m.SetMissingKeyOrValueAccessor(() => "Bạn chưa nhập ô này.");
    m.SetMissingRequestBodyRequiredValueAccessor(() => "Thiếu dữ liệu gửi lên. Bạn thử lại.");
    m.SetAttemptedValueIsInvalidAccessor((v, _) => $"Giá trị \"{v}\" chưa hợp lệ.");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(v => $"Giá trị \"{v}\" chưa hợp lệ.");
    m.SetUnknownValueIsInvalidAccessor(_ => "Giá trị chưa hợp lệ.");
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Giá trị chưa hợp lệ.");
    m.SetValueIsInvalidAccessor(v => $"Giá trị \"{v}\" chưa hợp lệ.");
    m.SetValueMustBeANumberAccessor(_ => "Bạn nhập một số.");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "Bạn nhập một số.");
}).AddJsonOptions(o => o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));
// Giữ nguyên chữ tiếng Việt trong HTML và JSON (không mã hóa thành thực thể), trang nhẹ và dễ đọc hơn
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

// Chạy sau Nginx trên Linux (bài 8): tin header X-Forwarded-For và X-Forwarded-Proto
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FPolyCinema.Startup");
var clock = app.Services.GetRequiredService<IClock>();
var rules = app.Services.GetRequiredService<CinemaRules>();
FileUtil.Clock = clock;

// ---------------------------------------------------------------- khởi tạo CSDL (SPEC 2.4)
try
{
    var connectionString = app.Configuration.GetConnectionString("Default") ?? string.Empty;
    DBUtil.Init(connectionString);

    if (resetDb && !app.Environment.IsDevelopment())
        throw new InvalidOperationException("Tham số --reset-db chỉ dùng trong môi trường Development.");

    var scriptsDir = Path.Combine(AppContext.BaseDirectory, "Database");
    var initializer = new DbInitializer(connectionString, scriptsDir, clock, logger);
    initializer.Initialize(resetDb);

    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var seeder = new DbSeeder(clock, rules, webRoot, logger);
    if (seeder.SeedIfEmpty()) logger.LogInformation("Đã nạp dữ liệu mẫu vào CSDL {Db}.", initializer.DatabaseName);
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Khởi tạo cơ sở dữ liệu thất bại: {Message}", ex.Message);
    if (initOnly) return 1;
    throw;
}

if (initOnly)
{
    logger.LogInformation("Đã khởi tạo xong cơ sở dữ liệu (--init-only), thoát.");
    return 0;
}

// ---------------------------------------------------------------- pipeline web
// UseForwardedHeaders đặt đầu pipeline (SPEC 2.7)
app.UseForwardedHeaders();
// Xử lý lỗi tập trung (SPEC 2.7): request thường ra trang lỗi thân thiện, AJAX ra JSON (ApiErrorMiddleware); không hiện stack trace
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
app.UseMiddleware<ApiErrorMiddleware>();
// Bind dữ liệu số theo dạng bất biến (ô number luôn gửi dấu chấm thập phân); hiển thị dùng Fmt với vi-VN
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = new[] { CultureInfo.InvariantCulture },
    SupportedUICultures = new[] { CultureInfo.InvariantCulture },
});
app.UseStaticFiles();
// Uploads:Path khác wwwroot/uploads thì phục vụ thêm thư mục đó tại /uploads
var uploadsDir = Path.Combine(app.Services.GetRequiredService<IOptions<UploadOptions>>().Value.ResolveRoot(app.Environment.ContentRootPath), FileUtil.UploadFolder);
if (!string.Equals(Path.GetFullPath(uploadsDir), Path.GetFullPath(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", FileUtil.UploadFolder)), StringComparison.OrdinalIgnoreCase))
{
    Directory.CreateDirectory(uploadsDir);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(uploadsDir), RequestPath = "/" + FileUtil.UploadFolder });
}
app.UseMiddleware<DemoToolsMiddleware>();
app.UseRouting();
// UseAuthentication đặt trước UseAuthorization (SPEC 2.5)
app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<FPolyCinema.Web.Hubs.CinemaHub>(FPolyCinema.Web.Hubs.CinemaHub.Path);
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
return 0;

/// <summary>Khai báo để test tích hợp (WebApplicationFactory) dùng được lớp Program sinh từ top-level statements.</summary>
public partial class Program { }
