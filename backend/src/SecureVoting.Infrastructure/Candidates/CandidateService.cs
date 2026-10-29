using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Abstractions;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Identity;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Candidates;

public sealed class CandidateService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    AuditWriter auditWriter,
    TimeProvider timeProvider,
    DepartmentAccessService access)
{
    public async Task<CandidateProfileDto?> GetMyNominationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var student = await db.Students.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (student is null)
        {
            return null;
        }

        var candidate = await db.Candidates
            .AsNoTracking()
            .Include(c => c.Election)
                .ThenInclude(e => e.DepartmentRef)
            .Include(c => c.Position)
            .Where(c => c.StudentId == student.Id)
            .OrderByDescending(c => c.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return candidate is null ? null : Map(candidate);
    }

    public async Task<Guid> NominateAsync(
        Guid userId,
        CandidateNominationRequest request,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var student = await db.Students.SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken)
            ?? throw new ConflictException("A student profile is required to contest an election.");
        if (!student.IsActive)
        {
            throw new ForbiddenOperationException("Inactive students cannot contest an election.");
        }

        var election = await db.Elections
            .Include(e => e.Rule)
            .SingleOrDefaultAsync(e => e.Id == request.ElectionId, cancellationToken)
            ?? throw new NotFoundException("Election was not found.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (election.Status is not (ElectionStatus.Draft or ElectionStatus.Scheduled) ||
            now >= election.NominationClosesAtUtc || now >= election.OpensAtUtc)
        {
            throw new ConflictException("Nominations are not open for this election.");
        }

        if (!await db.VoterEligibility.AnyAsync(
                v => v.ElectionId == election.Id && v.StudentId == student.Id && v.IsEligible,
                cancellationToken))
        {
            throw new ForbiddenOperationException("Only eligible students in this election may contest.");
        }

        var position = await db.ElectionPositions
            .SingleOrDefaultAsync(p => p.Id == request.PositionId && p.ElectionId == election.Id && p.IsActive,
                cancellationToken)
            ?? throw new ValidationException("The selected position is not valid for this election.");
        if (await db.Candidates.AnyAsync(c => c.ElectionId == election.Id && c.StudentId == student.Id,
                cancellationToken))
        {
            throw new ConflictException("You already have a nomination in this election.");
        }

        var candidate = new Candidate
        {
            ElectionId = election.Id,
            PositionId = position.Id,
            StudentId = student.Id,
            DisplayName = student.FullName,
            Manifesto = request.Manifesto.Trim(),
            Status = election.Rule.NominationRequiresApproval ? CandidateStatus.Pending : CandidateStatus.Approved,
            SubmittedAtUtc = now,
            ReviewedAtUtc = election.Rule.NominationRequiresApproval ? null : now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Candidates.Add(candidate);
            await db.SaveChangesAsync(cancellationToken);
            if (!election.Rule.NominationRequiresApproval)
            {
                var user = await userManager.FindByIdAsync(userId.ToString())
                    ?? throw new NotFoundException("Candidate account was not found.");
                if (!await userManager.IsInRoleAsync(user, RoleNames.Candidate))
                {
                    await userManager.AddToRoleAsync(user, RoleNames.Candidate);
                }

                await userManager.UpdateSecurityStampAsync(user);
            }

            await auditWriter.AppendAsync(
                db,
                "CandidateNominated",
                nameof(Candidate),
                candidate.Id.ToString(),
                AuditOutcome.Success,
                userId,
                student.FullName,
                correlationId,
                new { electionId = election.Id, positionId = position.Id, autoApproved = !election.Rule.NominationRequiresApproval },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return candidate.Id;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateMyProfileAsync(
        Guid candidateId,
        Guid userId,
        UpdateCandidateProfileRequest request,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var (candidate, election) = await LoadOwnedCandidateAsync(candidateId, userId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (election.Status is not (ElectionStatus.Draft or ElectionStatus.Scheduled))
        {
            throw new ConflictException("Approved candidate details are frozen once voting opens.");
        }

        if (candidate.Status is not (CandidateStatus.Pending or CandidateStatus.Rejected))
        {
            throw new ConflictException("Only a pending or rejected nomination can be edited.");
        }

        candidate.DisplayName = request.DisplayName.Trim();
        candidate.Manifesto = request.Manifesto.Trim();
        candidate.Status = CandidateStatus.Pending;
        candidate.RejectionReason = null;
        candidate.ReviewedAtUtc = null;
        candidate.ReviewedByUserId = null;
        candidate.UpdatedAtUtc = now;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "CandidateProfileUpdated",
            nameof(Candidate),
            candidate.Id.ToString(),
            AuditOutcome.Success,
            userId,
            candidate.DisplayName,
            correlationId,
            new { electionId = candidate.ElectionId, resubmitted = true },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task WithdrawAsync(
        Guid candidateId,
        Guid userId,
        CandidateWithdrawalRequest request,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var (candidate, election) = await LoadOwnedCandidateAsync(candidateId, userId, cancellationToken);
        if (election.Status is not (ElectionStatus.Draft or ElectionStatus.Scheduled))
        {
            throw new ConflictException("Candidates cannot withdraw after voting opens.");
        }

        if (candidate.Status is not (CandidateStatus.Pending or CandidateStatus.Approved))
        {
            throw new ConflictException("This nomination cannot be withdrawn.");
        }

        var wasApproved = candidate.Status == CandidateStatus.Approved;
        candidate.Status = CandidateStatus.Withdrawn;
        candidate.WithdrawalReason = request.Reason.Trim();
        candidate.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (wasApproved)
        {
            await RemoveCandidateRoleIfUnusedAsync(candidate.StudentId, userId);
        }

        await auditWriter.AppendAsync(
            db,
            "CandidateWithdrawn",
            nameof(Candidate),
            candidate.Id.ToString(),
            AuditOutcome.Success,
            userId,
            candidate.DisplayName,
            correlationId,
            new { electionId = candidate.ElectionId, reason = request.Reason },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task EnsureOwnedByAsync(
        Guid candidateId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var candidate = await db.Candidates.AsNoTracking()
            .Include(c => c.Student)
            .SingleOrDefaultAsync(c => c.Id == candidateId, cancellationToken)
            ?? throw new NotFoundException("Nomination was not found.");
        if (candidate.Student.UserId != userId)
        {
            throw new ForbiddenOperationException("You may only change your own nomination.");
        }
    }
    public async Task<string?> SetPhotoAsync(
        Guid candidateId,
        Guid userId,
        string fileName,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var (candidate, election) = await LoadOwnedCandidateAsync(candidateId, userId, cancellationToken);
        if (election.Status is not (ElectionStatus.Draft or ElectionStatus.Scheduled))
        {
            throw new ConflictException("Candidate photos cannot be changed after voting opens.");
        }

        var previous = candidate.PhotoFileName;
        candidate.PhotoFileName = fileName;
        candidate.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "CandidatePhotoUpdated",
            nameof(Candidate),
            candidate.Id.ToString(),
            AuditOutcome.Success,
            userId,
            candidate.DisplayName,
            correlationId,
            new { electionId = candidate.ElectionId },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return previous;
    }

    public async Task ApproveAsync(
        Guid candidateId,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var candidate = await LoadCandidateForAdminAsync(candidateId, cancellationToken);
        var election = await db.Elections.SingleAsync(e => e.Id == candidate.ElectionId, cancellationToken);
        await access.EnsureCanManageElectionAsync(actorUserId, candidate.ElectionId, cancellationToken);
        if (election.Status is not (ElectionStatus.Draft or ElectionStatus.Scheduled))
        {
            throw new ConflictException("Candidates can only be approved before voting opens.");
        }

        if (candidate.Status != CandidateStatus.Pending)
        {
            throw new ConflictException("Only a pending nomination can be approved.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        candidate.Status = CandidateStatus.Approved;
        candidate.ReviewedAtUtc = now;
        candidate.ReviewedByUserId = actorUserId;
        candidate.RejectionReason = null;
        candidate.UpdatedAtUtc = now;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (candidate.Student.UserId is not null)
        {
            var user = await userManager.FindByIdAsync(candidate.Student.UserId.Value.ToString());
            if (user is not null)
            {
                if (!await userManager.IsInRoleAsync(user, RoleNames.Candidate))
                {
                    await userManager.AddToRoleAsync(user, RoleNames.Candidate);
                }

                await userManager.UpdateSecurityStampAsync(user);
            }
        }

        await auditWriter.AppendAsync(
            db,
            "CandidateApproved",
            nameof(Candidate),
            candidate.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { electionId = candidate.ElectionId, positionId = candidate.PositionId },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RejectAsync(
        Guid candidateId,
        Guid actorUserId,
        string actorDisplay,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        if (reason.Trim().Length < 5)
        {
            throw new ValidationException("A rejection reason is required.");
        }

        var candidate = await LoadCandidateForAdminAsync(candidateId, cancellationToken);
        await access.EnsureCanManageElectionAsync(actorUserId, candidate.ElectionId, cancellationToken);
        if (candidate.Status != CandidateStatus.Pending)
        {
            throw new ConflictException("Only a pending nomination can be rejected.");
        }

        candidate.Status = CandidateStatus.Rejected;
        candidate.RejectionReason = reason.Trim();
        candidate.ReviewedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        candidate.ReviewedByUserId = actorUserId;
        candidate.UpdatedAtUtc = candidate.ReviewedAtUtc.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "CandidateRejected",
            nameof(Candidate),
            candidate.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { electionId = candidate.ElectionId, reason = candidate.RejectionReason },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DisqualifyAsync(
        Guid candidateId,
        Guid actorUserId,
        string actorDisplay,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        if (reason.Trim().Length < 5)
        {
            throw new ValidationException("A disqualification reason is required.");
        }

        var candidate = await LoadCandidateForAdminAsync(candidateId, cancellationToken);
        var election = await db.Elections.SingleAsync(e => e.Id == candidate.ElectionId, cancellationToken);
        await access.EnsureCanManageElectionAsync(actorUserId, candidate.ElectionId, cancellationToken);
        if (election.Status != ElectionStatus.Draft && election.Status != ElectionStatus.Scheduled)
        {
            throw new ConflictException("Candidates cannot be disqualified after voting opens.");
        }

        if (candidate.Status != CandidateStatus.Approved)
        {
            throw new ConflictException("Only an approved candidate can be disqualified.");
        }

        candidate.Status = CandidateStatus.Disqualified;
        candidate.RejectionReason = reason.Trim();
        candidate.ReviewedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        candidate.ReviewedByUserId = actorUserId;
        candidate.UpdatedAtUtc = candidate.ReviewedAtUtc.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (candidate.Student.UserId is not null)
        {
            await RemoveCandidateRoleIfUnusedAsync(candidate.StudentId, candidate.Student.UserId.Value);
        }

        await auditWriter.AppendAsync(
            db,
            "CandidateDisqualified",
            nameof(Candidate),
            candidate.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { electionId = candidate.ElectionId, reason = candidate.RejectionReason },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<(string ContentType, Stream Stream)?> OpenPhotoAsync(
        Guid candidateId,
        ICandidatePhotoStore store,
        CancellationToken cancellationToken = default)
    {
        var candidate = await db.Candidates.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == candidateId && c.Status == CandidateStatus.Approved, cancellationToken);
        if (candidate?.PhotoFileName is null)
        {
            return null;
        }

        var result = await store.OpenAsync(candidate.PhotoFileName, cancellationToken);
        return result is null ? null : (result.Value.ContentType, result.Value.Stream);
    }

    private async Task<(Candidate Candidate, Election Election)> LoadOwnedCandidateAsync(
        Guid candidateId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var candidate = await db.Candidates
            .Include(c => c.Student)
            .Include(c => c.Election)
            .SingleOrDefaultAsync(c => c.Id == candidateId, cancellationToken)
            ?? throw new NotFoundException("Nomination was not found.");
        if (candidate.Student.UserId != userId)
        {
            throw new ForbiddenOperationException("You may only change your own nomination.");
        }

        return (candidate, candidate.Election);
    }

    private async Task<Candidate> LoadCandidateForAdminAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        return await db.Candidates
            .Include(c => c.Student)
            .SingleOrDefaultAsync(c => c.Id == candidateId, cancellationToken)
            ?? throw new NotFoundException("Candidate was not found.");
    }

    private async Task RemoveCandidateRoleIfUnusedAsync(Guid studentId, Guid userId)
    {
        var stillApproved = await db.Candidates.AnyAsync(
            c => c.StudentId == studentId && c.Status == CandidateStatus.Approved);
        if (stillApproved)
        {
            return;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is not null && await userManager.IsInRoleAsync(user, RoleNames.Candidate))
        {
            await userManager.RemoveFromRoleAsync(user, RoleNames.Candidate);
            await userManager.UpdateSecurityStampAsync(user);
        }
    }

    private static CandidateProfileDto Map(Candidate candidate) =>
        new(
            candidate.Id,
            candidate.ElectionId,
            candidate.Election.Title,
            candidate.Election.DepartmentId,
            candidate.Election.DepartmentRef?.Code ?? candidate.Election.Department,
            candidate.Election.DepartmentRef?.Name ?? candidate.Election.Department,
            candidate.PositionId,
            candidate.Position.Title,
            candidate.DisplayName,
            candidate.Manifesto,
            string.IsNullOrWhiteSpace(candidate.PhotoFileName) ? null : $"/api/candidates/{candidate.Id}/photo",
            candidate.Status,
            candidate.RejectionReason,
            candidate.WithdrawalReason,
            candidate.SubmittedAtUtc,
            candidate.ReviewedAtUtc);
}
