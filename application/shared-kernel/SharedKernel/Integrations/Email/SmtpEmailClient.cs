using System.Net;
using System.Net.Mail;

namespace SharedKernel.Integrations.Email;

/// <summary>
///     Email client that sends emails via SMTP.
///     Used with Scaleway Transactional Email (TEM) or any SMTP relay.
///     Reads configuration from environment variables: SMTP_HOST, SMTP_PORT, SMTP_USERNAME, SMTP_PASSWORD,
///     SENDER_EMAIL_ADDRESS.
/// </summary>
public sealed class SmtpEmailClient(ILogger<SmtpEmailClient> logger) : IEmailClient
{
    private readonly string _senderEmail = Environment.GetEnvironmentVariable("SENDER_EMAIL_ADDRESS")
                                           ?? throw new InvalidOperationException("SENDER_EMAIL_ADDRESS environment variable is not configured.");

    private readonly string _smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.tem.scw.cloud";
    private readonly string? _smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
    private readonly int _smtpPort = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "465");
    private readonly string? _smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME");

    public async Task SendAsync(string recipient, string subject, string htmlContent, CancellationToken cancellationToken)
    {
        var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = true,
            Credentials = _smtpUsername is not null ? new NetworkCredential(_smtpUsername, _smtpPassword) : null
        };
        using (smtpClient)
        {
            var mailMessage = new MailMessage(_senderEmail, recipient, subject, htmlContent) { IsBodyHtml = true };

            logger.LogInformation("Sending email to {Recipient} with subject {Subject}", recipient, subject);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
        }
    }
}
