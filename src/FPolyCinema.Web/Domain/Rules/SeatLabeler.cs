namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Nhãn ghế (SPEC 3.2): RowIndex 1 là hàng gần màn hình nhất và mang chữ A. Nhãn gồm chữ hàng và số thứ tự
/// ghế thật trong hàng tính từ trái sang (không đếm ô trống), ví dụ G7. Logic thuần, có unit test.
/// </summary>
public static class SeatLabeler
{
    public const int MaxRows = 26;

    /// <summary>Chữ của hàng: 1 → A, 2 → B, …, 26 → Z.</summary>
    public static char RowLetter(int rowIndex)
    {
        if (rowIndex < 1 || rowIndex > MaxRows) throw new ArgumentOutOfRangeException(nameof(rowIndex), "Hàng phải từ 1 đến 26.");
        return (char)('A' + rowIndex - 1);
    }

    /// <summary>Gán nhãn cho các ô có ghế; trả về từ điển (hàng, cột) → nhãn.</summary>
    public static Dictionary<(int Row, int Col), string> AssignLabels(IEnumerable<(int Row, int Col)> seatCells)
    {
        var result = new Dictionary<(int, int), string>();
        foreach (var row in seatCells.Distinct().GroupBy(c => c.Row))
        {
            int ordinal = 0;
            foreach (var cell in row.OrderBy(c => c.Col))
            {
                ordinal++;
                result[cell] = $"{RowLetter(cell.Row)}{ordinal}";
            }
        }
        return result;
    }
}
