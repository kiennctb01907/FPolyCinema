namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Kết luận có được xóa cứng hay không (SPEC 3.14). Allowed = false thì giao diện giải thích Reason và,
/// nếu CanDeactivate, gợi ý "Ngưng hoạt động" (hoặc khóa tài khoản với nhân viên) thay cho xóa.
/// Consequences là danh sách hậu quả nêu trong hộp xác nhận khi được xóa.
/// </summary>
public sealed record DeleteDecision(bool Allowed, string? Reason, bool CanDeactivate, IReadOnlyList<string> Consequences)
{
    public static DeleteDecision Allow(params string[] consequences) => new(true, null, false, consequences);
    public static DeleteDecision Block(string reason, bool canDeactivate) => new(false, reason, canDeactivate, Array.Empty<string>());
}

/// <summary>Quy tắc xóa thuần của Phase 2 (SPEC 3.14): loại đồ uống, đồ uống, nhân viên.</summary>
public static class DeleteRules
{
    /// <summary>Loại đồ uống: chỉ xóa khi chưa có đồ uống nào.</summary>
    public static DeleteDecision Category(int drinkCount, bool isActive)
    {
        if (drinkCount > 0)
            return DeleteDecision.Block($"Loại này đang có {drinkCount} món nên không xóa được. Bạn có thể ngưng hoạt động loại này để ẩn khỏi nơi bán.", isActive);
        return DeleteDecision.Allow("Loại đồ uống bị xóa vĩnh viễn, không khôi phục được.");
    }

    /// <summary>Đồ uống: chỉ xóa cứng khi chưa xuất hiện trong hóa đơn; đã có thì chỉ được ngưng (Active = 0).</summary>
    public static DeleteDecision Drink(int billLineCount, int recipeCount, bool isActive)
    {
        if (billLineCount > 0)
            return DeleteDecision.Block($"Món này đã có trong {billLineCount} dòng hóa đơn nên không xóa được, để giữ đúng lịch sử bán hàng. Bạn có thể ngưng bán món này.", isActive);
        var list = new List<string> { "Món bị xóa vĩnh viễn, không khôi phục được." };
        if (recipeCount > 0) list.Add($"Xóa kèm {recipeCount} dòng định mức nguyên liệu của món.");
        list.Add("Ảnh món đã tải lên cũng bị xóa.");
        return new DeleteDecision(true, null, false, list);
    }

    /// <summary>
    /// Nhân viên: chỉ xóa cứng khi chưa tạo hóa đơn nào (và chưa có dữ liệu vận hành khác như ca, phê duyệt, nhật ký).
    /// Không ai được tự xóa tài khoản đang đăng nhập.
    /// </summary>
    public static DeleteDecision Staff(int billsCreated, int otherReferences, bool isSelf, bool isActive)
    {
        if (isSelf) return DeleteDecision.Block("Bạn không thể xóa tài khoản mình đang đăng nhập. Nhờ một quản lý rạp khác xóa nếu thật sự cần.", false);
        if (billsCreated > 0)
            return DeleteDecision.Block($"Nhân viên này đã lập {billsCreated} hóa đơn nên không xóa được. Bạn có thể khóa tài khoản để ngưng đăng nhập.", isActive);
        if (otherReferences > 0)
            return DeleteDecision.Block("Nhân viên này đã có dữ liệu vận hành (ca làm việc, phê duyệt, nhật ký hoặc phiếu kho) nên không xóa được. Bạn có thể khóa tài khoản để ngưng đăng nhập.", isActive);
        return DeleteDecision.Allow("Tài khoản bị xóa vĩnh viễn, nhân viên không đăng nhập được nữa.");
    }

    /// <summary>Khóa hoặc mở tài khoản: không ai được tự khóa tài khoản mình đang đăng nhập.</summary>
    public static string? CanSetStaffActive(int actorUserId, int targetUserId, bool active) =>
        !active && actorUserId == targetUserId ? "Bạn không thể tự khóa tài khoản mình đang đăng nhập. Nhờ một quản lý rạp khác khóa nếu cần." : null;
}
