using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using Situ.Application.Common.Abstractions;

namespace Situ.Infrastructure.Mail;

public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = body.Contains("<html", StringComparison.OrdinalIgnoreCase),
        };
        message.To.Add(to);
        if (message.IsBodyHtml)
        {
            var plain = Regex.Replace(body, "<[^>]+>", " ");
            plain = WebUtility.HtmlDecode(Regex.Replace(plain, @"\s+", " ")).Trim();
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plain, Encoding.UTF8, MediaTypeNames.Text.Plain));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(body, Encoding.UTF8, MediaTypeNames.Text.Html));
        }

#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(options.Username)
                ? null
                : new NetworkCredential(options.Username, options.Password),
        };
#pragma warning restore SYSLIB0014

        await client.SendMailAsync(message, cancellationToken);
    }
}
