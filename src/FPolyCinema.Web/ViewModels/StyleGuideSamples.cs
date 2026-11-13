using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Ví dụ dựng sẵn cho trang style guide (UI D1): một sơ đồ ghế nhỏ có đủ loại ghế và trạng thái của UI mục 8.</summary>
public static class StyleGuideSamples
{
    public static SeatMapVm SeatMap()
    {
        var vm = new SeatMapVm { Rows = 5, Cols = 12 };
        for (int r = 1; r <= 5; r++)
        {
            int n = 0;
            for (int c = 1; c <= 12; c++)
            {
                if (c == 4 || c == 9) continue; // lối đi
                if (r == 5 && c % 2 == 0) continue; // ô thứ hai của ghế đôi
                n++;
                var label = Domain.Rules.SeatLabeler.RowLetter(r).ToString() + n;
                var type = r == 5 ? SeatType.Sweetbox : r == 3 && c is >= 5 and <= 8 ? SeatType.Vip : r == 1 && c is 1 or 12 ? SeatType.Wheelchair : SeatType.Standard;
                var status = (r, c) switch
                {
                    (2, 6) or (2, 7) => "SELECTED",
                    (2, 10) => SeatDisplayStatus.Held,
                    (3, 2) or (3, 3) or (4, 6) => SeatDisplayStatus.Booked,
                    (4, 12) => SeatDisplayStatus.HouseHold,
                    (1, 6) => SeatDisplayStatus.Maintenance,
                    _ => SeatDisplayStatus.Available,
                };
                vm.Seats.Add(new SeatCell(r, c, label, type, status));
            }
        }
        return vm;
    }
}
