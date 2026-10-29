using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SecureVoting.Application.Abstractions;
using SecureVoting.Infrastructure.Configuration;

namespace SecureVoting.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            logger.LogInformation("Email delivery is disabled; notification was not sent.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.FromAddress))
        {
            logger.LogWarning("Email delivery is enabled but SMTP host/from address is not configured.");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config.FromName, config.FromAddress));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                config.Host,
                config.Port,
                config.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(config.UserName))
            {
                await client.AuthenticateAsync(config.UserName, config.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Email delivery failed.");
            return false;
        }
    }
}
