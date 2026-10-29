using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Elections;

public sealed class ElectionAdminService(
    AppDbContext db,
    AuditWriter auditWriter,
    TimeProvider timeProvider,
    DepartmentAccessService access)
{
    public async Task<Guid> CreateAsync(
        CreateElectionRequest request,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken)
            ?? throw new ValidationException("The selected department was not found.");
        await access.EnsureCanManageDepartmentAsync(actorUserId, department.Id, cancellationToken);
        if (!department.IsActive)
        {
            throw new ConflictException("The selected department is inactive.");
        }
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var election = new Election
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            AcademicSession = request.AcademicSession.Trim(),
            DepartmentId = department.Id,
            Department = department.Name,
            Status = ElectionStatus.Draft,
            NominationOpensAtUtc = request.NominationOpensAt.UtcDateTime,
            NominationClosesAtUtc = request.NominationClosesAt.UtcDateTime,
            OpensAtUtc = request.OpensAt.UtcDateTime,
            ClosesAtUtc = request.ClosesAt.UtcDateTime,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Rule = new ElectionRule
            {
                TieHandling = request.TieHandling,
                AllowAbstention = request.AllowAbstention,
                NominationRequiresApproval = request.NominationRequiresApproval
            }
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Elections.Add(election);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "ElectionCreated",
            nameof(Election),
            election.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { title = election.Title, session = election.AcademicSession, departmentId = department.Id },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return election.Id;
    }

    public async Task UpdateAsync(
        Guid electionId,
        UpdateElectionRequest request,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await access.EnsureCanManageElectionAsync(actorUserId, electionId, cancellationToken);
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken)
            ?? throw new ValidationException("The selected department was not found.");
        await access.EnsureCanManageDepartmentAsync(actorUserId, department.Id, cancellationToken);
        if (!department.IsActive)
        {
            throw new ConflictException("The selected department is inactive.");
        }
        var election = await db.Elections
            .Include(e => e.Rule)
            .SingleOrDefaultAsync(e => e.Id == electionId, cancellationToken)
            ?? throw new NotFoundException("Election was not found.");
        if (election.Status != ElectionStatus.Draft)
        {
            throw new ConflictException("Only draft elections can be edited.");
        }

        election.Title = request.Title.Trim();
        election.Description = request.Description.Trim();
        election.AcademicSession = request.AcademicSession.Trim();
        election.DepartmentId = department.Id;
        election.Department = department.Name;
        election.NominationOpensAtUtc = request.NominationOpensAt.UtcDateTime;
        election.NominationClosesAtUtc = request.NominationClosesAt.UtcDateTime;
        election.OpensAtUtc = request.OpensAt.UtcDateTime;
        election.ClosesAtUtc = request.ClosesAt.UtcDateTime;
        election.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        election.Rule.TieHandling = request.TieHandling;
        election.Rule.AllowAbstention = request.AllowAbstention;
        election.Rule.NominationRequiresApproval = request.NominationRequiresApproval;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                "ElectionUpdated",
                nameof(Election),
                election.Id.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                correlationId,
                new { title = election.Title },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("The election was changed by another session. Reload and try again.");
        }
    }

    public async Task<Guid> AddPositionAsync(
        Guid electionId,
        CreatePositionRequest request,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidatePosition(request);
        await access.EnsureCanManageElectionAsync(actorUserId, electionId, cancellationToken);
        var election = await db.Elections.SingleOrDefaultAsync(e => e.Id == electionId, cancellationToken)
            ?? throw new NotFoundException("Election was not found.");
        if (election.Status != ElectionStatus.Draft)
        {
            throw new ConflictException("Positions can only be changed while the election is a draft.");
        }

        if (await db.ElectionPositions.AnyAsync(
                p => p.ElectionId == electionId && p.Title.ToLower() == request.Title.Trim().ToLower(),
                cancellationToken))
        {
            throw new ConflictException("A position with this title already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var position = new ElectionPosition
        {
            ElectionId = electionId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            DisplayOrder = request.DisplayOrder,
            SeatCount = request.SeatCount,
            MaxSelections = request.MaxSelections,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ElectionPositions.Add(position);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "ElectionPositionCreated",
            nameof(ElectionPosition),
            position.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { electionId, title = position.Title },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return position.Id;
    }

    public async Task UpdatePositionAsync(
        Guid electionId,
        Guid positionId,
        UpdatePositionRequest request,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidatePosition(request);
        await access.EnsureCanManageElectionAsync(actorUserId, electionId, cancellationToken);
        var election = await db.Elections.SingleOrDefaultAsync(e => e.Id == electionId, cancellationToken)
            ?? throw new NotFoundException("Election was not found.");
        if (election.Status != ElectionStatus.Draft)
        {
            throw new ConflictException("Positions can only be changed while the election is a draft.");
        }

        var position = await db.ElectionPositions
            .SingleOrDefaultAsync(p => p.Id == positionId && p.ElectionId == electionId, cancellationToken)
            ?? throw new NotFoundException("Position was not found.");
        position.Title = request.Title.Trim();
        position.Description = request.Description.Trim();
        position.DisplayOrder = request.DisplayOrder;
        position.SeatCount = request.SeatCount;
        position.MaxSelections = request.MaxSelections;
        position.IsActive = request.IsActive;
        position.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "ElectionPositionUpdated",
            nameof(ElectionPosition),
            position.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { electionId, title = position.Title, position.IsActive },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ChangeStatusAsync(
        Guid electionId,
        string action,
        ElectionStatusRequest request,
        Guid? actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        await access.EnsureCanManageElectionAsync(actorUserId!.Value, electionId, cancellationToken);
        var election = await db.Elections.SingleOrDefaultAsync(e => e.Id == electionId, cancellationToken)
            ?? throw new NotFoundException("Election was not found.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var from = election.Status;
        ElectionStatus target;
        var reason = request.Reason.Trim();

        switch (action.Trim().ToLowerInvariant())
        {
            case "schedule":
                target = ElectionStatus.Scheduled;
                await EnsureCanScheduleAsync(election, cancellationToken);
                break;
            case "open":
                target = ElectionStatus.Open;
                await EnsureCanOpenAsync(election, now, cancellationToken);
                break;
            case "close":
                target = ElectionStatus.Closed;
                if (from != ElectionStatus.Open)
                {
                    throw new ConflictException("Only an open election can be closed.");
                }

                if (reason.Length < 5)
                {
                    throw new ValidationException("A closure reason is required.");
                }

                election.ClosureReason = reason;
                break;
            case "review":
                throw new ValidationException(
                    "Result review must use the result-review endpoint so a provisional snapshot is created atomically.");
            case "suspend":
                if (from is not (ElectionStatus.Scheduled or ElectionStatus.Open))
                {
                    throw new ConflictException("Only a scheduled or open election can be suspended.");
                }

                if (reason.Length < 5)
                {
                    throw new ValidationException("A suspension reason is required.");
                }

                target = ElectionStatus.Suspended;
                election.SuspendedFromStatus = from;
                election.SuspensionReason = reason;
                break;
            case "resume":
                if (from != ElectionStatus.Suspended)
                {
                    throw new ConflictException("Only a suspended election can be resumed.");
                }

                target = now < election.OpensAtUtc
                    ? ElectionStatus.Scheduled
                    : now < election.ClosesAtUtc
                        ? ElectionStatus.Open
                        : ElectionStatus.Closed;
                election.SuspensionReason = null;
                break;
            case "cancel":
                if (from is not (ElectionStatus.Draft or ElectionStatus.Scheduled or ElectionStatus.Open
                    or ElectionStatus.Suspended))
                {
                    throw new ConflictException("This election can no longer be cancelled through this workflow.");
                }

                if (reason.Length < 5)
                {
                    throw new ValidationException("A cancellation reason is required.");
                }

                target = ElectionStatus.Cancelled;
                election.CancellationReason = reason;
                break;
            default:
                throw new ValidationException("Unknown election action.");
        }

        ElectionStateMachine.EnsureTransition(from, target);
        election.Status = target;
        election.UpdatedAtUtc = now;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                $"Election{target}",
                nameof(Election),
                election.Id.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                correlationId,
                new { action, from = from.ToString(), to = target.ToString(), reason },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("The election changed in another session. Reload and try again.");
        }
    }

    public async Task CloseExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expired = await db.Elections
            .Where(e => e.Status == ElectionStatus.Open && e.ClosesAtUtc <= now)
            .ToListAsync(cancellationToken);
        foreach (var election in expired)
        {
            election.Status = ElectionStatus.Closed;
            election.ClosureReason = "Closed automatically at the scheduled closing time.";
            election.UpdatedAtUtc = now;
        }

        if (expired.Count == 0)
        {
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var election in expired)
        {
            await auditWriter.AppendAsync(
                db,
                "ElectionClosed",
                nameof(Election),
                election.Id.ToString(),
                AuditOutcome.Success,
                null,
                "System scheduler",
                null,
                new { reason = "Scheduled closing time" },
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureCanScheduleAsync(Election election, CancellationToken cancellationToken)
    {
        if (election.Status != ElectionStatus.Draft)
        {
            throw new ConflictException("Only a draft election can be scheduled.");
        }

        if (!await db.ElectionPositions.AnyAsync(p => p.ElectionId == election.Id && p.IsActive, cancellationToken))
        {
            throw new ConflictException("At least one active position is required before scheduling.");
        }

        if (!await db.VoterEligibility.AnyAsync(v => v.ElectionId == election.Id && v.IsEligible, cancellationToken))
        {
            throw new ConflictException("At least one eligible voter is required before scheduling.");
        }
    }

    private async Task EnsureCanOpenAsync(Election election, DateTime now, CancellationToken cancellationToken)
    {
        if (election.Status is not (ElectionStatus.Scheduled or ElectionStatus.Suspended))
        {
            throw new ConflictException("Only a scheduled or suspended election can be opened.");
        }

        if (now < election.OpensAtUtc || now >= election.ClosesAtUtc)
        {
            throw new ConflictException("The election can only be opened inside its authorised voting window.");
        }

        var activePositions = await db.ElectionPositions
            .Where(p => p.ElectionId == election.Id && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        if (activePositions.Count == 0)
        {
            throw new ConflictException("At least one active position is required.");
        }

        if (!await db.VoterEligibility.AnyAsync(v => v.ElectionId == election.Id && v.IsEligible, cancellationToken))
        {
            throw new ConflictException("At least one eligible voter is required.");
        }

        var positionsWithoutApprovedCandidate = await db.ElectionPositions
            .Where(p => p.ElectionId == election.Id && p.IsActive)
            .Where(p => !db.Candidates.Any(c => c.ElectionId == election.Id && c.PositionId == p.Id &&
                                                 c.Status == CandidateStatus.Approved))
            .Select(p => p.Title)
            .ToListAsync(cancellationToken);
        if (positionsWithoutApprovedCandidate.Count > 0)
        {
            throw new ConflictException(
                $"Every active position needs an approved candidate: {string.Join(", ", positionsWithoutApprovedCandidate)}.");
        }
    }

    private static void ValidateRequest(CreateElectionRequest request)
    {
        if (request.NominationClosesAt <= request.NominationOpensAt)
        {
            throw new ValidationException("Nomination closing time must be after opening time.");
        }

        if (request.OpensAt < request.NominationClosesAt)
        {
            throw new ValidationException("Voting cannot open before nominations close.");
        }

        if (request.ClosesAt <= request.OpensAt)
        {
            throw new ValidationException("Voting closing time must be after opening time.");
        }
    }

    private static void ValidatePosition(CreatePositionRequest request)
    {
        if (request.SeatCount < 1 || request.MaxSelections < 1 || request.MaxSelections > request.SeatCount)
        {
            throw new ValidationException("Seat count and maximum selections are invalid.");
        }
    }
}
