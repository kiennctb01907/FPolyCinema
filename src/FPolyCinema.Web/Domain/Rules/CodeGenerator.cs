using System.Security.Cryptography;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Sinh mã theo CONTRACTS mục 9:
/// - Mã hóa đơn: HD + yyyyMMddHHmmss + 4 ký tự in hoa ngẫu nhiên.
/// - Mã QR vé: TK + 12 ký tự ngẫu nhiên khó đoán (sinh bằng RandomNumberGenerator).
/// - Mã thành viên: TV + 8 chữ số.
/// - Số gọi bắp nước: 3 chữ số, đếm lại mỗi ngày theo rạp.
/// - Mã máy POS: POS-01, POS-02.
/// Không đọc đồng hồ: thời điểm do nơi gọi truyền vào từ IClock.
/// </summary>
public static class CodeGenerator
{
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    // Bỏ các ký tự dễ nhầm (0/O, 1/I/L) để nhân viên đọc mã khi máy quét lỗi
    private const string QrAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string BillCode(DateTime now) => "HD" + now.ToString("yyyyMMddHHmmss") + RandomString(Upper, 4);

    public static string TicketQrCode() => "TK" + RandomString(QrAlphabet, 12);

    public static string MemberCode(int sequence)
    {
        if (sequence < 0 || sequence > 99_999_999) throw new ArgumentOutOfRangeException(nameof(sequence));
        return "TV" + sequence.ToString("D8");
    }

    /// <summary>Số gọi kế tiếp trong ngày của một rạp: 1, 2, …, 999 rồi quay về 1.</summary>
    public static int NextPickupNumber(int? lastNumberToday) => lastNumberToday is null or >= 999 ? 1 : lastNumberToday.Value + 1;

    /// <summary>Hiển thị số gọi đủ 3 chữ số, ví dụ 7 → "007".</summary>
    public static string FormatPickupNumber(int number) => number.ToString("D3");

    public static string TerminalCode(int number) => "POS-" + number.ToString("D2");

    /// <summary>Mã voucher bù cho khách (SPEC 3.7, OVERBOOKED_REFUNDED): BU + 8 ký tự ngẫu nhiên dễ đọc.</summary>
    public static string CompensationVoucherCode() => "BU" + RandomString(QrAlphabet, 8);

    /// <summary>Mã giao dịch mô phỏng của cổng thanh toán: SIM + 17 ký tự ngẫu nhiên.</summary>
    public static string SimTxnId() => "SIM" + RandomString(QrAlphabet, 17);

    /// <summary>Mã voucher doanh nghiệp (FREE_TICKET): DN + 8 ký tự ngẫu nhiên khó đoán.</summary>
    public static string CorporateVoucherCode() => "DN" + RandomString(QrAlphabet, 8);

    private static string RandomString(string alphabet, int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }
}
