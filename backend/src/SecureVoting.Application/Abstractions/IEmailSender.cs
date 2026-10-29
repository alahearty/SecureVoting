namespace SecureVoting.Application.Abstractions;

public interface IEmailSender
{
    Task<bool> SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
