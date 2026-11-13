using System.Net;
using System.Net.Mail;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Web.Services;

/// <summary>Gửi email (SPEC 2.5). Nội dung là văn bản thuần, xuống dòng bằng \n.</summary>
public interface IEmailSender
{
    void Send(string toEmail, string subject, string body);
}

/// <summary>
/// Không cấu hình Email:SmtpHost (mặc định khi chạy Development): ghi thư vào bảng EmailOutbox, SentAt để trống,
/// xem ở hộp thư thử /dev/mailbox (UI D2).
/// </summary>
public sealed class OutboxEmailSender : IEmailSender
{
    private readonly IClock _clock;

    public OutboxEmailSender(IClock clock) => _clock = clock;

    public void Send(string toEmail, string subject, string body)
    {
        EmailOutboxDAL.Insert(new EmailOutbox { ToEmail = toEmail, Subject = subject, Body = body, SentAt = null, CreatedAt = _clock.Now });
    }
}

/// <summary>Có cấu hình Email:SmtpHost: gửi bằng SMTP thật, rồi lưu bản sao vào EmailOutbox với SentAt là lúc gửi.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly IClock _clock;

    public SmtpEmailSender(IOptions<EmailOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public void Send(string toEmail, string subject, string body)
    {
        using var message = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(_options.From) ? new MailAddress(_options.SmtpUser) : ParseAddress(_options.From),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8,
        };
        message.To.Add(toEmail);
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.SmtpPort != 25,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
            client.Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword);
        client.Send(message);
        EmailOutboxDAL.Insert(new EmailOutbox { ToEmail = toEmail, Subject = subject, Body = body, SentAt = _clock.Now, CreatedAt = _clock.Now });
    }

    /// <summary>Đọc địa chỉ dạng "Tên &lt;email&gt;" hoặc chỉ email.</summary>
    public static MailAddress ParseAddress(string value)
    {
        var lt = value.IndexOf('<');
        var gt = value.IndexOf('>');
        if (lt >= 0 && gt > lt) return new MailAddress(value[(lt + 1)..gt].Trim(), value[..lt].Trim());
        return new MailAddress(value.Trim());
    }
}
