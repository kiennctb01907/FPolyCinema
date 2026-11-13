using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Services;

/// <summary>Dữ liệu cho các trang công cụ (chỉ khi Demo:EnableTools): style guide (UI D1), hộp thư thử (UI D2).</summary>
public class DevToolsService
{
    public List<Movie> Movies(int limit) => MovieDAL.GetActive(limit);

    public PagedResult<DrinkRow> Drinks(int pageSize) => new(DrinkDAL.Search(null, null, null, 0, pageSize, out int total), 1, pageSize, total);

    public List<EmailOutbox> Mailbox(int limit) => EmailOutboxDAL.GetLatest(limit);

    public int MailCount() => EmailOutboxDAL.Count();

    public EmailOutbox? Mail(int id) => EmailOutboxDAL.GetById(id);

    public int ClearMailbox() => EmailOutboxDAL.DeleteAll();

    /// <summary>Dòng phụ của thẻ phim: "Hành động · 2 giờ 05 phút".</summary>
    public static string MovieSubline(Movie m)
    {
        var genre = CsvList.Split(m.Genres).Select(g => StatusDisplay.Of(StatusDisplay.Genres, g).Label).FirstOrDefault();
        return string.Join(" · ", new[] { genre, Fmt.Duration(m.DurationMinutes) }.Where(x => !string.IsNullOrEmpty(x)));
    }
}
