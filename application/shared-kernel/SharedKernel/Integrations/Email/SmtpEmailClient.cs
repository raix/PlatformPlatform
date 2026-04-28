using System.Net;
using System.Net.Mail;

namespace SharedKernel.Integrations.Email;

/// <summary>
/// Email client that sends emails via SMTP.
/// Used with Scaleway Transactional Email (TEM) or any SMTP relay.
/// Reads configuration from environment variables: SMTP_HOST, SMTP_PORT, SMTP_USERNAME, SMTP_PASSWORD, SENDER_EMAIL_ADDRESS.
/// </summary>
public sealed class SmtpEmailClient(ILogger<SmtpEmailClient> logger) : IEmailClient
{
    public async Task SendAsync(string recipient, string subject, string htmlContent, CancellationToken cancellationToken)
    {
        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.tem.scw.cloud";
        var smtpPort = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "465");
        var smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var senderEmail = Environment.GetEnvironmentVariable("SENDER_EMAIL_ADDRESS")
            ?? throw new InvalidOperationException("SENDER_EMAIL_ADDRESS environment variable is not configured.");

        using var smtpClient = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = true,
            Credentials = smtpUsername is not null ? new NetworkCredential(smtpUsername, smtpPassword) : null
        };

        var mailMessage = new MailMessage(senderEmail, recipient, subject, htmlContent) { IsBodyHtml = true };

        logger.LogInformation("Sending email to {Recipient} with subject {Subject}", recipient, subject);

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }
}
