using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Web.Services;

/// <summary>Dữ liệu form thêm, sửa nhân viên.</summary>
/// <summary>Dữ liệu form nhân viên; ManagerPin là PIN quản lý 6 số (chỉ ShiftManager, Manager), để trống khi sửa là giữ PIN cũ.</summary>
public sealed record StaffInput(string? Email, string? FullName, string? Phone, string? Role, int? CinemaId, string? ManagerPin = null);

/// <summary>
/// Quản lý nhân viên (UC36, SPEC 3.14): thêm (cấp mật khẩu ngẫu nhiên qua email, bắt đổi khi đăng nhập), sửa,
/// khóa hoặc mở (SetActive, ghi AuditLogs), xóa cứng chỉ khi chưa lập hóa đơn nào. Không ai tự khóa hay xóa chính mình.
/// </summary>
public class StaffService
{
    private readonly IClock _clock;
    private readonly AuthOptions _auth;
    private readonly IEmailSender _email;

    public StaffService(IClock clock, IOptions<AuthOptions> auth, IEmailSender email)
    {
        _clock = clock;
        _auth = auth.Value;
        _email = email;
    }

    public static readonly string[] StaffRoles = { Roles.POSCashier, Roles.TicketInspector, Roles.ShiftManager, Roles.Manager };

    public PagedResult<User> Search(string? query, string? role, int? cinemaId, bool? active, int page, int pageSize)
    {
        var r = role != null && Array.IndexOf(StaffRoles, role) >= 0 ? role : null;
        var items = UserDAL.SearchStaff(query, r, cinemaId, active, Paging.Offset(page, pageSize), pageSize, out int total);
        return new PagedResult<User>(items, page, pageSize, total);
    }

    /// <summary>Nhân viên theo Id; khách hàng không quản lý ở đây nên coi như không tìm thấy.</summary>
    public User Get(int id)
    {
        var u = UserDAL.GetById(id);
        if (u == null || u.Role == Roles.Customer) throw CategoryService.NotFound();
        return u;
    }

    private Dictionary<string, string> Validate(StaffInput input, int? exceptId)
    {
        var errors = new Dictionary<string, string>();
        var email = (input.Email ?? string.Empty).Trim();
        if (email.Length == 0) errors["Email"] = "Bạn chưa nhập email.";
        else if (email.Length > 256 || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@')) errors["Email"] = "Email chưa đúng định dạng, ví dụ ten@fpolycinema.vn.";
        else if (UserDAL.EmailExists(email, exceptId)) errors["Email"] = "Email này đã có tài khoản khác dùng.";
        if (string.IsNullOrWhiteSpace(input.FullName)) errors["FullName"] = "Bạn chưa nhập họ tên.";
        else if (input.FullName.Trim().Length > 150) errors["FullName"] = "Họ tên tối đa 150 ký tự.";
        if (!string.IsNullOrWhiteSpace(input.Phone) && !AuthService.IsPhone(input.Phone)) errors["Phone"] = "Số điện thoại gồm 9–11 chữ số, ví dụ 0901234567.";
        if (input.Role == null || Array.IndexOf(StaffRoles, input.Role) < 0) errors["Role"] = "Bạn chọn vai trò cho nhân viên.";
        if (input.CinemaId.HasValue && CinemaDAL.GetById(input.CinemaId.Value) == null) errors["CinemaId"] = "Rạp đã chọn không còn hoạt động. Bạn chọn một rạp khác trong danh sách.";
        else if (!input.CinemaId.HasValue && input.Role != null && input.Role != Roles.Manager) errors["CinemaId"] = "Nhân viên này cần gắn với một rạp.";
        // PIN quản lý (SPEC 2.5): 6 chữ số, chỉ quản lý ca và quản lý rạp; bắt buộc khi chưa có PIN
        bool supervisor = input.Role is Roles.ShiftManager or Roles.Manager;
        var hasPin = exceptId is int uid && UserDAL.GetById(uid)?.ManagerPinHash != null;
        if (!string.IsNullOrEmpty(input.ManagerPin))
        {
            if (!supervisor) errors["ManagerPin"] = "Chỉ quản lý ca và quản lý rạp mới có PIN duyệt. Bạn để trống ô này.";
            else if (!ManagerPinRules.IsWellFormed(input.ManagerPin)) errors["ManagerPin"] = "PIN gồm đúng 6 chữ số.";
        }
        else if (supervisor && !hasPin) errors["ManagerPin"] = "Quản lý ca và quản lý rạp cần PIN 6 số để duyệt hủy hóa đơn.";
        return errors;
    }

    /// <summary>Thêm nhân viên: cấp mật khẩu ngẫu nhiên (Auth:NewPasswordLength), gửi qua email, MustChangePassword = 1.</summary>
    public User Create(StaffInput input)
    {
        var errors = Validate(input, null);
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        var password = PasswordRules.Generate(_auth.NewPasswordLength);
        var user = new User
        {
            Email = input.Email!.Trim(),
            PasswordHash = PasswordUtil.Hash(password),
            FullName = input.FullName!.Trim(),
            Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim(),
            Role = input.Role!,
            CinemaId = input.CinemaId,
            Active = true,
            MustChangePassword = true,
            Tier = LoyaltyTier.Member,
            CreatedAt = _clock.Now,
        };
        if (!string.IsNullOrEmpty(input.ManagerPin)) user.ManagerPinHash = PasswordUtil.Hash(input.ManagerPin);
        UserDAL.Insert(user);
        _email.Send(user.Email, "Tài khoản nhân viên FPoly Cinema",
            $"Xin chào {user.FullName},\n\nQuản lý rạp đã tạo tài khoản cho bạn với vai trò {StatusDisplay.RoleLabel(user.Role)}.\n" +
            $"Email đăng nhập: {user.Email}\nMật khẩu được cấp: {password}\n\n" +
            "Lần đăng nhập đầu tiên, hệ thống sẽ yêu cầu bạn đặt mật khẩu của riêng mình.\n\nFPoly Cinema");
        return user;
    }

    public User Update(int id, StaffInput input, int actorUserId)
    {
        var user = Get(id);
        var errors = Validate(input, id);
        if (id == actorUserId && input.Role != user.Role) errors["Role"] = "Bạn không thể tự đổi vai trò của tài khoản mình đang đăng nhập. Giữ vai trò cũ hoặc nhờ quản lý rạp khác đổi.";
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        UserDAL.UpdateStaff(id, input.Email!, input.FullName!.Trim(), string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim(), input.Role!, input.CinemaId);
        // Đặt PIN mới (lưu dạng băm, xóa đếm sai và khóa); vai trò không còn quyền duyệt thì gỡ PIN
        if (input.Role is not (Roles.ShiftManager or Roles.Manager)) UserDAL.SetManagerPin(id, null);
        else if (!string.IsNullOrEmpty(input.ManagerPin)) UserDAL.SetManagerPin(id, PasswordUtil.Hash(input.ManagerPin));
        return Get(id);
    }

    /// <summary>Khóa hoặc mở tài khoản (UserDAL.SetActive) và ghi AuditLogs USER_LOCKED / USER_UNLOCKED (SPEC 2.7).</summary>
    public void SetActive(int id, bool active, int actorUserId)
    {
        var user = Get(id);
        var reason = DeleteRules.CanSetStaffActive(actorUserId, id, active);
        if (reason != null) throw new BusinessRuleException(ErrorCodes.ValidationFailed, reason);
        DBUtil.InTransaction((conn, tx) =>
        {
            UserDAL.SetActive(id, active, conn, tx);
            AuditLogDAL.Insert(new AuditLog
            {
                UserId = actorUserId,
                Action = active ? AuditAction.UserUnlocked : AuditAction.UserLocked,
                EntityType = "Users",
                EntityId = id.ToString(),
                Detail = $"{(active ? "Mở" : "Khóa")} tài khoản {user.Email} ({StatusDisplay.RoleLabel(user.Role)})",
                CreatedAt = _clock.Now,
            }, conn, tx);
            return 0;
        });
    }

    public int BillsCreated(int id) => UserDAL.CountBillsCreated(id);

    public DeleteDecision CheckDelete(int id, int actorUserId)
    {
        var user = Get(id);
        return DeleteRules.Staff(UserDAL.CountBillsCreated(id), UserDAL.CountOperationalReferences(id), id == actorUserId, user.Active);
    }

    /// <summary>Xóa cứng nhân viên chưa lập hóa đơn nào (và chưa có dữ liệu vận hành); bị chặn thì DELETE_BLOCKED, gợi ý khóa tài khoản.</summary>
    public void Delete(int id, int actorUserId)
    {
        DBUtil.InTransaction((conn, tx) =>
        {
            var user = UserDAL.GetById(id, conn, tx);
            if (user == null || user.Role == Roles.Customer) throw CategoryService.NotFound();
            var decision = DeleteRules.Staff(UserDAL.CountBillsCreated(id, conn, tx), UserDAL.CountOperationalReferences(id, conn, tx), id == actorUserId, user.Active);
            if (!decision.Allowed) throw CategoryService.Blocked(decision);
            UserDAL.DeleteDependents(id, conn, tx);
            UserDAL.Delete(id, conn, tx);
            return 0;
        });
    }
}
