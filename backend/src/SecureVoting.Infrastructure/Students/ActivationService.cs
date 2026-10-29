using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Identity;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Students;

public sealed class ActivationService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    AuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<ActivationService> logger)
{
    public async Task<ApplicationUser> ActivateAsync(
        string matricNumber,
        string token,
        string password,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedMatric = MatriculationNumber.Normalize(matricNumber);
        var tokenHash = TokenGenerator.Sha256Hex(token.Trim());
        var student = await db.Students
            .SingleOrDefaultAsync(s => s.MatricNumberNormalized == normalizedMatric, cancellationToken)
            ?? throw new ValidationException("The activation details are invalid or expired.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activation = await db.StudentActivationTokens
            .Where(t => t.StudentId == student.Id &&
                        t.TokenHash == tokenHash &&
                        t.UsedAtUtc == null &&
                        t.ExpiresAtUtc > now)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ValidationException("The activation details are invalid or expired.");

        if (!student.IsActive)
        {
            throw new ConflictException("This student account is suspended.");
        }

        var user = student.UserId is not null
            ? await userManager.FindByIdAsync(student.UserId.Value.ToString())
            : await userManager.FindByNameAsync(normalizedMatric);
        user ??= await userManager.FindByEmailAsync(student.Email);
        if (user is null || !user.IsActive)
        {
            throw new ValidationException("The activation details are invalid or expired.");
        }

        if (await userManager.HasPasswordAsync(user))
        {
            throw new ConflictException("This account has already been activated. Sign in or use password recovery.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var consumed = await db.StudentActivationTokens
                .Where(t => t.Id == activation.Id && t.UsedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAtUtc, now), cancellationToken);
            if (consumed != 1)
            {
                throw new ValidationException("The activation details are invalid or expired.");
            }

            var result = await userManager.AddPasswordAsync(user, password);
            if (!result.Succeeded)
            {
                throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            user.EmailConfirmed = true;
            user.IsActive = true;
            user.DisplayName = student.FullName;
            await userManager.UpdateAsync(user);
            await userManager.UpdateSecurityStampAsync(user);

            activation.UsedAtUtc = now;
            student.UserId = user.Id;
            student.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);

            await auditWriter.AppendAsync(
                db,
                "AccountActivated",
                nameof(Student),
                student.Id.ToString(),
                AuditOutcome.Success,
                user.Id,
                user.DisplayName,
                correlationId,
                new { method = "One-time activation token" },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Student account activated.");
            return user;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(ex, "Student activation failed.");
            throw;
        }
    }
}
