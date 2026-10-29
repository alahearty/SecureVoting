using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureVoting.Application.Abstractions;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Configuration;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Identity;

public sealed class PasswordResetService(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    AuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<PasswordResetService> logger)
{
    public async Task RequestAsync(
        string identifier,
        AppDbContext db,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(identifier);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var now = timeProvider.GetUtcNow();
        var link = $"{appOptions.Value.FrontendBaseUrl.TrimEnd('/')}/reset-password" +
                   $"?identifier={Uri.EscapeDataString(identifier.Trim())}&token={Uri.EscapeDataString(token)}";
        var body =
            "<p>A password reset was requested for your IAUE SecureVote account.</p>" +
            $"<p><a href=\"{link}\">Reset your password</a></p>" +
            $"<p>This link expires in {appOptions.Value.PasswordResetMinutes} minutes. If you did not request it, ignore this email.</p>";

        var sent = await emailSender.SendAsync(user.Email, "IAUE SecureVote password reset", body, cancellationToken);
        if (!sent)
        {
            logger.LogWarning("Password reset email could not be delivered; no token was logged.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "PasswordResetRequested",
            "User",
            user.Id.ToString(),
            sent ? AuditOutcome.Success : AuditOutcome.Failure,
            user.Id,
            user.DisplayName,
            correlationId,
            new { delivery = sent ? "submitted" : "unavailable" },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _ = now;
    }

    public async Task ResetAsync(
        string identifier,
        string token,
        string password,
        AppDbContext db,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(identifier);
        if (user is null || !user.IsActive)
        {
            throw new ValidationException("The reset link is invalid or expired.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
            {
                throw new ValidationException("The reset link is invalid or expired.");
            }

            await userManager.UpdateSecurityStampAsync(user);
            await auditWriter.AppendAsync(
                db,
                "PasswordResetCompleted",
                "User",
                user.Id.ToString(),
                AuditOutcome.Success,
                user.Id,
                user.DisplayName,
                correlationId,
                new { method = "Identity reset token" },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(ex, "Password reset failed.");
            throw;
        }
    }

    private async Task<ApplicationUser?> FindUserAsync(string identifier)
    {
        var value = identifier.Trim();
        var user = await userManager.FindByEmailAsync(value);
        if (user is not null)
        {
            return user;
        }

        if (MatriculationNumber.TryNormalize(value, out var normalized))
        {
            return await userManager.FindByNameAsync(normalized);
        }

        return null;
    }
}
