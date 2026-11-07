using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
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
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CinemaRules>>().Value);
builder.Services.AddSingleton<IClock>(sp => new SystemClock(sp.GetRequiredService<CinemaRules>()));
builder.Services.AddScoped<BillStatusService>();
builder.Services.AddControllersWithViews();

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
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
return 0;
