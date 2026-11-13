using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Thông tin phòng chiếu (form A3): rạp, tên, định dạng, kích thước lưới khi tạo, tỷ lệ hàng lý tưởng.</summary>
public sealed record AuditoriumInput(int CinemaId, string? Name, string? Format, int TotalRows, int TotalCols, decimal IdealRowRatio, bool Active);

/// <summary>Kết quả xem trước sơ đồ: lỗi, ghế đã dựng (nhãn, điểm, mức heatmap), gợi ý ghế nội bộ, thống kê.</summary>
public sealed record LayoutPreview(IReadOnlyList<string> Errors, IReadOnlyList<LayoutSeat> Seats, IReadOnlyList<(int Row, int Col)> SuggestedHouse, LayoutStats Stats);

/// <summary>
/// Quản lý phòng chiếu và sơ đồ ghế (UC32, SPEC 3.2, 3.14, UI A4):
/// - Lưu sơ đồ: gán nhãn (SeatLabeler), ghép cặp ghế đôi, tính lại BaseViewScore (BEST_VIEW_AUDIO) mỗi lần lưu.
/// - Phòng đã có suất tương lai bán vé thì không sửa cấu trúc, chỉ bật tắt IsMaintenance.
/// - Ghế đã có vé (kể cả vé cũ) không xóa khỏi sơ đồ được; đổi sang bảo trì thay vì xóa.
/// - Thêm, bớt ghế thì cập nhật ShowtimeSeats của các suất tương lai chưa bán; ghế nội bộ nhận HOUSE_HOLD.
/// - Phòng chỉ xóa được khi chưa có suất chiếu nào.
/// </summary>
public class AuditoriumService
{
    private readonly CinemaRules _rules;

    public AuditoriumService(CinemaRules rules) => _rules = rules;

    public List<AuditoriumRow> List(int? cinemaId) => AuditoriumDAL.GetRows(cinemaId);

    public Auditorium Get(int id) => AuditoriumDAL.GetById(id) ?? throw CategoryService.NotFound();

    public List<Seat> Seats(int id) => SeatDAL.GetByAuditorium(id);

    /// <summary>Phòng đang bị khóa cấu trúc vì có suất tương lai đã bán vé hoặc đang giữ ghế.</summary>
    public bool IsStructureLocked(int id, DateTime now) => AuditoriumDAL.CountFutureShowtimesWithSales(id, now) > 0;

    private static Dictionary<string, string> ValidateInfo(int? id, AuditoriumInput input, bool creating)
    {
        var errors = new Dictionary<string, string>();
        var name = (input.Name ?? string.Empty).Trim();
        if (CinemaDAL.GetById(input.CinemaId) == null) errors["CinemaId"] = "Bạn chọn rạp của phòng.";
        if (name.Length == 0) errors["Name"] = "Bạn chưa nhập tên phòng, ví dụ Phòng 3.";
        else if (name.Length > 100) errors["Name"] = "Tên phòng tối đa 100 ký tự.";
        else if (!errors.ContainsKey("CinemaId") && AuditoriumDAL.NameExists(input.CinemaId, name, id)) errors["Name"] = "Rạp đã có phòng cùng tên. Bạn đặt tên khác.";
        if (!AuditoriumFormat.All.Contains(input.Format)) errors["Format"] = "Bạn chọn định dạng phòng.";
        if (creating)
        {
            if (input.TotalRows < 1 || input.TotalRows > AuditoriumLayout.MaxRows) errors["TotalRows"] = $"Số hàng phải từ 1 đến {AuditoriumLayout.MaxRows}.";
            if (input.TotalCols < 1 || input.TotalCols > AuditoriumLayout.MaxCols) errors["TotalCols"] = $"Số cột phải từ 1 đến {AuditoriumLayout.MaxCols}.";
        }
        if (input.IdealRowRatio <= 0 || input.IdealRowRatio > 1) errors["IdealRowRatio"] = "Tỷ lệ hàng lý tưởng phải lớn hơn 0 và không quá 1, mặc định 0,65.";
        return errors;
    }

    /// <summary>Tạo phòng mới với lưới đầy ghế thường; tô lại lối đi, ghế VIP… ở công cụ thiết kế.</summary>
    public Auditorium Create(AuditoriumInput input)
    {
        var errors = ValidateInfo(null, input, creating: true);
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        var cells = (from r in Enumerable.Range(1, input.TotalRows) from c in Enumerable.Range(1, input.TotalCols)
                     select new LayoutCell(r, c, SeatType.Standard, false, false)).ToList();
        var ratio = Math.Round(input.IdealRowRatio, 2);
        var seats = AuditoriumLayout.Build(input.TotalRows, input.TotalCols, cells, ratio);
        return DBUtil.InTransaction((conn, tx) =>
        {
            var a = new Auditorium
            {
                CinemaId = input.CinemaId, Name = input.Name!.Trim(), Format = input.Format!, TotalRows = input.TotalRows, TotalCols = input.TotalCols,
                IdealRowRatio = ratio, Active = input.Active,
            };
            AuditoriumDAL.Insert(a, conn, tx);
            foreach (var s in seats)
                SeatDAL.Insert(new Seat { AuditoriumId = a.Id, RowIndex = s.Row, ColIndex = s.Col, Label = s.Label, SeatType = s.SeatType, BaseViewScore = s.Score }, conn, tx);
            return a;
        });
    }

    /// <summary>Sửa tên, định dạng, tỷ lệ hàng lý tưởng, trạng thái. Đổi tỷ lệ thì tính lại BaseViewScore của mọi ghế.</summary>
    public Auditorium UpdateInfo(int id, AuditoriumInput input)
    {
        var current = Get(id);
        var errors = ValidateInfo(id, input with { CinemaId = current.CinemaId }, creating: false);
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        var ratio = Math.Round(input.IdealRowRatio, 2);
        return DBUtil.InTransaction((conn, tx) =>
        {
            var a = AuditoriumDAL.GetById(id, conn, tx) ?? throw CategoryService.NotFound();
            bool rescore = a.IdealRowRatio != ratio;
            a.Name = input.Name!.Trim(); a.Format = input.Format!; a.IdealRowRatio = ratio; a.Active = input.Active;
            AuditoriumDAL.Update(a, conn, tx);
            if (rescore)
                foreach (var s in SeatDAL.GetByAuditorium(id, conn, tx))
                {
                    s.BaseViewScore = SeatScoring.Score(a.TotalRows, a.TotalCols, s.RowIndex, s.ColIndex, s.SeatType, SeatPersona.BestViewAudio, ratio);
                    SeatDAL.Update(s, conn, tx);
                }
            return a;
        });
    }

    /// <summary>Xem trước sơ đồ chưa lưu: nhãn "Tự đánh số", điểm cho heatmap, gợi ý ghế nội bộ, thống kê.</summary>
    public LayoutPreview Preview(int id, int rows, int cols, IReadOnlyCollection<LayoutCell> cells)
    {
        var a = Get(id);
        var errors = AuditoriumLayout.Validate(rows, cols, cells);
        if (errors.Count > 0 && (rows < 1 || cols < 1 || rows > AuditoriumLayout.MaxRows || cols > AuditoriumLayout.MaxCols))
            return new LayoutPreview(errors, Array.Empty<LayoutSeat>(), Array.Empty<(int, int)>(), AuditoriumLayout.Stats(cells));
        var inGrid = cells.Where(c => c.Row >= 1 && c.Row <= rows && c.Col >= 1 && c.Col <= cols).GroupBy(c => (c.Row, c.Col)).Select(g => g.First()).ToList();
        var seats = AuditoriumLayout.Build(rows, cols, inGrid, a.IdealRowRatio);
        var house = AuditoriumLayout.SuggestHouseSeats(rows, cols, inGrid, _rules.HouseSeatsPerAuditorium, a.IdealRowRatio);
        return new LayoutPreview(errors, seats, house, AuditoriumLayout.Stats(inGrid));
    }

    /// <summary>
    /// Lưu sơ đồ (SPEC 3.2). Trả về true nếu đã lưu cấu trúc, false nếu chỉ lưu ghế bảo trì (phòng đang khóa cấu trúc).
    /// BaseViewScore luôn được tính lại và lưu.
    /// </summary>
    public bool SaveLayout(int id, int rows, int cols, IReadOnlyCollection<LayoutCell> cells, DateTime now)
    {
        var errors = AuditoriumLayout.Validate(rows, cols, cells);
        if (errors.Count > 0) throw FieldErrors.Of("layout", string.Join(" ", errors));

        return DBUtil.InTransaction((conn, tx) =>
        {
            var a = AuditoriumDAL.GetById(id, conn, tx) ?? throw CategoryService.NotFound();
            var existing = SeatDAL.GetByAuditorium(id, conn, tx);
            var oldCells = existing.Select(s => new LayoutCell(s.RowIndex, s.ColIndex, s.SeatType, s.IsHouseSeat, s.IsMaintenance)).ToList();
            bool structural = AuditoriumLayout.IsStructuralChange(a.TotalRows, a.TotalCols, oldCells, rows, cols, cells);
            bool locked = AuditoriumDAL.CountFutureShowtimesWithSales(id, now, conn, tx) > 0;
            var byPos = existing.ToDictionary(s => (s.RowIndex, s.ColIndex));
            var built = AuditoriumLayout.Build(rows, cols, cells, a.IdealRowRatio);

            if (!structural)
            {
                // Chỉ bật tắt ghế bảo trì (luôn được phép, kể cả khi phòng đã bán vé suất tương lai)
                foreach (var s in built)
                {
                    var seat = byPos[(s.Row, s.Col)];
                    if (seat.IsMaintenance != s.IsMaintenance || seat.BaseViewScore != s.Score)
                    {
                        seat.IsMaintenance = s.IsMaintenance;
                        seat.BaseViewScore = s.Score;
                        SeatDAL.Update(seat, conn, tx);
                    }
                }
                // TODO(Phase 4): ghế vừa chuyển bảo trì mà đã bán ở suất tương lai thì vào danh sách "Cần đổi ghế" của dashboard (SPEC 3.2).
                return false;
            }
            if (locked)
                throw new BusinessRuleException(ErrorCodes.ValidationFailed,
                    "Phòng này đã có suất sắp chiếu bán vé nên không sửa được cấu trúc sơ đồ. Bạn chỉ bật hoặc tắt ghế bảo trì, hoặc chờ các suất đó chiếu xong.",
                    new Dictionary<string, string> { ["layout"] = "Phòng đang khóa cấu trúc vì đã bán vé suất sắp chiếu." });

            var newPos = built.ToDictionary(s => (s.Row, s.Col));
            var removed = existing.Where(s => !newPos.ContainsKey((s.RowIndex, s.ColIndex))).ToList();
            var withTickets = SeatDAL.IdsWithTickets(id, conn, tx);
            var blocked = removed.Where(s => withTickets.Contains(s.Id)).Select(s => s.Label).ToList();
            if (blocked.Count > 0)
                throw new BusinessRuleException(ErrorCodes.ValidationFailed,
                    $"Ghế {string.Join(", ", blocked.Take(8))}{(blocked.Count > 8 ? "…" : string.Empty)} đã có vé trong lịch sử bán nên không xóa khỏi sơ đồ được. Bạn giữ lại các ghế này và đặt chế độ bảo trì nếu không dùng nữa.",
                    new Dictionary<string, string> { ["layout"] = "Có ghế đã bán vé bị xóa khỏi sơ đồ." });

            int pairBase = SeatDAL.MaxPairGroupId(conn, tx);
            SeatDAL.SetTemporaryLabels(id, conn, tx);
            foreach (var s in removed)
            {
                ShowtimeSeatDAL.DeleteForSeat(s.Id, conn, tx);
                SeatDAL.Delete(s.Id, conn, tx);
            }
            foreach (var s in built)
            {
                int? pair = s.PairIndex is int p ? pairBase + p : null;
                if (byPos.TryGetValue((s.Row, s.Col), out var seat))
                {
                    seat.Label = s.Label; seat.SeatType = s.SeatType; seat.PairGroupId = pair; seat.IsHouseSeat = s.IsHouseSeat;
                    seat.IsMaintenance = s.IsMaintenance; seat.BaseViewScore = s.Score;
                    SeatDAL.Update(seat, conn, tx);
                }
                else
                {
                    int seatId = SeatDAL.Insert(new Seat
                    {
                        AuditoriumId = id, RowIndex = s.Row, ColIndex = s.Col, Label = s.Label, SeatType = s.SeatType, PairGroupId = pair,
                        IsHouseSeat = s.IsHouseSeat, IsMaintenance = s.IsMaintenance, BaseViewScore = s.Score,
                    }, conn, tx);
                    ShowtimeSeatDAL.AddSeatToFutureShowtimes(seatId, id, s.IsHouseSeat, now, conn, tx);
                }
            }
            ShowtimeSeatDAL.SyncHouseSeatsForFutureShowtimes(id, now, conn, tx);
            a.TotalRows = rows; a.TotalCols = cols;
            AuditoriumDAL.Update(a, conn, tx);
            return true;
        });
    }

    public DeleteDecision CheckDelete(int id)
    {
        var a = Get(id);
        return DeleteRules.Auditorium(AuditoriumDAL.CountShowtimes(id), SeatDAL.GetByAuditorium(id).Count, a.Active);
    }

    /// <summary>Xóa phòng (kèm ghế) chỉ khi chưa có suất chiếu nào (SPEC 3.14); bị chặn thì gợi ý ngưng hoạt động.</summary>
    public void Delete(int id)
    {
        DBUtil.InTransaction((conn, tx) =>
        {
            var a = AuditoriumDAL.GetById(id, conn, tx) ?? throw CategoryService.NotFound();
            var d = DeleteRules.Auditorium(AuditoriumDAL.CountShowtimes(id, conn, tx), 0, a.Active);
            if (!d.Allowed) throw CategoryService.Blocked(d);
            SeatDAL.DeleteByAuditorium(id, conn, tx);
            AuditoriumDAL.Delete(id, conn, tx);
            return 0;
        });
    }

    public void SetActive(int id, bool active)
    {
        if (AuditoriumDAL.SetActive(id, active) == 0) throw CategoryService.NotFound();
    }
}
