using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;

namespace FPolyCinema.Tests;

/// <summary>
/// Unit test NlpParser của LocalAgent (SPEC 7 S7-05: ít nhất 20 câu; SPEC 4.2-09 → 4.2-25). Hôm nay là thứ Năm 12/11/2026:
/// thứ Sáu 13/11, thứ Bảy 14/11, Chủ nhật 15/11, thứ Hai tuần sau 16/11.
/// </summary>
public class NlpParserTests
{
    private static readonly DateTime Thursday = new(2026, 11, 12);
    private static readonly TimeSpan DayStart = new(8, 0, 0);

    private static readonly List<NlpCatalogItem> Movies = new()
    {
        new(1, "Lật Mặt 8: Vòng Tay Nắng", "Face Off 8"),
        new(2, "Mai"),
        new(3, "Doraemon: Nobita và bản giao hưởng địa cầu"),
        new(4, "Avengers: Hồi kết", "Avengers Endgame"),
    };

    private static readonly List<NlpCatalogItem> Drinks = new()
    {
        new(11, "Bắp phô mai (M)"), new(12, "Bắp caramel (M)"), new(13, "Nước suối"), new(14, "Combo Solo"),
        new(15, "Combo bắp phô mai"), new(16, "Combo Couple"), new(17, "Nước có ga vị cola (M)"),
    };

    private static BookingIntent P(string text, DateTime? today = null) => NlpParser.Parse(text, today ?? Thursday, DayStart, Movies, Drinks);
    private static TimeSpan H(int h, int m = 0) => new(h, m, 0);

    // ------------------------------------------------------------------ 10 câu bắt buộc của SPEC 4.2
    [Fact]
    public void Nlp_01_SaturdayActionSubtitledAfter19_BestView_CheeseCombo()
    {
        var i = P("Tối thứ 7 này mình đi 3 người, muốn xem phim hành động phụ đề sau 19h, lấy 3 ghế đẹp nhất giữa phòng kèm 1 combo bắp phô mai");
        Assert.Equal(new[] { new DateTime(2026, 11, 14) }, i.Dates);
        Assert.Equal(H(19), i.TimeFrom);
        Assert.Equal(NlpParser.DayEnd, i.TimeTo);
        Assert.Equal(3, i.Quantity);
        Assert.Equal(Genre.Action, i.Genre);
        Assert.Equal(LanguageFormat.Subtitled, i.LanguageFormat);
        Assert.Equal(SeatPersona.BestViewAudio, i.Persona);
        var item = Assert.Single(i.Items);
        Assert.Equal(15, item.DrinkId);
        Assert.Equal(1, item.Quantity);
        Assert.False(i.SearchOnly);
    }

    [Fact]
    public void Nlp_01b_SaturdayIsTodayWhenTodayIsSaturday()
    {
        var saturday = new DateTime(2026, 11, 14);
        var i = P("Tối thứ 7 này mình đi 3 người, muốn xem phim hành động phụ đề sau 19h", saturday);
        Assert.Equal(new[] { saturday }, i.Dates);
    }

    [Fact]
    public void Nlp_02_SundayMorningAnimationDubbedForKid()
    {
        var i = P("2 vé hoạt hình lồng tiếng sáng chủ nhật cho bé");
        Assert.Equal(new[] { new DateTime(2026, 11, 15) }, i.Dates);
        Assert.Equal(H(8), i.TimeFrom);
        Assert.Equal(H(12), i.TimeTo);
        Assert.Equal(2, i.Quantity);
        Assert.Equal(Genre.Animation, i.Genre);
        Assert.Equal(LanguageFormat.Dubbed, i.LanguageFormat);
        Assert.True(i.ForChild);
    }

    [Fact]
    public void Nlp_03_CoupleTonightPrivate()
    {
        var i = P("Cặp đôi muốn ghế đôi riêng tư tối nay");
        Assert.Equal(new[] { Thursday }, i.Dates);
        Assert.Equal(H(18), i.TimeFrom);
        Assert.Equal(H(23, 59), i.TimeTo);
        Assert.Equal(2, i.Quantity);
        Assert.Equal(SeatPersona.PrivateCouple, i.Persona);
    }

    [Fact]
    public void Nlp_04_Tomorrow8pmHorror_SearchOnly()
    {
        var i = P("Mai 8 giờ tối có phim kinh dị nào không");
        Assert.Equal(new[] { Thursday.AddDays(1) }, i.Dates);
        Assert.Equal(H(19), i.TimeFrom);
        Assert.Equal(H(21), i.TimeTo);
        Assert.Equal(Genre.Horror, i.Genre);
        Assert.True(i.SearchOnly);
        Assert.Null(i.Quantity);
    }

    [Fact]
    public void Nlp_05_NearsightedNearScreenImax()
    {
        var i = P("Mình bị cận, muốn ngồi gần màn hình, xem IMAX");
        Assert.Equal(SeatPersona.NearScreen, i.Persona);
        Assert.Equal(AuditoriumFormat.IMAX, i.Format);
        Assert.Empty(i.Dates);
    }

    [Fact]
    public void Nlp_06_WeekendOriginal_SearchOnly()
    {
        var i = P("Cuối tuần này có suất bản gốc không");
        Assert.Equal(new[] { new DateTime(2026, 11, 14), new DateTime(2026, 11, 15) }, i.Dates);
        Assert.Equal(LanguageFormat.Original, i.LanguageFormat);
        Assert.True(i.SearchOnly);
    }

    [Fact]
    public void Nlp_07_EarliestFourTicketsNearAisle()
    {
        var i = P("Đặt 4 vé suất gần nhất, ghế gần lối đi");
        Assert.True(i.Earliest);
        Assert.Equal(4, i.Quantity);
        Assert.Equal(SeatPersona.NearAisle, i.Persona);
        Assert.False(i.SearchOnly);
    }

    [Fact]
    public void Nlp_08_AddTwoWaters_FollowUp()
    {
        var i = P("Thêm 2 nước suối");
        Assert.Equal(AgentCommand.AddItems, i.Command);
        var item = Assert.Single(i.Items);
        Assert.Equal(13, item.DrinkId);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void Nlp_09_SwitchToNextShowtime()
    {
        var i = P("Đổi sang suất sau đó");
        Assert.Equal(AgentCommand.NextShowtime, i.Command);
        Assert.Null(i.TimeFrom); // "sang" không phải "sáng"
    }

    [Fact]
    public void Nlp_10_StartOver_Reset()
    {
        Assert.Equal(AgentCommand.Reset, P("Thôi, làm lại").Command);
        Assert.Equal(AgentCommand.Reset, P("thoi").Command);
    }

    // ------------------------------------------------------------------ các câu thêm
    [Fact]
    public void Nlp_11_DayAfterTomorrowImax_AccentedMot()
    {
        var i = P("Mốt có suất IMAX không");
        Assert.Equal(new[] { Thursday.AddDays(2) }, i.Dates);
        Assert.Equal(AuditoriumFormat.IMAX, i.Format);
        Assert.True(i.SearchOnly);
        // "một" (số 1) không phải "mốt"
        Assert.Empty(P("Một vé xem phim hành động").Dates);
        Assert.Equal(1, P("Một vé xem phim hành động").Quantity);
    }

    [Fact]
    public void Nlp_12_DateDdMmAfternoon()
    {
        var i = P("2 vé ngày 20/11 buổi chiều");
        Assert.Equal(new[] { new DateTime(2026, 11, 20) }, i.Dates);
        Assert.Equal(H(12), i.TimeFrom);
        Assert.Equal(H(18), i.TimeTo);
        Assert.Equal(2, i.Quantity);
        // Ngày đã qua trong năm thì hiểu là năm sau
        Assert.Equal(new DateTime(2027, 1, 5), P("ngày 05/01 2 vé").Dates.Single());
    }

    [Fact]
    public void Nlp_13_MondayNextWeek()
    {
        var i = P("Thứ 2 tuần sau 4 người");
        Assert.Equal(new[] { new DateTime(2026, 11, 16) }, i.Dates);
        Assert.Equal(4, i.Quantity);
        Assert.Equal(new[] { new DateTime(2026, 11, 13) }, P("thứ sáu 2 vé").Dates);
    }

    [Fact]
    public void Nlp_14_MovieTitleWithoutAccents()
    {
        var i = P("minh muon xem lat mat 8 vong tay nang toi nay 2 ve");
        Assert.Equal(1, i.MovieId);
        Assert.Equal(new[] { Thursday }, i.Dates);
        Assert.Equal(H(18), i.TimeFrom);
        Assert.Equal(2, i.Quantity);
    }

    [Fact]
    public void Nlp_15_MovieTitleFuzzyMatch()
    {
        var i = P("Đặt 2 vé phim Avenger hoi ket");
        Assert.Equal(4, i.MovieId);
        Assert.Equal(1, P("2 vé Face off 8 chiều nay").MovieId);
    }

    [Fact]
    public void Nlp_16_ShortTitleOnlyAfterWordPhim()
    {
        var i = P("Mai có phim Mai không");
        Assert.Equal(2, i.MovieId);
        Assert.Equal(new[] { Thursday.AddDays(1) }, i.Dates);
        Assert.Null(P("Mai 2 vé hoạt hình").MovieId);
    }

    [Fact]
    public void Nlp_17_After7pmFridayNearAisle()
    {
        var i = P("Sau 7h tối thứ 6, 2 ghế gần lối đi");
        Assert.Equal(new[] { new DateTime(2026, 11, 13) }, i.Dates);
        Assert.Equal(H(19), i.TimeFrom);
        Assert.Equal(H(23, 59), i.TimeTo);
        Assert.Equal(2, i.Quantity);
        Assert.Equal(SeatPersona.NearAisle, i.Persona);
    }

    [Fact]
    public void Nlp_18_BeforeTenSundayMorningForKids()
    {
        var i = P("Trước 10h sáng chủ nhật cho bé xem hoạt hình");
        Assert.Equal(new[] { new DateTime(2026, 11, 15) }, i.Dates);
        Assert.Equal(H(8), i.TimeFrom);
        Assert.Equal(H(10), i.TimeTo);
        Assert.Equal(Genre.Animation, i.Genre);
        Assert.True(i.ForChild);
    }

    [Fact]
    public void Nlp_19_TodayHorrorDubbed3D_SearchOnly()
    {
        var i = P("Hôm nay có phim kinh dị lồng tiếng 3D nào không");
        Assert.Equal(new[] { Thursday }, i.Dates);
        Assert.Equal(Genre.Horror, i.Genre);
        Assert.Equal(LanguageFormat.Dubbed, i.LanguageFormat);
        Assert.Equal(AuditoriumFormat.F3D, i.Format);
        Assert.True(i.SearchOnly);
    }

    [Fact]
    public void Nlp_20_GenericComboAndPopcorn()
    {
        var i = P("Cho mình 1 combo và 2 bắp caramel");
        Assert.Equal(2, i.Items.Count);
        Assert.Contains(i.Items, x => x.DrinkId == null && x.Query == "combo" && x.Quantity == 1);
        Assert.Contains(i.Items, x => x.DrinkId == 12 && x.Quantity == 2);
        Assert.Null(i.Quantity); // số lượng món không phải số vé
    }

    [Fact]
    public void Nlp_21_ComedyWeekendFivePeople()
    {
        var i = P("Xem phim hài vào cuối tuần, đi 5 người");
        Assert.Equal(Genre.Comedy, i.Genre);
        Assert.Equal(2, i.Dates.Count);
        Assert.Equal(5, i.Quantity);
    }

    [Fact]
    public void Nlp_22_AloneSciFi4dxAround2130()
    {
        var i = P("Một mình xem phim viễn tưởng 4DX lúc 21:30");
        Assert.Equal(1, i.Quantity);
        Assert.Equal(Genre.SciFi, i.Genre);
        Assert.Equal(AuditoriumFormat.F4DX, i.Format);
        Assert.Equal(H(20, 30), i.TimeFrom);
        Assert.Equal(H(22, 30), i.TimeTo);
    }

    [Fact]
    public void Nlp_23_ThreeTicketsTomorrowAt9_NoSuffixIsMorning()
    {
        var i = P("Ngày mai 3 vé khoảng 9h");
        Assert.Equal(new[] { Thursday.AddDays(1) }, i.Dates);
        Assert.Equal(H(8), i.TimeFrom);
        Assert.Equal(H(10), i.TimeTo);
        Assert.Equal(3, i.Quantity);
    }

    [Fact]
    public void Nlp_24_Normalize_RemovesAccentsAndPunctuation()
    {
        Assert.Equal("dat ve toi nay", NlpParser.Normalize("Đặt vé, TỐI NAY!"));
        Assert.Equal("20/11 19:30", NlpParser.Normalize("20/11 19:30"));
        Assert.True(NlpParser.Similarity("avenger hoi ket", "avengers hoi ket") >= 0.9);
    }
}
