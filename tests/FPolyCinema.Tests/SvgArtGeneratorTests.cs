using System.Xml.Linq;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho SvgArtGenerator (UI mục 7, phần Ảnh mẫu).</summary>
public class SvgArtGeneratorTests
{
    private static XElement Parse(string svg) => XElement.Parse(svg);

    [Fact]
    public void Poster_Is600x900_WithGradientTitleSubtitleAndAgeBadge()
    {
        var svg = SvgArtGenerator.Poster("Mùa Hè Trên Đồi Gió", new[] { Genre.Drama, Genre.Family }, 2026, AgeRating.T13);
        var root = Parse(svg);
        Assert.Equal("600", root.Attribute("width")!.Value);
        Assert.Equal("900", root.Attribute("height")!.Value);
        Assert.Contains("linearGradient", svg);
        // Tên phim chữ hoa, tự xuống dòng thành hai dòng
        Assert.Contains(">MÙA HÈ TRÊN</text>", svg);
        Assert.Contains(">ĐỒI GIÓ</text>", svg);
        Assert.Contains("Tâm lý • Gia đình • 2026", svg);
        Assert.Contains(">T13<", svg);
        Assert.Contains("font-weight=\"700\"", svg);
        Assert.Contains("<circle", svg); // họa tiết hình học
    }

    [Fact]
    public void Poster_GradientDependsOnGenre()
    {
        var action = SvgArtGenerator.Poster("Phim A", new[] { Genre.Action }, 2026, AgeRating.T16);
        var romance = SvgArtGenerator.Poster("Phim A", new[] { Genre.Romance }, 2026, AgeRating.T16);
        Assert.NotEqual(action, romance);
    }

    [Fact]
    public void Poster_LongTitleWrapsIntoSeveralLines()
    {
        var title = "Biên Niên Sử Vùng Đất Băng Giá Phương Bắc";
        var root = Parse(SvgArtGenerator.Poster(title, new[] { Genre.SciFi }, 2026, AgeRating.T16));
        XNamespace ns = "http://www.w3.org/2000/svg";
        var titleLines = root.Descendants(ns + "g").First(g => g.Attribute("font-weight")?.Value == "700").Elements(ns + "text").ToList();
        Assert.True(titleLines.Count >= 2);
        Assert.All(titleLines, l => Assert.True(l.Value.Length <= 22));
        Assert.Equal(title.ToUpperInvariant().Replace(" ", ""), string.Concat(titleLines.Select(l => l.Value)).Replace(" ", ""));
    }

    [Fact]
    public void Poster_TitleLinesFitWidthForAllSeedMovies()
    {
        XNamespace ns = "http://www.w3.org/2000/svg";
        foreach (var m in FPolyCinema.Web.Infrastructure.Seed.SeedCatalog.Movies)
        {
            var g = Parse(SvgArtGenerator.Poster(m.Title, m.Genres, 2026, m.AgeRating)).Descendants(ns + "g").First(x => x.Attribute("font-weight")?.Value == "700");
            int size = int.Parse(g.Attribute("font-size")!.Value);
            foreach (var line in g.Elements(ns + "text"))
                Assert.True(line.Value.Length * size * 0.68 <= 528, $"{m.Title}: dòng \"{line.Value}\" rộng quá vùng chữ");
        }
    }

    [Fact]
    public void Poster_EscapesXmlAndIsDeterministic()
    {
        var a = SvgArtGenerator.Poster("Tom & Jerry <3", new[] { Genre.Comedy }, 2026, AgeRating.P);
        var b = SvgArtGenerator.Poster("Tom & Jerry <3", new[] { Genre.Comedy }, 2026, AgeRating.P);
        Assert.Equal(a, b);
        Parse(a); // XML hợp lệ
        Assert.Contains("&amp;", a);
    }

    [Fact]
    public void AgeBadge_UsesDarkTextExceptC()
    {
        Assert.Contains("fill=\"#0B0F19\">P<", SvgArtGenerator.Poster("X", new[] { Genre.Family }, 2026, AgeRating.P));
        Assert.Contains("fill=\"#FFFFFF\">C<", SvgArtGenerator.Poster("X", new[] { Genre.Horror }, 2026, AgeRating.C));
    }

    [Theory]
    [InlineData(DrinkArtKind.Popcorn)]
    [InlineData(DrinkArtKind.Drink)]
    [InlineData(DrinkArtKind.Snack)]
    [InlineData(DrinkArtKind.Combo)]
    public void DrinkImage_Is600x600FlatShapeOnLightBackground(DrinkArtKind kind)
    {
        var svg = SvgArtGenerator.DrinkImage(kind, "Combo Couple");
        var root = Parse(svg);
        Assert.Equal("600", root.Attribute("width")!.Value);
        Assert.Equal("600", root.Attribute("height")!.Value);
        Assert.Matches("<(polygon|path) ", svg);
        // Nền màu nhạt phủ kín ảnh
        Assert.Matches("<rect width=\"600\" height=\"600\" fill=\"#(FEF3C7|E0F2FE|FFE4E6|EDE9FE)\"/>", svg);
    }

    [Fact]
    public void Art_NeverReferencesInternetImages()
    {
        var all = SvgArtGenerator.Poster("X", new[] { Genre.Action }, 2026, AgeRating.T18)
                  + string.Concat(Enum.GetValues<DrinkArtKind>().Select(k => SvgArtGenerator.DrinkImage(k, "x")));
        Assert.DoesNotContain("<image", all);
        Assert.DoesNotContain("href=\"http", all);
    }

    [Fact]
    public void SeedArt_FileNamesMatchDbPathsExactlyIncludingCase()
    {
        var root = Path.Combine(Path.GetTempPath(), "fpc-art-" + Guid.NewGuid().ToString("N"));
        try
        {
            var seeder = new DbSeeder(new FakeClock(new DateTime(2026, 11, 6, 9, 0, 0)), new CinemaRules(), root, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            seeder.WriteSeedArt();
            var dir = Path.Combine(root, "uploads", "seed");
            var names = Directory.GetFiles(dir).Select(f => Path.GetFileName(f)).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(FPolyCinema.Web.Infrastructure.Seed.SeedCatalog.Movies.Length + FPolyCinema.Web.Infrastructure.Seed.SeedCatalog.Drinks.Length, names.Count);
            for (int i = 0; i < FPolyCinema.Web.Infrastructure.Seed.SeedCatalog.Movies.Length; i++)
                Assert.Contains(DbSeeder.PosterPath(i).Split('/').Last(), names);
            for (int i = 0; i < FPolyCinema.Web.Infrastructure.Seed.SeedCatalog.Drinks.Length; i++)
                Assert.Contains(DbSeeder.DrinkImagePath(i).Split('/').Last(), names);
            Assert.All(names, n => Assert.Equal(n.ToLowerInvariant(), n));
            // Thư mục script CSDL đúng chữ hoa chữ thường như trong Program.cs
            Assert.Contains("Database", Directory.GetDirectories(RepoPaths.Web).Select(Path.GetFileName));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void WrapText_RespectsMaxCharsAndLines()
    {
        var lines = SvgArtGenerator.WrapText("một hai ba bốn năm sáu bảy tám chín mười", 8, 3);
        Assert.Equal(3, lines.Count);
        Assert.EndsWith("…", lines[^1]);
    }
}
