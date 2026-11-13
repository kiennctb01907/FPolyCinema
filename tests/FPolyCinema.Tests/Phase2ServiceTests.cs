using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Tests;

/// <summary>Ghi lại thư thay vì gửi (thay IEmailSender trong test).</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public void Send(string toEmail, string subject, string body) => Sent.Add((toEmail, subject, body));
}

/// <summary>Môi trường web giả cho UploadService (thư mục tạm).</summary>
public sealed class FakeWebEnv : IWebHostEnvironment
{
    public FakeWebEnv(string root) { ContentRootPath = root; WebRootPath = Path.Combine(root, "wwwroot"); }
    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "FPolyCinema.Web";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
}

/// <summary>CSDL thử riêng cho test dịch vụ Phase 2 (tên DB + "_SvcTest_" + chuỗi ngẫu nhiên), có sẵn một rạp, nguyên liệu, món đã bán.</summary>
public sealed class ServiceDatabaseFixture : IDisposable
{
    public ServiceDatabaseFixture()
    {
        ConnectionString = TestDatabase.NewConnectionString("SvcTest");
        Clock = new FakeClock(new DateTime(2026, 11, 7, 9, 0, 0));
        Root = Path.Combine(Path.GetTempPath(), "fpc-svc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "wwwroot", "uploads"));
        DBUtil.Init(ConnectionString);
        new DbInitializer(ConnectionString, RepoPaths.File("Database"), Clock, NullLogger.Instance).Initialize(resetDb: true);

        CinemaId = CinemaDAL.Insert(new Cinema { Name = "Rạp thử", Address = "Hà Nội", Active = true });
        Cinema2Id = CinemaDAL.Insert(new Cinema { Name = "Rạp thử 2", Address = "TP. HCM", Active = true });
        MaterialId = RawMaterialDAL.Insert(new RawMaterial { CinemaId = CinemaId, Name = "Bắp hạt", Unit = "kg", ActualStock = 10, MinThreshold = 1, Active = true });
        Material2Id = RawMaterialDAL.Insert(new RawMaterial { CinemaId = Cinema2Id, Name = "Bắp hạt", Unit = "kg", ActualStock = 10, MinThreshold = 1, Active = true });
        SoldCategoryId = CategoryDAL.Insert(new Category { Name = "Bắp rang", Active = true });
        SoldDrinkId = DrinkDAL.Insert(new Drink { CategoryId = SoldCategoryId, Name = "Bắp đã bán", Price = 59_000, Active = true, CreatedAt = Clock.Now });
        CashierId = UserDAL.Insert(NewUser("cashier.test@fpolycinema.vn", Roles.POSCashier, CinemaId));
        ManagerId = UserDAL.Insert(NewUser("manager.test@fpolycinema.vn", Roles.Manager, null));
        var bill = new Bill { Code = "HD20261107090000TEST", Channel = BillChannel.Pos, CinemaId = CinemaId, UserId = CashierId, CreatedAt = Clock.Now, FnbSubtotal = 59_000, Total = 59_000 };
        BillDAL.Insert(bill);
        BillDetailDAL.Insert(new BillDetail { BillId = bill.Id, DrinkId = SoldDrinkId, Quantity = 1, Price = 59_000, VatRate = 8, LineFinalAmount = 59_000 });
    }

    public static User NewUser(string email, string role, int? cinemaId) => new()
    {
        Email = email, PasswordHash = PasswordUtil.Hash("Matkhau123"), FullName = "Người dùng thử", Role = role, CinemaId = cinemaId,
        Active = true, Tier = LoyaltyTier.Member, CreatedAt = new DateTime(2026, 11, 1),
    };

    public string ConnectionString { get; }
    public FakeClock Clock { get; }
    public string Root { get; }
    public int CinemaId { get; }
    public int Cinema2Id { get; }
    public int MaterialId { get; }
    public int Material2Id { get; }
    public int SoldCategoryId { get; }
    public int SoldDrinkId { get; }
    public int CashierId { get; }
    public int ManagerId { get; }

    public void Dispose()
    {
        TestDatabase.Drop(ConnectionString);
        try { Directory.Delete(Root, true); } catch (IOException) { }
    }
}

/// <summary>Test tích hợp các dịch vụ Phase 2 trên CSDL thử: OTP quên mật khẩu, đăng nhập, quy tắc xóa, khóa nhân viên, định mức.</summary>
[Collection("Database")]
public class Phase2ServiceTests
{
    private readonly ServiceDatabaseFixture _db;

    public Phase2ServiceTests(ServiceDatabaseFixture db)
    {
        _db = db;
        DBUtil.Init(db.ConnectionString);
    }

    private PasswordResetService Reset(CapturingEmailSender mail) =>
        new(_db.Clock, Options.Create(new AuthOptions()), mail, NullLogger<PasswordResetService>.Instance);

    private UploadService Uploads() => new(new FakeWebEnv(_db.Root), Options.Create(new UploadOptions { Path = Path.Combine(_db.Root, "wwwroot", "uploads") }));

    private static string CodeIn(string body) => System.Text.RegularExpressions.Regex.Match(body, @"\b\d{6}\b").Value;

    [Fact]
    public void Authenticate_ChecksPasswordAndActive()
    {
        var u = ServiceDatabaseFixture.NewUser("login.test@fpolycinema.vn", Roles.TicketInspector, _db.CinemaId);
        UserDAL.Insert(u);
        Assert.NotNull(UserDAL.Authenticate("login.test@fpolycinema.vn", "Matkhau123"));
        Assert.Null(UserDAL.Authenticate("login.test@fpolycinema.vn", "sai-mat-khau"));
        var auth = new AuthService(_db.Clock);
        UserDAL.SetActive(u.Id, false);
        Assert.Null(UserDAL.Authenticate("login.test@fpolycinema.vn", "Matkhau123"));
        Assert.Equal(LoginOutcome.Locked, auth.Login("login.test@fpolycinema.vn", "Matkhau123").Outcome);
        Assert.Equal(LoginOutcome.InvalidCredentials, auth.Login("khong-co@fpolycinema.vn", "Matkhau123").Outcome);
    }

    [Fact]
    public void ForgotPassword_OtpFlow_WrongThenRightSendsNewPasswordAndForcesChange()
    {
        var u = ServiceDatabaseFixture.NewUser("otp.test@fpolycinema.vn", Roles.Customer, null);
        UserDAL.Insert(u);
        var mail = new CapturingEmailSender();
        var svc = Reset(mail);

        // Email không tồn tại: cùng kết quả, không gửi thư (không lộ tài khoản)
        var r0 = svc.RequestOtp("khong-ton-tai@fpolycinema.vn");
        Assert.Empty(mail.Sent);
        var r1 = svc.RequestOtp(u.Email);
        Assert.Equal(r0.ValidSeconds, r1.ValidSeconds);
        Assert.Equal(300, r1.ValidSeconds);
        var code = CodeIn(Assert.Single(mail.Sent).Body);
        Assert.Equal(6, code.Length);
        var stored = PasswordResetOtpDAL.GetLatestForUser(u.Id)!;
        Assert.NotEqual(code, stored.OtpHash);                       // lưu dạng băm
        Assert.Equal(_db.Clock.Now.AddMinutes(5), stored.ExpiresAt); // hạn 5 phút

        var wrong = code == "000000" ? "111111" : "000000";
        var ex = Assert.Throws<BusinessRuleException>(() => svc.VerifyOtp(u.Email, wrong));
        Assert.Equal(ErrorCodes.OtpInvalid, ex.Code);
        Assert.Equal("Mã xác nhận chưa đúng. Bạn còn 4 lần thử.", ex.Message);

        svc.VerifyOtp(u.Email, code);
        Assert.Equal(2, mail.Sent.Count);
        var newPassword = System.Text.RegularExpressions.Regex.Match(mail.Sent[1].Body, @"Mật khẩu mới của bạn là: (\S+)").Groups[1].Value;
        Assert.Equal(10, newPassword.Length);
        var after = UserDAL.GetById(u.Id)!;
        Assert.True(after.MustChangePassword);
        Assert.NotNull(UserDAL.Authenticate(u.Email, newPassword));
        Assert.Null(UserDAL.Authenticate(u.Email, "Matkhau123"));
        // Mã đã dùng thì không dùng lại được
        Assert.Equal(ErrorCodes.OtpExpired, Assert.Throws<BusinessRuleException>(() => svc.VerifyOtp(u.Email, code)).Code);
    }

    [Fact]
    public void ForgotPassword_OtpExpiresAfterFiveMinutesAndAfterFiveWrongTries()
    {
        var u = ServiceDatabaseFixture.NewUser("otp2.test@fpolycinema.vn", Roles.Customer, null);
        UserDAL.Insert(u);
        var mail = new CapturingEmailSender();
        var clock = new FakeClock(_db.Clock.Now);
        var svc = new PasswordResetService(clock, Options.Create(new AuthOptions()), mail, NullLogger<PasswordResetService>.Instance);

        svc.RequestOtp(u.Email);
        var code = CodeIn(mail.Sent[^1].Body);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(ErrorCodes.OtpExpired, Assert.Throws<BusinessRuleException>(() => svc.VerifyOtp(u.Email, code)).Code);

        // Gửi lại trong 60 giây thì không gửi mã mới
        svc.RequestOtp(u.Email);
        int sent = mail.Sent.Count;
        var again = svc.RequestOtp(u.Email);
        Assert.Equal(sent, mail.Sent.Count);
        Assert.True(again.ResendAfterSeconds > 0);

        code = CodeIn(mail.Sent[^1].Body);
        var wrong = code == "000000" ? "111111" : "000000";
        for (int i = 1; i <= 4; i++)
            Assert.Equal(ErrorCodes.OtpInvalid, Assert.Throws<BusinessRuleException>(() => svc.VerifyOtp(u.Email, wrong)).Code);
        var last = Assert.Throws<BusinessRuleException>(() => svc.VerifyOtp(u.Email, wrong));
        Assert.Equal(ErrorCodes.OtpInvalid, last.Code);
        Assert.Contains("hết lượt thử", last.Message);
        // Sau 5 lần sai, mã đúng cũng không dùng được nữa
        Assert.Equal(ErrorCodes.OtpExpired, Assert.Throws<BusinessRuleException>(() => svc.VerifyOtp(u.Email, code)).Code);
    }

    [Fact]
    public void ChangePassword_ClearsMustChangeAndRejectsWrongCurrent()
    {
        var u = ServiceDatabaseFixture.NewUser("change.test@fpolycinema.vn", Roles.POSCashier, _db.CinemaId);
        u.MustChangePassword = true;
        UserDAL.Insert(u);
        var auth = new AuthService(_db.Clock);
        Assert.Equal(ErrorCodes.ValidationFailed, Assert.Throws<BusinessRuleException>(() => auth.ChangePassword(u.Id, "sai", "MatKhauMoi2026", "MatKhauMoi2026")).Code);
        var changed = auth.ChangePassword(u.Id, "Matkhau123", "MatKhauMoi2026", "MatKhauMoi2026");
        Assert.False(changed.MustChangePassword);
        Assert.False(UserDAL.GetById(u.Id)!.MustChangePassword);
        Assert.NotNull(UserDAL.Authenticate(u.Email, "MatKhauMoi2026"));
        // Dấu mật khẩu đổi nên các phiên cũ bị đăng xuất
        Assert.NotEqual(AuthService.PasswordStamp(u.PasswordHash), AuthService.PasswordStamp(changed.PasswordHash));
    }

    [Fact]
    public void Register_CreatesMemberCustomerAndRejectsDuplicateEmail()
    {
        var auth = new AuthService(_db.Clock);
        var u = auth.Register(new RegisterInput("Khách Thử", "dangky.test@gmail.com", "0901234567", new DateTime(2000, 1, 1), "Rapphim2026", "Rapphim2026"));
        Assert.Equal(Roles.Customer, u.Role);
        Assert.Matches("^TV\\d{8}$", u.MemberCode!);
        Assert.Equal(LoyaltyTier.Member, u.Tier);
        var dup = Assert.Throws<BusinessRuleException>(() => auth.Register(new RegisterInput("Khách Khác", "dangky.test@gmail.com", null, null, "Rapphim2026", "Rapphim2026")));
        Assert.Equal(ErrorCodes.ValidationFailed, dup.Code);
        Assert.Contains("Email", ((IDictionary<string, string>)dup.Details!).Keys);
    }

    [Fact]
    public void Category_DeleteBlockedWhenHasDrinks_AllowedWhenEmpty()
    {
        var svc = new CategoryService();
        var ex = Assert.Throws<BusinessRuleException>(() => svc.Delete(_db.SoldCategoryId));
        Assert.Equal(ErrorCodes.DeleteBlocked, ex.Code);
        Assert.Equal(422, ex.HttpStatus);
        var empty = svc.Save(null, "Loại trống thử", true);
        Assert.True(svc.CheckDelete(empty.Id).Allowed);
        svc.Delete(empty.Id);
        Assert.Null(CategoryDAL.GetById(empty.Id));
        Assert.Equal(ErrorCodes.ValidationFailed, Assert.Throws<BusinessRuleException>(() => svc.Save(null, "Bắp rang", true)).Code);
    }

    [Fact]
    public void Drink_SaveWithRecipesPerCinema_DeleteOnlyWhenNeverSold()
    {
        var svc = new DrinkService(Uploads());
        // Món đã có trong hóa đơn: chỉ được ngưng bán
        var blocked = Assert.Throws<BusinessRuleException>(() => svc.Delete(_db.SoldDrinkId));
        Assert.Equal(ErrorCodes.DeleteBlocked, blocked.Code);
        svc.SetActive(_db.SoldDrinkId, false);
        Assert.False(DrinkDAL.GetById(_db.SoldDrinkId)!.Active);

        var input = new DrinkInput(_db.SoldCategoryId, "Bắp mới thử", 45_000, "Mô tả", null, false, true,
            new[] { new RecipeInput(_db.MaterialId, 0.12m), new RecipeInput(_db.Material2Id, 0.15m) });
        var d = svc.Save(null, input, _db.Clock.Now);
        var lines = svc.Recipes(d.Id);
        Assert.Equal(2, lines.Count);
        Assert.Equal(new[] { _db.CinemaId, _db.Cinema2Id }, lines.Select(l => l.CinemaId).OrderBy(x => x));
        // Lưu lại thay toàn bộ định mức
        svc.Save(d.Id, input with { Recipes = new[] { new RecipeInput(_db.MaterialId, 0.2m) } }, _db.Clock.Now);
        Assert.Equal(0.2m, Assert.Single(svc.Recipes(d.Id)).Quantity);
        // Nguyên liệu trùng hoặc định mức không dương bị từ chối
        Assert.Throws<BusinessRuleException>(() => svc.Save(d.Id, input with { Recipes = new[] { new RecipeInput(_db.MaterialId, 0.1m), new RecipeInput(_db.MaterialId, 0.2m) } }, _db.Clock.Now));
        Assert.Throws<BusinessRuleException>(() => svc.Save(d.Id, input with { Recipes = new[] { new RecipeInput(_db.MaterialId, 0m) } }, _db.Clock.Now));
        Assert.Throws<BusinessRuleException>(() => svc.Save(d.Id, input with { Image = "C:/Windows/win.ini" }, _db.Clock.Now));
        Assert.True(svc.CheckDelete(d.Id).Allowed);
        svc.Delete(d.Id);
        Assert.Null(DrinkDAL.GetById(d.Id));
        Assert.Equal(0, ProductRecipeDAL.CountByDrink(d.Id));
    }

    [Fact]
    public void Staff_CreateEmailsPassword_SetActiveWritesAudit_DeleteRules()
    {
        var mail = new CapturingEmailSender();
        var svc = new StaffService(_db.Clock, Options.Create(new AuthOptions()), mail);
        var u = svc.Create(new StaffInput("gate.test@fpolycinema.vn", "Nhân Viên Thử", "0912345678", Roles.TicketInspector, _db.CinemaId));
        Assert.True(u.MustChangePassword);
        var pw = System.Text.RegularExpressions.Regex.Match(Assert.Single(mail.Sent).Body, @"Mật khẩu được cấp: (\S+)").Groups[1].Value;
        Assert.Equal(10, pw.Length);
        Assert.NotNull(UserDAL.Authenticate(u.Email, pw));
        // Nhân viên không phải quản lý phải gắn với một rạp
        Assert.Throws<BusinessRuleException>(() => svc.Create(new StaffInput("x.test@fpolycinema.vn", "X", null, Roles.POSCashier, null)));

        // Khóa và mở ghi AuditLogs; không ai tự khóa mình
        svc.SetActive(u.Id, false, _db.ManagerId);
        Assert.False(UserDAL.GetById(u.Id)!.Active);
        var logs = AuditLogDAL.GetAll().Where(l => l.EntityId == u.Id.ToString()).ToList();
        Assert.Contains(logs, l => l.Action == AuditAction.UserLocked && l.UserId == _db.ManagerId && l.EntityType == "Users");
        svc.SetActive(u.Id, true, _db.ManagerId);
        Assert.Contains(AuditLogDAL.GetAll(), l => l.Action == AuditAction.UserUnlocked && l.EntityId == u.Id.ToString());
        Assert.Throws<BusinessRuleException>(() => svc.SetActive(_db.ManagerId, false, _db.ManagerId));

        // Thu ngân đã lập hóa đơn: không xóa được, gợi ý khóa; tự xóa mình bị chặn; nhân viên mới xóa được
        var blocked = Assert.Throws<BusinessRuleException>(() => svc.Delete(_db.CashierId, _db.ManagerId));
        Assert.Equal(ErrorCodes.DeleteBlocked, blocked.Code);
        Assert.False(svc.CheckDelete(_db.ManagerId, _db.ManagerId).Allowed);
        var fresh = svc.Create(new StaffInput("xoa.test@fpolycinema.vn", "Nhân Viên Mới", null, Roles.POSCashier, _db.CinemaId));
        svc.Delete(fresh.Id, _db.ManagerId);
        Assert.Null(UserDAL.GetById(fresh.Id));
    }

    [Fact]
    public void Outbox_WritesMailForDevMailbox()
    {
        var before = EmailOutboxDAL.Count();
        new OutboxEmailSender(_db.Clock).Send("a@b.vn", "Tiêu đề", "Nội dung");
        Assert.Equal(before + 1, EmailOutboxDAL.Count());
        var m = EmailOutboxDAL.GetLatest(1).Single();
        Assert.Null(m.SentAt);
        Assert.Equal("Tiêu đề", m.Subject);
    }
}
