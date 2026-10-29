using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Application.Elections;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Ballots;

public sealed class BallotService(
    AppDbContext db,
    AuditWriter auditWriter,
    TimeProvider timeProvider)
{
    public async Task<BallotDto> GetBallotAsync(
        Guid electionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var student = await db.Students.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken)
            ?? throw new ForbiddenOperationException("A student profile is required to access a ballot.");
        var election = await db.Elections.AsNoTracking()
            .Include(e => e.Rule)
            .Include(e => e.DepartmentRef)
            .SingleOrDefaultAsync(e => e.Id == electionId, cancellationToken)
            ?? throw new NotFoundException("Election was not found.");
        var eligibility = await db.VoterEligibility.AsNoTracking()
            .SingleOrDefaultAsync(v => v.ElectionId == electionId && v.StudentId == student.Id, cancellationToken);
        if (eligibility is null || !eligibility.IsEligible)
        {
            throw new ForbiddenOperationException("You are not eligible for this election.");
        }

        var entitlement = await db.VoterEntitlements.AsNoTracking()
            .SingleOrDefaultAsync(v => v.ElectionId == electionId && v.StudentId == student.Id, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (election.Status != ElectionStatus.Open || now < election.OpensAtUtc || now >= election.ClosesAtUtc)
        {
            throw new ConflictException("This election is not currently open for voting.");
        }

        var positions = await db.ElectionPositions.AsNoTracking()
            .Where(p => p.ElectionId == electionId && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);
        var candidates = await db.Candidates.AsNoTracking()
            .Where(c => c.ElectionId == electionId && c.Status == CandidateStatus.Approved)
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);
        var positionDtos = positions.Select(position => new BallotPositionDto(
            position.Id,
            position.Title,
            position.Description,
            position.SeatCount,
            position.MaxSelections,
            election.Rule.AllowAbstention,
            candidates
                .Where(c => c.PositionId == position.Id)
                .Select(c => new BallotCandidateDto(
                    c.Id,
                    c.DisplayName,
                    c.Manifesto,
                    string.IsNullOrWhiteSpace(c.PhotoFileName) ? null : $"/api/candidates/{c.Id}/photo"))
                .ToArray()))
            .ToArray();

        return new BallotDto(
            election.Id,
            election.Title,
            election.DepartmentId,
            election.DepartmentRef?.Code ?? election.Department,
            election.DepartmentRef?.Name ?? election.Department,
            election.ClosesAtUtc,
            entitlement?.ConsumedAtUtc is not null,
            entitlement?.ConsumedAtUtc,
            true,
            positionDtos);
    }

    public async Task<VoteStatusDto> GetStatusAsync(
        Guid electionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var student = await db.Students.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken)
            ?? throw new ForbiddenOperationException("A student profile is required.");
        var entitlement = await db.VoterEntitlements.AsNoTracking()
            .SingleOrDefaultAsync(v => v.ElectionId == electionId && v.StudentId == student.Id, cancellationToken);
        return new VoteStatusDto(
            electionId,
            entitlement?.ConsumedAtUtc is not null,
            entitlement?.ConsumedAtUtc,
            entitlement?.ConfirmationCodeHash is null ? null : "Confirmation code was issued at submission.");
    }

    public async Task<BallotAcceptedDto> SubmitAsync(
        Guid electionId,
        Guid userId,
        CastBallotRequest request,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var student = await db.Students
            .SingleOrDefaultAsync(s => s.UserId == userId && s.IsActive, cancellationToken)
            ?? throw new ForbiddenOperationException("An active student profile is required to vote.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var election = await db.Elections
                .FromSqlInterpolated($"SELECT e.*, e.xmin FROM \"Elections\" AS e WHERE e.\"Id\" = {electionId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Election was not found.");
            await db.Entry(election).Reference(e => e.Rule).LoadAsync(cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;

            var eligibility = await db.VoterEligibility
                .SingleOrDefaultAsync(v => v.ElectionId == electionId && v.StudentId == student.Id, cancellationToken)
                ?? throw new ForbiddenOperationException("You are not eligible for this election.");
            if (!eligibility.IsEligible)
            {
                throw new ForbiddenOperationException("You are not eligible for this election.");
            }

            var entitlement = await db.VoterEntitlements
                .SingleOrDefaultAsync(v => v.ElectionId == electionId && v.StudentId == student.Id, cancellationToken)
                ?? throw new ConflictException("Your voting entitlement is missing. Contact the electoral officer.");
            if (entitlement.ConsumedAtUtc is not null)
            {
                throw new ConflictException("You have already submitted a ballot for this election.");
            }

            var positions = await db.ElectionPositions
                .Where(p => p.ElectionId == electionId && p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync(cancellationToken);
            var candidates = await db.Candidates
                .Where(c => c.ElectionId == electionId)
                .ToListAsync(cancellationToken);
            var validated = BallotValidator.Validate(
                election,
                positions,
                candidates,
                request.Positions,
                isEligible: true,
                now);

            var confirmationReference = TokenGenerator.Create(24);
            var confirmationHash = TokenGenerator.Sha256Hex(confirmationReference);
            var affected = await db.VoterEntitlements
                .Where(e => e.Id == entitlement.Id && e.ConsumedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.ConsumedAtUtc, now)
                    .SetProperty(e => e.ConfirmationCodeHash, confirmationHash), cancellationToken);
            if (affected != 1)
            {
                throw new ConflictException("You have already submitted a ballot for this election.");
            }

            var ballot = new Ballot
            {
                ElectionId = electionId,
                SubmittedAtUtc = now,
                ContentHash = string.Empty
            };
            var canonicalChoices = validated.Positions
                .OrderBy(p => p.PositionId)
                .Select(p => $"{p.PositionId:N}:{(p.IsAbstention ? "ABSTAIN" : string.Join(',', p.CandidateIds.Order()))}")
                .ToArray();
            ballot.ContentHash = TokenGenerator.Sha256Hex(string.Join("|", canonicalChoices));
            foreach (var position in validated.Positions)
            {
                if (position.IsAbstention)
                {
                    ballot.Abstentions.Add(new BallotAbstention
                    {
                        ElectionPositionId = position.PositionId,
                        CreatedAtUtc = now
                    });
                }
                else
                {
                    foreach (var candidateId in position.CandidateIds)
                    {
                        ballot.Selections.Add(new BallotSelection
                        {
                            ElectionPositionId = position.PositionId,
                            CandidateId = candidateId,
                            CreatedAtUtc = now
                        });
                    }
                }
            }

            ballot.Receipt = new BallotReceipt
            {
                ReceiptHash = TokenGenerator.Sha256Hex(TokenGenerator.Create(32)),
                CreatedAtUtc = now
            };
            db.Ballots.Add(ballot);
            await auditWriter.AppendAsync(
                db,
                "BallotAccepted",
                nameof(Election),
                election.Id.ToString(),
                AuditOutcome.Success,
                null,
                "Anonymous student ballot",
                correlationId,
                new { recorded = true },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new BallotAcceptedDto(
                electionId,
                now,
                confirmationReference,
                "Your ballot was accepted. The confirmation code is shown once and does not reveal your choices.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
