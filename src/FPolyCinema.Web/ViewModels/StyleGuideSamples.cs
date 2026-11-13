using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Ví dụ dựng sẵn cho trang style guide (UI D1): một sơ đồ ghế nhỏ có đủ loại ghế và trạng thái của UI mục 8.</summary>
public static class StyleGuideSamples
{
    public static SeatMapVm SeatMap()
    {
        // Sơ đồ mẫu có tương tác ở chế độ thử (không có suất): bấm chọn, phím mũi tên, tooltip điểm và giá
        var vm = new SeatMapVm { Rows = 5, Cols = 12, Interactive = true };
        int id = 0;
        for (int r = 1; r <= 5; r++)
        {
            int n = 0;
            for (int c = 1; c <= 12; c++)
            {
                if (c == 4 || c == 9) continue; // lối đi
                n++;
                var label = Domain.Rules.SeatLabeler.RowLetter(r).ToString() + n;
                var type = r == 5 && c is not (3 or 10) ? SeatType.Sweetbox : r == 3 && c is >= 5 and <= 8 ? SeatType.Vip : r == 1 && c is 1 or 12 ? SeatType.Wheelchair : SeatType.Standard;
                var status = (r, c) switch
                {
                    (2, 6) or (2, 7) => "SELECTED",
                    (2, 10) => SeatDisplayStatus.Held,
                    (3, 2) or (3, 3) or (4, 6) => SeatDisplayStatus.Booked,
                    (4, 12) => SeatDisplayStatus.HouseHold,
                    (1, 6) => SeatDisplayStatus.Maintenance,
                    _ => SeatDisplayStatus.Available,
                };
                int score = Math.Max(40, 100 - Math.Abs(3 - r) * 9 - Math.Abs(6 - c) * 4);
                long price = type switch { SeatType.Vip => 95_000, SeatType.Sweetbox => 120_000, _ => 80_000 };
                vm.Seats.Add(new SeatCell(r, c, label, type, status)
                {
                    SeatId = ++id, PairGroupId = type == SeatType.Sweetbox ? c switch { <= 2 => 1, <= 6 => 2, <= 8 => 3, _ => 4 } : null, Score = score,
                    ScoreNote = score >= 90 ? "tọa độ vàng" : score >= 75 ? "góc nhìn tốt" : string.Empty, Price = price,
                });
            }
        }
        return vm;
    }
}
