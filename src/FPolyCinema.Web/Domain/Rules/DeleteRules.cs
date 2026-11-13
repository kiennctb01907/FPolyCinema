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

/// <summary>Quy tắc xóa thuần (SPEC 3.14): loại đồ uống, đồ uống, nhân viên (Phase 2); phim, nhà phát hành, phòng chiếu, suất chiếu (Phase 3).</summary>
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

    // ---------------------------------------------------------------- Phase 3
    /// <summary>Phim: chỉ xóa cứng khi chưa có suất chiếu hay vé (SPEC 3.14). Nếu đã có thì chỉ được ngưng (Active = 0).</summary>
    public static DeleteDecision Movie(int showtimeCount, int ticketCount, int kdmCount, int rentalRuleCount, bool isActive)
    {
        if (showtimeCount > 0 || ticketCount > 0)
        {
            var what = ticketCount > 0 ? $"{showtimeCount} suất chiếu và {ticketCount} vé" : $"{showtimeCount} suất chiếu";
            return DeleteDecision.Block($"Phim này đã có {what} nên không xóa được, để giữ đúng lịch sử chiếu và doanh thu. Bạn có thể ngưng chiếu phim này.", isActive);
        }
        var list = new List<string> { "Hồ sơ phim bị xóa vĩnh viễn, không khôi phục được." };
        if (kdmCount > 0) list.Add($"Xóa kèm {kdmCount} KDM của phim tại các rạp.");
        if (rentalRuleCount > 0) list.Add($"Xóa kèm {rentalRuleCount} quy tắc chia doanh thu.");
        list.Add("Poster đã tải lên cũng bị xóa nếu không phim nào khác dùng.");
        return new DeleteDecision(true, null, false, list);
    }

    /// <summary>Nhà phát hành: chỉ xóa khi chưa có phim nào; đã có thì chỉ được ngưng hợp tác.</summary>
    public static DeleteDecision Distributor(int movieCount, bool isActive)
    {
        if (movieCount > 0)
            return DeleteDecision.Block($"Nhà phát hành này đang có {movieCount} phim nên không xóa được. Bạn có thể ngưng hoạt động để ẩn khỏi ô chọn khi thêm phim.", isActive);
        return DeleteDecision.Allow("Nhà phát hành bị xóa vĩnh viễn, không khôi phục được.");
    }

    /// <summary>Phòng chiếu: chỉ xóa khi chưa có suất chiếu nào (SPEC 3.14). Đã có thì chỉ được ngưng hoạt động.</summary>
    public static DeleteDecision Auditorium(int showtimeCount, int seatCount, bool isActive)
    {
        if (showtimeCount > 0)
            return DeleteDecision.Block($"Phòng này đã có {showtimeCount} suất chiếu nên không xóa được, để giữ đúng lịch sử bán vé. Bạn có thể ngưng hoạt động phòng.", isActive);
        return DeleteDecision.Allow("Phòng chiếu bị xóa vĩnh viễn, không khôi phục được.", $"Xóa kèm {seatCount} ghế của sơ đồ.");
    }

    /// <summary>Suất chiếu: chỉ xóa khi chưa bán vé nào và không có ghế đang giữ; đã bán thì dùng hủy suất do sự cố.</summary>
    public static DeleteDecision Showtime(int ticketCount, bool isLocked, int corporateBookings)
    {
        if (ticketCount > 0 || isLocked)
            return DeleteDecision.Block("Suất này đã có khách mua vé hoặc đang giữ ghế nên không xóa được. Nếu cần, dùng chức năng Hủy suất do sự cố.", false);
        if (corporateBookings > 0)
            return DeleteDecision.Block("Suất này đã gắn với hợp đồng doanh nghiệp nên không xóa được.", false);
        return DeleteDecision.Allow("Suất chiếu bị xóa khỏi lịch, không khôi phục được.");
    }

    /// <summary>Khóa hoặc mở tài khoản: không ai được tự khóa tài khoản mình đang đăng nhập.</summary>
    public static string? CanSetStaffActive(int actorUserId, int targetUserId, bool active) =>
        !active && actorUserId == targetUserId ? "Bạn không thể tự khóa tài khoản mình đang đăng nhập. Nhờ một quản lý rạp khác khóa nếu cần." : null;
}
