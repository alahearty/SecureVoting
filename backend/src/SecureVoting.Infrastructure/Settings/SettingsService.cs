using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Settings;

public sealed class SettingsService(AppDbContext db, AuditWriter auditWriter, TimeProvider timeProvider)
{
    private static readonly HashSet<string> EditableKeys = new(StringComparer.Ordinal)
    {
        "ApplicationName",
        "InstitutionName",
        "DepartmentName"
    };

    public async Task<IReadOnlyList<SystemSettingDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await db.SystemSettings.AsNoTracking()
            .OrderBy(s => s.Key)
            .Select(s => new SystemSettingDto(s.Key, s.Value, s.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Guid actorUserId,
        string actorDisplay,
        UpdateSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!EditableKeys.Contains(request.Key))
        {
            throw new ValidationException("This setting cannot be changed here.");
        }

        var setting = await db.SystemSettings
            .SingleOrDefaultAsync(s => s.Key == request.Key, cancellationToken)
            ?? throw new NotFoundException("Setting was not found.");
        setting.Value = request.Value.Trim();
        setting.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "SystemSettingUpdated",
            nameof(SystemSetting),
            request.Key,
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            null,
            new { key = request.Key },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
