using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FPolyCinema.Web.Controllers;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Infrastructure.Web;
using FPolyCinema.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho logic thuần của Phase 2: OTP, mật khẩu, quy tắc xóa, phân trang, định tuyến theo vai trò.</summary>
public class Phase2RulesTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 10, 0, 0);

    // ---------------------------------------------------------------- OTP (SPEC 2.5)
    [Fact]
    public void Otp_GenerateCode_HasSixDigits()
    {
        for (int i = 0; i < 50; i++)
        {
            var c = OtpRules.GenerateCode(6);
            Assert.Equal(6, c.Length);
            Assert.True(OtpRules.IsWellFormed(c, 6));
        }
        Assert.False(OtpRules.IsWellFormed("12345", 6));
        Assert.False(OtpRules.IsWellFormed("12a456", 6));
        Assert.False(OtpRules.IsWellFormed(null, 6));
    }

    [Fact]
    public void Otp_Check_ValidWrongExpiredUsedAndAttempts()
    {
        var exp = T0.AddMinutes(5);
        Assert.Equal(OtpCheck.Valid, OtpRules.Check(T0, exp, null, 0, 5, codeMatches: true));
        Assert.Equal(OtpCheck.Wrong, OtpRules.Check(T0, exp, null, 0, 5, codeMatches: false));
        Assert.Equal(OtpCheck.Wrong, OtpRules.Check(T0, exp, null, 3, 5, codeMatches: false));
        Assert.Equal(OtpCheck.WrongNoAttemptsLeft, OtpRules.Check(T0, exp, null, 4, 5, codeMatches: false));
        // Hết hạn 5 phút, đã dùng, đã sai đủ 5 lần thì mã không dùng được nữa (OTP_EXPIRED), kể cả khi nhập đúng
        Assert.Equal(OtpCheck.Expired, OtpRules.Check(exp, exp, null, 0, 5, codeMatches: true));
        Assert.Equal(OtpCheck.Expired, OtpRules.Check(T0, exp, T0, 0, 5, codeMatches: true));
        Assert.Equal(OtpCheck.Expired, OtpRules.Check(T0, exp, null, 5, 5, codeMatches: true));
        Assert.Equal(3, OtpRules.RemainingAttempts(2, 5));
        Assert.Equal(0, OtpRules.RemainingAttempts(7, 5));
    }

    [Fact]
    public void Otp_ResendOnlyAfterSixtySeconds()
    {
        Assert.True(OtpRules.CanResend(T0, null));
        Assert.False(OtpRules.CanResend(T0.AddSeconds(59), T0));
        Assert.True(OtpRules.CanResend(T0.AddSeconds(60), T0));
        Assert.Equal(45, OtpRules.SecondsUntilResend(T0.AddSeconds(15), T0));
        Assert.Equal(T0, OtpRules.CreatedAt(T0.AddMinutes(5), 5));
    }

    // ---------------------------------------------------------------- mật khẩu
    [Fact]
    public void Password_Validate_LengthLettersDigitsConfirmAndNotSame()
    {
        Assert.Null(PasswordRules.Validate("Rapphim2026", "Rapphim2026"));
        Assert.NotNull(PasswordRules.Validate("", ""));
        Assert.Contains("8 ký tự", PasswordRules.Validate("abc123", "abc123"));
        Assert.Contains("chữ và số", PasswordRules.Validate("abcdefgh", "abcdefgh"));
        Assert.Contains("chưa khớp", PasswordRules.Validate("Rapphim2026", "Rapphim2027"));
        Assert.Contains("khác mật khẩu hiện tại", PasswordRules.Validate("Rapphim2026", "Rapphim2026", "Rapphim2026"));
    }

    [Fact]
    public void Password_Generate_TenCharsWithLowerUpperDigit()
    {
        for (int i = 0; i < 50; i++)
        {
            var p = PasswordRules.Generate(10);
            Assert.Equal(10, p.Length);
            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsDigit);
            Assert.Null(PasswordRules.Validate(p, p));
        }
    }

    [Fact]
    public void Password_Strength_ZeroToFour()
    {
        Assert.Equal(0, PasswordRules.Strength(""));
        Assert.Equal(1, PasswordRules.Strength("abc123"));
        Assert.Equal(2, PasswordRules.Strength("abcd1234"));
        Assert.Equal(3, PasswordRules.Strength("Abcd1234"));
        Assert.Equal(4, PasswordRules.Strength("Rap-Phim-2026!"));
    }

    // ---------------------------------------------------------------- quy tắc xóa (SPEC 3.14)
    [Fact]
    public void DeleteRules_Category_OnlyWhenNoDrinks()
    {
        var blocked = DeleteRules.Category(drinkCount: 3, isActive: true);
        Assert.False(blocked.Allowed);
        Assert.True(blocked.CanDeactivate);
        Assert.Contains("3 món", blocked.Reason);
        var ok = DeleteRules.Category(0, true);
        Assert.True(ok.Allowed);
        Assert.NotEmpty(ok.Consequences);
    }

    [Fact]
    public void DeleteRules_Drink_OnlyWhenNotInAnyBill()
    {
        var blocked = DeleteRules.Drink(billLineCount: 12, recipeCount: 3, isActive: true);
        Assert.False(blocked.Allowed);
        Assert.True(blocked.CanDeactivate);
        Assert.Contains("ngưng bán", blocked.Reason);
        Assert.False(DeleteRules.Drink(1, 0, isActive: false).CanDeactivate);
        var ok = DeleteRules.Drink(0, 3, true);
        Assert.True(ok.Allowed);
        Assert.Contains(ok.Consequences, c => c.Contains("3 dòng định mức"));
    }

    [Fact]
    public void DeleteRules_Staff_OnlyWhenNoBillsAndNeverSelf()
    {
        var self = DeleteRules.Staff(0, 0, isSelf: true, isActive: true);
        Assert.False(self.Allowed);
        Assert.Contains("mình đang đăng nhập", self.Reason);
        var withBills = DeleteRules.Staff(billsCreated: 5, otherReferences: 0, isSelf: false, isActive: true);
        Assert.False(withBills.Allowed);
        Assert.True(withBills.CanDeactivate);
        Assert.Contains("khóa tài khoản", withBills.Reason);
        Assert.False(DeleteRules.Staff(0, 2, false, true).Allowed);
        Assert.True(DeleteRules.Staff(0, 0, false, true).Allowed);
        Assert.NotNull(DeleteRules.CanSetStaffActive(7, 7, active: false));
        Assert.Null(DeleteRules.CanSetStaffActive(7, 7, active: true));
        Assert.Null(DeleteRules.CanSetStaffActive(1, 7, active: false));
    }

    // ---------------------------------------------------------------- phân trang (UI U5-21)
    [Fact]
    public void Paging_LabelOffsetAndWindow()
    {
        Assert.Equal("1–20 trên 135", Paging.Label(1, 20, 135));
        Assert.Equal("121–135 trên 135", Paging.Label(7, 20, 135));
        Assert.Equal("1.001–1.020 trên 2.500", Paging.Label(51, 20, 2500));
        Assert.Equal("0 kết quả", Paging.Label(1, 20, 0));
        Assert.Equal(40, Paging.Offset(3, 20));
        Assert.Equal(7, Paging.PageCount(135, 20));
        Assert.Equal(1, Paging.ClampPage(-3));
        Assert.Equal(new[] { 1, 0, 4, 5, 6, 7, 8, 0, 20 }, Paging.Window(6, 20));
        Assert.Equal(new[] { 1, 2, 3 }, Paging.Window(2, 3));
    }

    // ---------------------------------------------------------------- trang chính và khu vực theo vai trò
    [Fact]
    public void RoleRouting_HomeAreaLayoutAndReturn()
    {
        Assert.Equal("/", RoleRouting.HomePath(Roles.Customer));
        Assert.Equal("/", RoleRouting.HomePath(null));
        Assert.Equal("/admin/drinks", RoleRouting.HomePath(Roles.Manager));
        Assert.Equal("/me", RoleRouting.HomePath(Roles.POSCashier));
        Assert.Equal(UiArea.Admin, RoleRouting.AreaOfPath("/admin/users", Roles.POSCashier));
        Assert.Equal(UiArea.Pos, RoleRouting.AreaOfPath("/pos/index", Roles.Manager));
        Assert.Equal(UiArea.Customer, RoleRouting.AreaOfPath("/admin/users", Roles.Customer));
        Assert.Equal(UiArea.Pos, RoleRouting.AreaOfPath("/me", Roles.TicketInspector));
        Assert.Equal("_LayoutAdmin", RoleRouting.LayoutOf(UiArea.Admin));
        Assert.Equal("_LayoutPos", RoleRouting.LayoutOf(UiArea.Pos));
        Assert.Equal("_LayoutCustomer", RoleRouting.LayoutOf(UiArea.Customer));
        Assert.False(RoleRouting.CanReturnTo("/admin/drinks", Roles.Customer));
        Assert.False(RoleRouting.CanReturnTo("/Login/Login", Roles.Manager));
        Assert.True(RoleRouting.CanReturnTo("/Login/ChangePassword", Roles.Customer));
        Assert.False(RoleRouting.CanReturnTo("/admin/users", Roles.ShiftManager));
        Assert.True(RoleRouting.CanReturnTo("/admin/drinks", Roles.ShiftManager));
    }

    // ---------------------------------------------------------------- định dạng (UI mục 9) và nhãn trạng thái (UI mục 6)
    [Fact]
    public void Fmt_VietnameseFormats()
    {
        Assert.Equal("125.000 ₫", Fmt.Money(125_000));
        Assert.Equal("-20.000 ₫", Fmt.Money(-20_000));
        Assert.Equal("06/11/2026", Fmt.Date(new DateTime(2026, 11, 6, 19, 30, 0)));
        Assert.Equal("19:30", Fmt.Time(new DateTime(2026, 11, 6, 19, 30, 0)));
        Assert.Equal("06/11/2026 19:30", Fmt.DateTime(new DateTime(2026, 11, 6, 19, 30, 0)));
        Assert.Equal("T7, 07/11", Fmt.WeekdayDate(new DateTime(2026, 11, 7)));
        Assert.Equal("CN, 08/11", Fmt.WeekdayDate(new DateTime(2026, 11, 8)));
        Assert.Equal("T2, 09/11", Fmt.WeekdayDate(new DateTime(2026, 11, 9)));
        Assert.Equal("2 giờ 05 phút", Fmt.Duration(125));
        Assert.Equal("2 giờ", Fmt.Duration(120));
        Assert.Equal("45 phút", Fmt.Duration(45));
        Assert.Equal("0,15", Fmt.Decimal(0.150m));
        Assert.Equal("TQ", Fmt.Initials("Trần Minh Quân"));
    }

    [Fact]
    public void StatusDisplay_CoversEveryEnumValueWithVietnameseLabels()
    {
        void Covers(string group, IEnumerable<string> values)
        {
            foreach (var v in values)
            {
                var s = StatusDisplay.Of(group, v);
                Assert.NotEqual("Không xác định", s.Label);
            }
        }
        Covers(StatusDisplay.Bill, new[] { BillStatus.Waiting, BillStatus.Finish, BillStatus.Cancel });
        Covers(StatusDisplay.Ticket, new[] { TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.CheckedIn, TicketCheckInStatus.Swapped, TicketCheckInStatus.Voided });
        Covers(StatusDisplay.Showtime, new[] { OperationalStatus.Scheduled, OperationalStatus.Boarding, OperationalStatus.AdsPlaying, OperationalStatus.FeaturePlaying, OperationalStatus.CreditsRolling, OperationalStatus.CleaningInProgress, OperationalStatus.Completed, OperationalStatus.Cancelled });
        Covers(StatusDisplay.Role, Roles.All);
        Covers(StatusDisplay.Age, new[] { AgeRating.P, AgeRating.K, AgeRating.T13, AgeRating.T16, AgeRating.T18, AgeRating.C });
        Assert.Equal(("Chờ thanh toán", "warning"), (StatusDisplay.Of(StatusDisplay.Bill, "waiting").Label, StatusDisplay.Of(StatusDisplay.Bill, "waiting").Tone));
        Assert.Equal("Không xác định", StatusDisplay.Of(StatusDisplay.Bill, "BOGUS").Label);
        Assert.Equal("Đang hoạt động", StatusDisplay.Of(StatusDisplay.Active, true).Label);
        // Vai trò hiển thị đúng cột Hiển thị của CONTRACTS mục 1
        Assert.Equal("Quản lý rạp (quản trị)", StatusDisplay.RoleLabel(Roles.Manager));
        Assert.Equal("Thu ngân và quầy bắp nước", StatusDisplay.RoleLabel(Roles.POSCashier));
        Assert.Equal("Soát vé và điều phối phòng chiếu", StatusDisplay.RoleLabel(Roles.TicketInspector));
        Assert.Equal("Quản lý ca", StatusDisplay.RoleLabel(Roles.ShiftManager));
        Assert.Equal("Khách hàng", StatusDisplay.RoleLabel(Roles.Customer));
        Assert.Equal("T16: Phim dành cho khán giả từ đủ 16 tuổi trở lên", StatusDisplay.AgeExplanation(AgeRating.T16));
    }

    // ---------------------------------------------------------------- phản hồi AJAX và mã lỗi (CONTRACTS mục 8)
    [Fact]
    public void ApiResponse_ShapeAndHttpStatusOfCodes()
    {
        var relaxed = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All) };
        var ok = JsonSerializer.Serialize(ApiResponse.Ok(new { id = 3 }), relaxed);
        Assert.Equal("{\"ok\":true,\"data\":{\"id\":3}}", ok);
        var fail = JsonSerializer.Serialize(ApiResponse.Fail(ErrorCodes.DeleteBlocked, "Không xóa được.", new { reason = "x" }), relaxed);
        Assert.Equal("{\"ok\":false,\"error\":{\"code\":\"DELETE_BLOCKED\",\"message\":\"Không xóa được.\",\"details\":{\"reason\":\"x\"}}}", fail);
        Assert.Equal(400, ErrorCodes.HttpStatusOf(ErrorCodes.ValidationFailed));
        Assert.Equal(401, ErrorCodes.HttpStatusOf(ErrorCodes.AuthRequired));
        Assert.Equal(403, ErrorCodes.HttpStatusOf(ErrorCodes.AccessDenied));
        Assert.Equal(403, ErrorCodes.HttpStatusOf(ErrorCodes.MustChangePassword));
        Assert.Equal(404, ErrorCodes.HttpStatusOf(ErrorCodes.NotFound));
        Assert.Equal(409, ErrorCodes.HttpStatusOf(ErrorCodes.InvalidTransition));
        Assert.Equal(422, ErrorCodes.HttpStatusOf(ErrorCodes.DeleteBlocked));
        Assert.Equal(422, ErrorCodes.HttpStatusOf(ErrorCodes.OtpInvalid));
        Assert.Equal(422, ErrorCodes.HttpStatusOf(ErrorCodes.OtpExpired));
        Assert.Equal(500, ErrorCodes.HttpStatusOf(ErrorCodes.ServerError));
        var (status, code, message, _) = ApiErrorMiddleware.Describe(new InvalidOperationException("chi tiết kỹ thuật"), "trace-1");
        Assert.Equal((500, ErrorCodes.ServerError), (status, code));
        Assert.DoesNotContain("kỹ thuật", message);
    }

    [Fact]
    public void DalText_LikeContains_EscapesWildcards()
    {
        Assert.Null(DalText.LikeContains("  "));
        Assert.Equal("%bắp%", DalText.LikeContains(" bắp "));
        Assert.Equal("%50!%!_![a!!%", DalText.LikeContains("50%_[a!"));
    }

    // ---------------------------------------------------------------- claims (SPEC 2.5)
    [Fact]
    public void Auth_BuildPrincipal_ClaimsAndCinemaIdOnlyForStaff()
    {
        var staff = new User { Id = 4, FullName = "Đỗ Văn Hùng", Email = "cashier@fpolycinema.vn", Role = Roles.POSCashier, CinemaId = 1, PasswordHash = "abc", MustChangePassword = true };
        var p = AuthService.BuildPrincipal(staff);
        Assert.Equal("4", p.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Đỗ Văn Hùng", p.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("cashier@fpolycinema.vn", p.FindFirstValue(ClaimTypes.Email));
        Assert.Equal(Roles.POSCashier, p.FindFirstValue(ClaimTypes.Role));
        Assert.Equal("1", p.FindFirstValue(AppClaims.CinemaId));
        Assert.Equal("1", p.FindFirstValue(AppClaims.MustChangePassword));
        var customer = new User { Id = 9, FullName = "Khách", Email = "k@x.vn", Role = Roles.Customer, CinemaId = 1, PasswordHash = "abc" };
        Assert.Null(AuthService.BuildPrincipal(customer).FindFirstValue(AppClaims.CinemaId));
        // Đổi mật khẩu thì dấu đổi, phiên cũ hết hiệu lực; tài khoản bị khóa cũng hết hiệu lực
        Assert.True(AuthService.IsSessionValid(staff, AuthService.PasswordStamp("abc")));
        Assert.False(AuthService.IsSessionValid(staff, AuthService.PasswordStamp("khac")));
        staff.Active = false;
        Assert.False(AuthService.IsSessionValid(staff, AuthService.PasswordStamp("abc")));
    }

    [Fact]
    public void Program_UseForwardedHeadersIsFirstMiddleware()
    {
        var code = File.ReadAllText(RepoPaths.File("Program.cs"));
        var uses = System.Text.RegularExpressions.Regex.Matches(code, @"\bapp\.Use[A-Za-z]+\(").Select(m => m.Value).ToList();
        Assert.Equal("app.UseForwardedHeaders(", uses[0]);
        Assert.True(uses.IndexOf("app.UseAuthentication(") < uses.IndexOf("app.UseAuthorization("));
    }

    // ---------------------------------------------------------------- controller: phân quyền rõ ràng, route attribute, policy đúng chỗ
    private static IEnumerable<Type> Controllers() =>
        typeof(HomeController).Assembly.GetTypes().Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract);

    [Fact]
    public void Controllers_EveryActionHasAuthorizeOrAllowAnonymous()
    {
        foreach (var c in Controllers())
        {
            bool classLevel = c.GetCustomAttributes<AuthorizeAttribute>(true).Any() || c.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null))
            {
                bool actionLevel = m.GetCustomAttributes<AuthorizeAttribute>().Any() || m.GetCustomAttributes<AllowAnonymousAttribute>().Any();
                Assert.True(classLevel || actionLevel, $"{c.Name}.{m.Name} chưa có [Authorize] hoặc [AllowAnonymous]");
            }
        }
    }

    [Fact]
    public void Controllers_PoliciesMatchContracts()
    {
        string? Policy(MemberInfo m) => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p != null);
        Assert.Equal(Policies.ManagerOnly, Policy(typeof(FileController)));
        Assert.Equal(Policies.ManagerOnly, Policy(typeof(UsersController)));
        Assert.Equal(Policies.EmployeeOrManager, Policy(typeof(CategoriesController)));
        Assert.Equal(Policies.EmployeeOrManager, Policy(typeof(DrinksController)));
        foreach (var t in new[] { typeof(CategoriesController), typeof(DrinksController) })
            foreach (var name in new[] { "Create", "Edit", "Delete", "DeleteCheck", "SetActive" })
                Assert.All(t.GetMethods().Where(m => m.Name == name), m => Assert.Equal(Policies.ManagerOnly, Policy(m)));
        foreach (var name in new[] { "Upload", "Delete" })
        {
            var m = typeof(FileController).GetMethod(name)!;
            Assert.NotNull(m.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }
}
