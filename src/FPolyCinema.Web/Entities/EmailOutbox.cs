namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng EmailOutbox: thư đã gửi hoặc nằm trong hộp thư thử.</summary>
public class EmailOutbox
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
