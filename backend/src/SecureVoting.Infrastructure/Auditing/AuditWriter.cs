using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Auditing;

public sealed class AuditWriter(TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task AppendAsync(
        AppDbContext db,
        string eventType,
        string entityType,
        string? entityId,
        AuditOutcome outcome,
        Guid? actorUserId,
        string actorDisplay,
        string? correlationId,
        object? details,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Audit entries must be written inside the transaction they describe.");
        }

        var state = await db.AuditChainStates
            .FromSqlRaw("SELECT \"Id\", \"LastSequence\", \"LastHash\", \"UpdatedAtUtc\" FROM \"AuditChainState\" WHERE \"Id\" = 1 FOR UPDATE")
            .SingleAsync(cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        now = new DateTime(now.Ticks - (now.Ticks % 10), DateTimeKind.Utc);
        var detailsJson = CanonicalJson.Normalize(
            details is null ? "{}" : JsonSerializer.Serialize(details, JsonOptions));
        var departmentId = await ResolveDepartmentIdAsync(
            db, entityType, entityId, detailsJson, cancellationToken);
        var sequence = state.LastSequence + 1;
        var entry = new AuditLog
        {
            Sequence = sequence,
            EventId = Guid.NewGuid(),
            OccurredAtUtc = now,
            ActorUserId = actorUserId,
            ActorDisplay = actorDisplay,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            Outcome = outcome,
            CorrelationId = correlationId,
            DepartmentId = departmentId,
            DetailsJson = detailsJson,
            PreviousHash = state.LastHash,
            Hash = string.Empty,
            CreatedAtUtc = now
        };
        entry.Hash = AuditHash.Compute(entry);

        state.LastSequence = sequence;
        state.LastHash = entry.Hash;
        state.UpdatedAtUtc = now;
        db.AuditLogs.Add(entry);
    }

    private static async Task<Guid?> ResolveDepartmentIdAsync(
        AppDbContext db,
        string entityType,
        string? entityId,
        string detailsJson,
        CancellationToken cancellationToken)
    {
        if (string.Equals(entityType, nameof(Department), StringComparison.Ordinal) &&
            Guid.TryParse(entityId, out var departmentId))
        {
            return await db.Departments.AsNoTracking()
                .Where(d => d.Id == departmentId)
                .Select(d => (Guid?)d.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (string.Equals(entityType, nameof(Election), StringComparison.Ordinal) &&
            Guid.TryParse(entityId, out var electionEntityId))
        {
            return await db.Elections.AsNoTracking()
                .Where(e => e.Id == electionEntityId)
                .Select(e => (Guid?)e.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        try
        {
            using var document = JsonDocument.Parse(detailsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("electionId", out var electionIdElement) &&
                Guid.TryParse(electionIdElement.GetString(), out var electionId))
            {
                return await db.Elections.AsNoTracking()
                    .Where(e => e.Id == electionId)
                    .Select(e => (Guid?)e.DepartmentId)
                    .SingleOrDefaultAsync(cancellationToken);
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
