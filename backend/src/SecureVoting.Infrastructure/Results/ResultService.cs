using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Application.Exporting;
using SecureVoting.Application.Results;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Results;

public sealed class ResultService(
    AppDbContext db,
    AuditWriter auditWriter,
    TimeProvider timeProvider)
{
    public async Task<ElectionResultDto?> GetPublishedAsync(
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        var result = await db.ElectionResults
            .AsNoTracking()
            .Include(r => r.Election)
            .Include(r => r.Lines)
            .Include(r => r.Approvals)
            .Where(r => r.ElectionId == electionId && r.Status == ResultStatus.Published)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return result is null ? null : await MapAsync(result, cancellationToken);
    }

    public async Task<ElectionResultDto?> GetLatestForPrivilegedAsync(
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        var result = await db.ElectionResults
            .AsNoTracking()
            .Include(r => r.Election)
            .Include(r => r.Lines)
            .Include(r => r.Approvals)
            .Where(r => r.ElectionId == electionId)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return result is null ? null : await MapAsync(result, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetVersionsAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        return await db.ElectionResults.AsNoTracking()
            .Where(r => r.ElectionId == electionId)
            .OrderByDescending(r => r.Version)
            .Select(r => r.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<ElectionResultDto> RequestReviewAsync(
        Guid electionId,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var election = await db.Elections
                .FromSqlInterpolated($"SELECT e.*, e.xmin FROM \"Elections\" AS e WHERE e.\"Id\" = {electionId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Election was not found.");
            if (election.Status != ElectionStatus.Closed)
            {
                throw new ConflictException("Only a closed election can be sent for result review.");
            }

            await db.Entry(election).Reference(e => e.Rule).LoadAsync(cancellationToken);
            var result = await CreateSnapshotAsync(election, actorUserId, null, cancellationToken);
            election.Status = ElectionStatus.UnderReview;
            election.ResultsRequestedAtUtc = now;
            election.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                "ElectionUnderReview",
                nameof(Election),
                election.Id.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                correlationId,
                new { resultId = result.Id, version = result.Version, integrityHash = result.IntegrityHash },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await MapAsync(result, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ElectionResultDto> VerifyAsync(
        Guid electionId,
        Guid resultId,
        Guid actorUserId,
        string actorDisplay,
        string notes,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var election = await db.Elections
                .FromSqlInterpolated($"SELECT e.*, e.xmin FROM \"Elections\" AS e WHERE e.\"Id\" = {electionId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Election was not found.");
            if (election.Status != ElectionStatus.UnderReview)
            {
                throw new ConflictException("The election is not under result review.");
            }

            var result = await db.ElectionResults
                .Include(r => r.Lines)
                .Include(r => r.Approvals)
                .SingleOrDefaultAsync(r => r.Id == resultId && r.ElectionId == electionId, cancellationToken)
                ?? throw new NotFoundException("Result was not found.");
            if (result.Status != ResultStatus.Provisional)
            {
                throw new ConflictException("Only a provisional result can be verified.");
            }

            var latestResultId = await db.ElectionResults
                .Where(r => r.ElectionId == electionId)
                .OrderByDescending(r => r.Version)
                .Select(r => r.Id)
                .FirstAsync(cancellationToken);
            if (latestResultId != result.Id)
            {
                throw new ConflictException("A newer result snapshot exists; verify the latest version.");
            }

            if (result.Approvals.Any(a => a.UserId == actorUserId &&
                                          a.ApprovalType == ApprovalType.ObserverVerification))
            {
                throw new ConflictException("You already verified this result.");
            }

            var verification = new ResultApproval
            {
                UserId = actorUserId,
                Role = RoleNames.Observer,
                ApprovalType = ApprovalType.ObserverVerification,
                Decision = ApprovalDecision.Approved,
                Notes = notes.Trim(),
                CreatedAtUtc = now
            };
            db.ResultApprovals.Add(verification);
            result.Approvals.Add(verification);
            result.Status = ResultStatus.Verified;
            result.VerifiedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                "ResultVerifiedByObserver",
                nameof(ElectionResult),
                result.Id.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                null,
                new { electionId, version = result.Version, integrityHash = result.IntegrityHash },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await MapAsync(result, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ElectionResultDto> PublishAsync(
        Guid electionId,
        Guid resultId,
        Guid actorUserId,
        string actorDisplay,
        string notes,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var election = await db.Elections
                .FromSqlInterpolated($"SELECT e.*, e.xmin FROM \"Elections\" AS e WHERE e.\"Id\" = {electionId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Election was not found.");
            if (election.Status != ElectionStatus.UnderReview)
            {
                throw new ConflictException("The election is not under result review.");
            }

            var result = await db.ElectionResults
                .Include(r => r.Lines)
                .Include(r => r.Approvals)
                .SingleOrDefaultAsync(r => r.Id == resultId && r.ElectionId == electionId, cancellationToken)
                ?? throw new NotFoundException("Result was not found.");
            if (result.Status != ResultStatus.Verified ||
                !result.Approvals.Any(a => a.ApprovalType == ApprovalType.ObserverVerification &&
                                           a.Decision == ApprovalDecision.Approved))
            {
                throw new ConflictException("An independent observer must verify the result before publication.");
            }

            var latestResultId = await db.ElectionResults
                .Where(r => r.ElectionId == electionId)
                .OrderByDescending(r => r.Version)
                .Select(r => r.Id)
                .FirstAsync(cancellationToken);
            if (latestResultId != result.Id)
            {
                throw new ConflictException("Only the latest result snapshot can be published.");
            }

            var previouslyPublished = await db.ElectionResults
                .Where(r => r.ElectionId == electionId && r.Status == ResultStatus.Published && r.Id != result.Id)
                .ToListAsync(cancellationToken);
            foreach (var previous in previouslyPublished)
            {
                previous.Status = ResultStatus.Superseded;
            }

            if (previouslyPublished.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            result.Status = ResultStatus.Published;
            result.PublishedAtUtc = now;
            result.PublishedByUserId = actorUserId;
            var publicationApproval = new ResultApproval
            {
                UserId = actorUserId,
                Role = RoleNames.SuperAdministrator,
                ApprovalType = ApprovalType.PublicationApproval,
                Decision = ApprovalDecision.Approved,
                Notes = notes.Trim(),
                CreatedAtUtc = now
            };
            db.ResultApprovals.Add(publicationApproval);
            result.Approvals.Add(publicationApproval);
            election.Status = ElectionStatus.Published;
            election.PublishedAtUtc = now;
            election.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                "ResultPublished",
                nameof(ElectionResult),
                result.Id.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                null,
                new { electionId, version = result.Version, integrityHash = result.IntegrityHash, supersededVersions = previouslyPublished.Select(x => x.Version) },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await MapAsync(result, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ElectionResultDto> RecountAsync(
        Guid electionId,
        Guid actorUserId,
        string actorDisplay,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (reason.Trim().Length < 5)
        {
            throw new ValidationException("A recount reason is required.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var election = await db.Elections
                .FromSqlInterpolated($"SELECT e.*, e.xmin FROM \"Elections\" AS e WHERE e.\"Id\" = {electionId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Election was not found.");
            if (election.Status != ElectionStatus.Published)
            {
                throw new ConflictException("A recount can only be initiated for a published election.");
            }

            var previous = await db.ElectionResults
                .Where(r => r.ElectionId == electionId && r.Status == ResultStatus.Published)
                .OrderByDescending(r => r.Version)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ConflictException("The published result record is missing.");
            ElectionStateMachine.EnsureTransition(election.Status, ElectionStatus.UnderReview);
            await db.Entry(election).Reference(e => e.Rule).LoadAsync(cancellationToken);
            var result = await CreateSnapshotAsync(election, actorUserId, previous.Id, cancellationToken);
            result.ChangeReason = reason.Trim();
            election.Status = ElectionStatus.UnderReview;
            election.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                "ResultRecountRequested",
                nameof(ElectionResult),
                result.Id.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                null,
                new { electionId, previousVersion = previous.Version, newVersion = result.Version, integrityHash = result.IntegrityHash, reason },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await MapAsync(result, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<string> ExportCsvAsync(
        Guid electionId,
        bool publishedOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = publishedOnly
            ? await GetPublishedAsync(electionId, cancellationToken)
            : await GetLatestForPrivilegedAsync(electionId, cancellationToken);
        if (result is null)
        {
            throw new NotFoundException("No result snapshot exists for this election.");
        }
        var builder = new StringBuilder();
        builder.AppendLine("Election,Version,Status,Position,SeatCount,Candidate,Votes,Percentage,Rank,Winner,Tie,Abstentions,TurnoutPercent");
        foreach (var position in result.Positions)
        {
            foreach (var candidate in position.Candidates)
            {
                builder.AppendLine(string.Join(
                    ',',
                    CsvEscaper.Escape(result.ElectionTitle),
                    result.Version.ToString(CultureInfo.InvariantCulture),
                    CsvEscaper.Escape(result.Status.ToString()),
                    CsvEscaper.Escape(position.PositionTitle),
                    position.SeatCount.ToString(CultureInfo.InvariantCulture),
                    CsvEscaper.Escape(candidate.CandidateName),
                    candidate.Votes.ToString(CultureInfo.InvariantCulture),
                    candidate.Percentage.ToString("0.00", CultureInfo.InvariantCulture),
                    candidate.Rank.ToString(CultureInfo.InvariantCulture),
                    candidate.IsWinner ? "YES" : "NO",
                    candidate.IsTie ? "YES" : "NO",
                    position.Abstentions.ToString(CultureInfo.InvariantCulture),
                    result.TurnoutPercentage.ToString("0.00", CultureInfo.InvariantCulture)));
            }
        }

        return builder.ToString();
    }

    private async Task<ElectionResult> CreateSnapshotAsync(
        Election election,
        Guid actorUserId,
        Guid? previousResultId,
        CancellationToken cancellationToken)
    {
        var positions = await db.ElectionPositions
            .Where(p => p.ElectionId == election.Id && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);
        var candidates = await db.Candidates
            .Where(c => c.ElectionId == election.Id)
            .ToListAsync(cancellationToken);
        var ballots = await db.Ballots
            .Include(b => b.Selections)
            .Include(b => b.Abstentions)
            .Where(b => b.ElectionId == election.Id)
            .ToListAsync(cancellationToken);
        var eligible = await db.VoterEligibility.CountAsync(
            v => v.ElectionId == election.Id && v.IsEligible, cancellationToken);
        var tallied = ResultCalculator.Calculate(election, positions, candidates, ballots, eligible);
        var latestVersion = await db.ElectionResults
            .Where(r => r.ElectionId == election.Id)
            .Select(r => (int?)r.Version)
            .MaxAsync(cancellationToken) ?? 0;
        var version = latestVersion + 1;
        var result = new ElectionResult
        {
            ElectionId = election.Id,
            Version = version,
            Status = ResultStatus.Provisional,
            TotalEligible = tallied.TotalEligible,
            TotalBallots = tallied.TotalBallots,
            TurnoutPercentage = tallied.TurnoutPercentage,
            GeneratedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            GeneratedByUserId = actorUserId,
            PreviousResultId = previousResultId,
            IntegrityHash = string.Empty
        };

        foreach (var position in tallied.Positions)
        {
            foreach (var candidate in position.Candidates)
            {
                result.Lines.Add(new ElectionResultLine
                {
                    PositionId = position.PositionId,
                    CandidateId = candidate.CandidateId,
                    VoteCount = candidate.Votes,
                    PositionSeatCount = position.SeatCount,
                    PositionAbstentions = position.Abstentions,
                    Percentage = candidate.Percentage,
                    Rank = candidate.Rank,
                    IsWinner = candidate.IsWinner,
                    IsTie = candidate.IsTie
                });
            }
        }

        result.IntegrityHash = ResultIntegrity.Compute(result);
        db.ElectionResults.Add(result);
        return result;
    }

    private async Task<ElectionResultDto> MapAsync(ElectionResult result, CancellationToken cancellationToken)
    {
        if (!ResultIntegrity.Verify(result))
        {
            throw new ConflictException("Result snapshot integrity verification failed.");
        }

        var electionTitle = result.Election?.Title
            ?? await db.Elections.AsNoTracking()
                .Where(e => e.Id == result.ElectionId)
                .Select(e => e.Title)
                .SingleAsync(cancellationToken);
        var positionTitles = await db.ElectionPositions.AsNoTracking()
            .Where(p => p.ElectionId == result.ElectionId)
            .ToDictionaryAsync(p => p.Id, p => p.Title, cancellationToken);
        var candidateNames = await db.Candidates.AsNoTracking()
            .Where(c => c.ElectionId == result.ElectionId)
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, cancellationToken);
        var positions = result.Lines
            .GroupBy(line => line.PositionId)
            .Select(group =>
            {
                var tieNote = group.Any(x => x.IsTie)
                    ? "A tie exists in this position. The configured tie rule applies."
                    : null;
                return new ResultPositionDto(
                    group.Key,
                    positionTitles.GetValueOrDefault(group.Key, "Position"),
                    group.Max(x => x.PositionSeatCount),
                    group.Max(x => x.PositionAbstentions),
                    group.OrderByDescending(x => x.VoteCount)
                        .ThenBy(x => x.CandidateId)
                        .Select(x => new ResultCandidateDto(
                            x.CandidateId,
                            candidateNames.GetValueOrDefault(x.CandidateId, "Candidate"),
                            x.VoteCount,
                            x.Percentage,
                            x.Rank,
                            x.IsWinner,
                            x.IsTie))
                        .ToArray(),
                    tieNote);
            })
            .ToArray();
        var approvals = result.Approvals
            .Select(a => new ResultApprovalDto(
                a.Role,
                a.ApprovalType.ToString(),
                a.Decision.ToString(),
                a.Notes,
                a.CreatedAtUtc))
            .ToArray();
        return new ElectionResultDto(
            result.Id,
            result.ElectionId,
            electionTitle,
            result.Version,
            result.Status,
            result.TotalEligible,
            result.TotalBallots,
            result.TurnoutPercentage,
            result.GeneratedAtUtc,
            result.VerifiedAtUtc,
            result.PublishedAtUtc,
            result.IntegrityHash,
            positions,
            approvals,
            result.ChangeReason);
    }

    private static string Csv(string value)
    {
        var safe = value;
        if (safe.Length > 0 && (safe[0] is '=' or '+' or '-' or '@' || char.IsControl(safe[0])))
        {
            safe = "'" + safe;
        }

        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
