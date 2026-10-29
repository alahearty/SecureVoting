using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Api.Controllers;

[Route("api/audit")]
[Authorize(Policy = "ViewAudit")]
public sealed class AuditController(
    AppDbContext db,
    AuditVerifier verifier,
    DepartmentAccessService access) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> List(
        [FromQuery] string? eventType,
        [FromQuery] string? actor,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.AuditLogs.AsNoTracking();
        if (!User.IsInRole(RoleNames.SuperAdministrator))
        {
            var departments = await access.GetAllAssignedDepartmentIdsAsync(CurrentUserId, cancellationToken);
            if (departments.Count == 0)
            {
                return Ok(Array.Empty<AuditLogDto>());
            }

            query = query.Where(a => a.DepartmentId != null && departments.Contains(a.DepartmentId.Value));
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(a => a.EventType.Contains(eventType));
        }

        if (!string.IsNullOrWhiteSpace(actor))
        {
            query = query.Where(a => a.ActorDisplay.Contains(actor));
        }

        if (from is not null)
        {
            query = query.Where(a => a.OccurredAtUtc >= from.Value.UtcDateTime);
        }

        if (to is not null)
        {
            query = query.Where(a => a.OccurredAtUtc <= to.Value.UtcDateTime);
        }

        return Ok(await query
            .OrderByDescending(a => a.Sequence)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Sequence,
                a.EventId,
                a.OccurredAtUtc,
                a.ActorUserId == null ? null : a.ActorUserId.ToString(),
                a.ActorDisplay,
                a.EventType,
                a.EntityType,
                a.EntityId,
                a.Outcome,
                a.CorrelationId,
                a.DetailsJson,
                a.Hash))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("verify")]
    public async Task<ActionResult<AuditVerificationDto>> Verify(CancellationToken cancellationToken)
    {
        return Ok(await verifier.VerifyAsync(cancellationToken));
    }
}
