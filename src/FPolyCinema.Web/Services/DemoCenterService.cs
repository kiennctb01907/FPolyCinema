using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Infrastructure.Seed;

namespace FPolyCinema.Web.Services;

/// <summary>
/// Một màn hình của kịch bản demo. Kind = window: cửa sổ riêng trên tên miền phụ, tự đăng nhập bằng /demo/switch-role (SPEC S5-09);
/// Kind = frame: màn hình không cần đăng nhập, nhúng bằng iframe trong cửa sổ sân khấu (SPEC S5-10).
/// </summary>
public sealed record DemoScreen(string Kind, string Label, string Icon, string Url, string? Account, string? Host, WindowSlot? Slot, string? WindowName);

/// <summary>Một kịch bản dựng sẵn của trung tâm demo (UI D4): mô tả, các bước trình diễn, các cửa sổ cần mở và các màn hình nhúng iframe.</summary>
public sealed record DemoScenario(int No, string Title, string Icon, string Description, List<string> Steps, List<DemoScreen> Windows, List<DemoScreen> Frames,
    string? Notice, bool NeedsTicket);

/// <summary>Toàn bộ trung tâm demo: 5 kịch bản, có tách được phiên theo tên miền phụ hay không, vé mẫu hiện có của khách.</summary>
public sealed record DemoCenter(List<DemoScenario> Scenarios, bool SeparateSessions, string? TicketCode, string? TicketInfo, bool TicketReady, int CinemaId, string CinemaName,
    DateTime Now, int ClockOffsetMinutes);

/// <summary>
/// Trung tâm demo nhiều màn hình /demo/multi-screen (SPEC 5, UI D4, chỉ khi Demo:EnableTools): dựng 5 kịch bản ở SPEC mục 5 với địa chỉ thật của từng màn hình,
/// và tạo vé mẫu có bắp nước cho khách mẫu qua đúng luồng đặt vé online (giữ ghế, thêm combo, thanh toán VietQR mô phỏng có ký HMAC).
/// </summary>
public class DemoCenterService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly OnlineBookingService _booking;
    private readonly SeatSuggestService _suggest;

    public DemoCenterService(IClock clock, CinemaRules rules, OnlineBookingService booking, SeatSuggestService suggest)
    {
        _clock = clock;
        _rules = rules;
        _booking = booking;
        _suggest = suggest;
    }

    /// <summary>Rạp của các tài khoản nhân viên mẫu (rạp 1); không có thì rạp đầu tiên.</summary>
    public static (int Id, string Name) DemoCinema()
    {
        var gate = DemoAccounts.Find("gate") is { } a ? UserDAL.GetByEmail(a.Email) : null;
        var cinema = gate?.CinemaId is int cid ? CinemaDAL.GetById(cid) : null;
        cinema ??= CinemaDAL.GetAll().Where(c => c.Active).OrderBy(c => c.Id).FirstOrDefault();
        return cinema == null ? (0, string.Empty) : (cinema.Id, cinema.Name);
    }

    /// <summary>Suất còn chiếu của rạp từ 3 giờ trước đến 7 ngày tới, kèm cờ còn bán online theo giờ hiện tại.</summary>
    public List<DemoShowtimeOption> Options(int cinemaId)
    {
        var now = _clock.Now;
        return ShowtimeDAL.ForCustomer(cinemaId, null, now.AddHours(-3), now.AddDays(7), now)
            .Select(r => new DemoShowtimeOption(r.ShowtimeId, r.StartTime, r.FeatureStartTime, r.EndTime, r.OperationalStatus, r.FreeSeats,
                SeatHoldRules.IsSalesOpen(r.FeatureStartTime, r.OperationalStatus, BillChannel.Online, now, _rules)))
            .ToList();
    }

    /// <summary>Vé gần nhất của khách mẫu còn dùng được cho kịch bản bắp nước và soát vé (null nếu chưa có), kèm giờ bắt đầu của suất.</summary>
    public (string Code, string Info, DateTime Start)? UsableTicket()
    {
        var khach = UserDAL.GetByEmail(SeedCatalog.KhachEmail);
        if (khach == null) return null;
        var now = _clock.Now;
        var row = BillDAL.ForCustomer(khach.Id)
            .Where(b => DemoScreenRules.TicketUsable(b.Status, b.FnbStatus, b.CheckInStatus, b.StartTime, b.EndTime, now))
            .OrderBy(b => b.StartTime).FirstOrDefault();
        return row == null ? null : (row.Code, $"{row.MovieTitle} · {Fmt.DateTime(row.StartTime!.Value)} · {row.AuditoriumName}", row.StartTime!.Value);
    }

    /// <summary>Dựng 5 kịch bản với địa chỉ cửa sổ theo scheme, host và cổng của yêu cầu hiện tại.</summary>
    public DemoCenter Build(string scheme, string host, int? port)
    {
        var (cinemaId, cinemaName) = DemoCinema();
        var options = cinemaId == 0 ? new List<DemoShowtimeOption>() : Options(cinemaId);
        var now = _clock.Now;
        var contest = DemoScreenRules.PickContest(options, now);
        var ticket = UsableTicket();
        var cashier = DemoAccounts.Find("cashier") is { } ca ? UserDAL.GetByEmail(ca.Email) : null;
        var shift = cashier == null ? null : PosShiftDAL.GetOpenByUser(cashier.Id);
        var terminal = shift?.TerminalCode ?? "POS-01";
        var displayCinema = shift?.CinemaId ?? cinemaId;

        DemoScreen Win(int scenario, string sub, string account, string path, string label, string icon) =>
            new("window", label, icon, DemoScreenRules.WindowOrigin(scheme, host, port, sub) + DemoScreenRules.SwitchRolePath(account, path),
                DemoAccounts.Find(account)?.Label, new Uri(DemoScreenRules.WindowOrigin(scheme, host, port, sub)).Host, null, $"fpc-demo-{scenario}-{sub}");
        DemoScreen Frame(string path, string label, string icon) => new("frame", label, icon, path, null, null, null, null);
        DemoScreen Stage(int scenario, string label) =>
            new("window", label, "bi-display", $"{scheme}://{host}{(port is int p ? ":" + p : string.Empty)}/demo/multi-screen/stage?scenario={scenario}",
                "Không cần đăng nhập", host, null, $"fpc-demo-{scenario}-stage");

        var ticketPath = ticket is { } t ? OnlineBookingService.TicketUrl(t.Code) : "/account?tab=tickets";
        const string prepareHint = "Muốn demo ngay, bạn bấm \"Tạo vé mẫu có bắp nước\" ở đầu trang để lấy vé ở suất gần nhất còn bán online.";
        bool ready = ticket is { } tk && DemoScreenRules.ReadyNow(tk.Start, now, _rules.ImHereWindowMinutesBeforeStart);
        string? fnbNotice = ticket is not { } t3
            ? "Khách mẫu chưa có vé kèm bắp nước cho suất sắp chiếu. Bạn bấm \"Tạo vé mẫu có bắp nước\" ở đầu trang trước khi mở các màn hình."
            : !ready
                ? $"Vé {t3.Code} thuộc suất {Fmt.DateTime(t3.Start)}: nút \"Tôi đã tới rạp\" hiện từ {Fmt.Time(t3.Start.AddMinutes(-_rules.ImHereWindowMinutesBeforeStart))}. {prepareHint}"
                : null;
        string gateNotice = ticket is not { } t4
            ? fnbNotice!
            : now < t4.Start.AddMinutes(-_rules.BoardingLeadMinutes)
                ? $"Vé {t4.Code} thuộc suất {Fmt.DateTime(t4.Start)}: máy soát vé cho vào từ {Fmt.Time(t4.Start.AddMinutes(-_rules.BoardingLeadMinutes))}, quét sớm hơn sẽ báo \"Chưa tới giờ vào\". {prepareHint}"
                : "Suất của vé đang mở cửa đón khách. Nếu máy báo \"Phòng đang dọn vệ sinh\", bạn bấm \"Đã dọn xong\" ở khung Phòng đang dọn của máy soát vé.";

        var scenarios = new List<DemoScenario>
        {
            new(1, "Hai khách cùng giành một ghế", "bi-people",
                "Hai khách đăng nhập ở hai cửa sổ riêng (khach-a và khach-b) cùng mở sơ đồ ghế của một suất. Ghế người này giữ đổi màu ngay ở cửa sổ kia qua kênh sơ đồ ghế.",
                new List<string>
                {
                    "Cửa sổ trái chọn một ghế: ghế chuyển sang \"Đang giữ\" ngay ở cửa sổ phải.",
                    "Cửa sổ phải bấm đúng ghế đó: hệ thống báo ghế đã có người giữ và không cho chọn.",
                    "Cửa sổ trái bỏ chọn: ghế trống lại ở cả hai cửa sổ.",
                },
                contest == null
                    ? new List<DemoScreen> { Win(1, DemoScreenRules.KhachA, "khach", "/showtimes", "Khách A – lịch chiếu", "bi-person"), Win(1, DemoScreenRules.KhachB, "khach2", "/showtimes", "Khách B – lịch chiếu", "bi-person") }
                    : new List<DemoScreen> { Win(1, DemoScreenRules.KhachA, "khach", $"/booking/{contest.Id}", "Khách A – chọn ghế", "bi-person"), Win(1, DemoScreenRules.KhachB, "khach2", $"/booking/{contest.Id}", "Khách B – chọn ghế", "bi-person") },
                new List<DemoScreen>(),
                contest == null ? "Rạp mẫu không còn suất nào bán online trong 7 ngày tới. Hai cửa sổ mở trang lịch chiếu; bạn chạy lại với --reset-db để có suất mới." : null,
                false),
            new(2, "POS và màn hình phụ của khách", "bi-display",
                $"Thu ngân bán vé ở máy {terminal}; màn hình phụ quay về phía khách (nhúng iframe) hiện suất, ghế, giỏ hàng và mã QR thanh toán theo từng thao tác của thu ngân.",
                new List<string>
                {
                    "Thu ngân chọn một suất: màn hình phụ hiện phim và sơ đồ ghế.",
                    "Chọn ghế, thêm bắp nước: giỏ hàng và tổng tiền trên màn hình phụ đổi ngay.",
                    "Thanh toán bằng VietQR: màn hình phụ hiện mã QR; thanh toán xong thì hiện lời cảm ơn rồi về màn hình chờ.",
                },
                new List<DemoScreen> { Win(2, DemoScreenRules.Cashier, "cashier", "/pos", "Thu ngân – POS", "bi-cash-coin"), Stage(2, "Màn hình phụ của khách") },
                new List<DemoScreen> { Frame($"/pos/customer-display?terminal={Uri.EscapeDataString(terminal)}&cinemaId={displayCinema}", $"Màn hình phụ – máy {terminal}", "bi-display") },
                shift == null ? "Thu ngân mẫu chưa có ca đang mở: cửa sổ POS sẽ mở màn hình Mở ca trước, bạn mở ca tại máy POS-01." : null,
                false),
            new(3, "Khách đặt bắp nước, bếp và bảng gọi số", "bi-cup-straw",
                "Khách có vé kèm combo bấm \"Tôi đã tới rạp – làm bắp nước ngay\" trên vé điện tử; đơn hiện ngay ở màn hình bếp, bếp báo xong thì số gọi lên bảng gọi số (nhúng iframe) và vé của khách đổi trạng thái.",
                new List<string>
                {
                    "Cửa sổ khách (trái) bấm \"Tôi đã tới rạp – làm bắp nước ngay\": vé hiện số gọi.",
                    "Màn hình bếp (phải trên) có đơn mới ở cột Đang chuẩn bị; bấm \"Sẵn sàng giao\".",
                    "Bảng gọi số (phải dưới) đưa số của khách sang cột \"Mời nhận đồ\"; vé điện tử báo món đã sẵn sàng mà không cần tải lại trang.",
                    "Bếp bấm \"Đã giao\": số rời khỏi bảng gọi số.",
                },
                new List<DemoScreen> { Win(3, DemoScreenRules.KhachA, "khach", ticketPath, "Khách – vé điện tử", "bi-ticket-perforated"), Win(3, DemoScreenRules.Cashier, "cashier", "/pos/fnb-kds", "Bếp – màn hình bếp", "bi-fire"), Stage(3, "Bảng gọi số") },
                new List<DemoScreen> { Frame($"/lobby/pickup-board?cinemaId={cinemaId}", "Bảng gọi số", "bi-megaphone") },
                fnbNotice, true),
            new(4, "Máy soát vé, vé điện tử và bảng sảnh", "bi-qr-code-scan",
                "Nhân viên soát vé quét hoặc nhập mã trên vé điện tử của khách; vé của khách đổi sang \"Đã vào cửa\" ngay, bảng sảnh (nhúng iframe) hiện trạng thái mở cửa của suất.",
                new List<string>
                {
                    "Bảng sảnh (phải dưới) hiện suất đang mở cửa đón khách.",
                    "Cửa sổ khách (phải trên) đưa mã vé; máy soát vé (trái) quét camera hoặc gõ mã HD… rồi bấm \"Cho vào tất cả\".",
                    "Vé điện tử của khách chuyển sang \"Đã vào cửa\" mà không cần tải lại trang.",
                },
                new List<DemoScreen> { Win(4, DemoScreenRules.Gate, "gate", "/pos/gate-inspector", "Soát vé – máy soát vé", "bi-qr-code-scan"), Win(4, DemoScreenRules.KhachA, "khach", ticketPath, "Khách – vé điện tử", "bi-ticket-perforated"), Stage(4, "Bảng sảnh") },
                new List<DemoScreen> { Frame($"/lobby/digital-signage?cinemaId={cinemaId}", "Bảng sảnh", "bi-easel") },
                gateNotice,
                true),
            new(5, "Thu ngân xin hủy hóa đơn, quản lý ca duyệt từ xa", "bi-shield-check",
                "Thu ngân gửi yêu cầu hủy một hóa đơn đã thanh toán; quản lý ca ở cửa sổ khác nhận cửa sổ phê duyệt ngay, duyệt hoặc từ chối, và kết quả về lại quầy tức thì.",
                new List<string>
                {
                    "Thu ngân (trái) mở \"Hóa đơn của tôi\", chọn một hóa đơn đã thanh toán, bấm \"Hủy\" rồi \"Gửi quản lý duyệt từ xa\".",
                    "Quản lý ca (phải) thấy cửa sổ phê duyệt và số trên chuông tăng; bấm \"Duyệt\" hoặc \"Từ chối\".",
                    "Quầy nhận kết quả ngay: hóa đơn chuyển sang Đã hủy (nếu được duyệt) hoặc giữ nguyên kèm lý do từ chối.",
                },
                new List<DemoScreen> { Win(5, DemoScreenRules.Cashier, "cashier", "/pos/my-bills", "Thu ngân – hóa đơn của tôi", "bi-receipt"), Win(5, DemoScreenRules.Manager, "shift", "/ops", "Quản lý ca – vận hành", "bi-person-check") },
                new List<DemoScreen>(),
                shift == null ? "Thu ngân mẫu chưa có ca đang mở: bạn mở ca và bán một vé trước để có hóa đơn cần hủy." : null,
                false),
        };

        foreach (var s in scenarios)
        {
            var slots = DemoScreenRules.Slots(s.Windows.Count);
            for (int i = 0; i < s.Windows.Count; i++) s.Windows[i] = s.Windows[i] with { Slot = slots[i] };
        }
        return new DemoCenter(scenarios, DemoScreenRules.IsLocalHost(host), ticket?.Code, ticket?.Info, ready, cinemaId, cinemaName,
            now, _clock is SystemClock sc ? (int)sc.Offset.TotalMinutes : 0);
    }

    /// <summary>Kịch bản theo số thứ tự (1–5), null nếu không có.</summary>
    public DemoScenario? Scenario(int no, string scheme, string host, int? port) => Build(scheme, host, port).Scenarios.FirstOrDefault(s => s.No == no);

    /// <summary>
    /// Tạo vé mẫu có bắp nước cho khách mẫu (khach@fpolycinema.vn) ở suất phù hợp nhất cho kịch bản 3 và 4 (DemoScreenRules.PickEntry): bỏ đơn đang dở của khách,
    /// giữ 2 ghế theo gợi ý ghế AI, thêm một combo, thanh toán VietQR mô phỏng và gửi webhook thành công có chữ ký – đúng luồng đặt vé online của khách.
    /// Trả mã hóa đơn (dùng làm mã vé) và giờ bắt đầu của suất.
    /// </summary>
    public async Task<(string Code, DateTime Start)> PrepareTicket()
    {
        var khach = UserDAL.GetByEmail(SeedCatalog.KhachEmail);
        if (khach == null || !khach.Active)
            throw new BusinessRuleException(ErrorCodes.NotFound, "Không có tài khoản khách mẫu khach@fpolycinema.vn. Bạn chạy lại với --reset-db để nạp lại dữ liệu mẫu.");
        var (cinemaId, _) = DemoCinema();
        var now = _clock.Now;
        var pick = DemoScreenRules.PickEntry(Options(cinemaId), now, _rules.ImHereWindowMinutesBeforeStart)
            ?? throw new BusinessRuleException(ErrorCodes.SalesClosed, "Rạp mẫu không còn suất nào bán online. Bạn chạy lại với --reset-db để có lịch chiếu mới.");
        var combo = DrinkDAL.GetAll().Where(d => d.Active && d.IsCombo && !d.IsSoldOut)
            .OrderBy(d => d.Name == "Combo Couple" ? 0 : 1).ThenBy(d => d.Id).FirstOrDefault()
            ?? throw new BusinessRuleException(ErrorCodes.StockInsufficient, "Không còn combo nào đang bán để tạo vé mẫu.");

        await _booking.CancelWaiting(khach.Id);
        var clusters = _suggest.Suggest(pick.Id, null, 2, null).Clusters;
        BusinessRuleException? last = null;
        bool held = false;
        foreach (var c in clusters.Take(3))
        {
            try
            {
                await _booking.Hold(khach.Id, pick.Id, c.SeatIds);
                held = true;
                break;
            }
            catch (BusinessRuleException ex) { last = ex; }
        }
        if (!held) throw last ?? new BusinessRuleException(ErrorCodes.SeatTaken, "Suất được chọn không còn 2 ghế liền nhau. Bạn thử lại sau.");
        await _booking.UpdateQuantity(khach.Id, pick.Id, combo.Id, "incr");
        await _booking.Confirm(khach.Id, pick.Id, true, true);
        var result = await _booking.Checkout(khach.Id, pick.Id, PaymentMethod.VietQrSim);
        var bill = BillDAL.GetByCode(result.Code)!;
        var (ok, sig, _, _) = _booking.SimPayloads(bill);
        var hook = await _booking.HandleWebhook(ok, sig);
        if (hook.Status != 200) throw new BusinessRuleException(ErrorCodes.ServerError, "Không thanh toán được vé mẫu: " + hook.Message);
        return (result.Code, pick.Start);
    }
}
