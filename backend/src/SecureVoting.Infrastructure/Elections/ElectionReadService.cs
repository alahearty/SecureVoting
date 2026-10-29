using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Elections;

public sealed class ElectionReadService(AppDbContext db, DepartmentAccessService access)
{
    public async Task<IReadOnlyList<ElectionSummaryDto>> ListForAdminAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var query = db.Elections
            .AsNoTracking()
            .Include(e => e.Results)
            .Include(e => e.DepartmentRef)
            .AsQueryable();
        if (!await access.IsSuperAdminAsync(userId, cancellationToken))
        {
            var departments = await access.GetAssignedDepartmentIdsAsync(
                userId, DepartmentAccessScope.ElectoralOfficer, cancellationToken);
            if (departments.Count == 0)
            {
                return [];
            }

            query = query.Where(e => departments.Contains(e.DepartmentId));
        }

        var elections = await query
            .OrderByDescending(e => e.OpensAtUtc)
            .ToListAsync(cancellationToken);
        var counts = await CountsAsync(cancellationToken);
        return elections
            .Select(e => MapSummary(e, counts, null, null, null, null))
            .ToArray();
    }

    public async Task<IReadOnlyList<ElectionSummaryDto>> ListForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var elections = await db.Elections
            .AsNoTracking()
            .Include(e => e.DepartmentRef)
            .Include(e => e.VoterEligibility)
            .Include(e => e.VoterEntitlements)
            .Include(e => e.Results)
            .Where(e => e.VoterEligibility.Any(v => v.StudentId == studentId && v.IsEligible))
            .OrderByDescending(e => e.OpensAtUtc)
            .ToListAsync(cancellationToken);
        var counts = await CountsAsync(cancellationToken);
        return elections
            .Select(e =>
            {
                var eligibility = e.VoterEligibility.FirstOrDefault(v => v.StudentId == studentId);
                var entitlement = e.VoterEntitlements.FirstOrDefault(v => v.StudentId == studentId);
                return MapSummary(e, counts, studentId, eligibility?.IsEligible == true, entitlement?.ConsumedAtUtc is not null,
                    entitlement?.ConsumedAtUtc);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<ElectionSummaryDto>> ListForObserverAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (await access.IsSuperAdminAsync(userId, cancellationToken))
        {
            return await ListForReviewCoreAsync(null, cancellationToken);
        }

        var departments = await access.GetAssignedDepartmentIdsAsync(
            userId, DepartmentAccessScope.Observer, cancellationToken);
        return departments.Count == 0
            ? []
            : await ListForReviewCoreAsync(departments, cancellationToken);
    }

    public async Task<IReadOnlyList<ElectionSummaryDto>> ListForReviewAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (await access.IsSuperAdminAsync(userId, cancellationToken))
        {
            return await ListForReviewCoreAsync(null, cancellationToken);
        }

        var departments = await access.GetAllAssignedDepartmentIdsAsync(userId, cancellationToken);
        return departments.Count == 0
            ? []
            : await ListForReviewCoreAsync(departments, cancellationToken);
    }

    private async Task<IReadOnlyList<ElectionSummaryDto>> ListForReviewCoreAsync(
        IReadOnlySet<Guid>? departmentIds,
        CancellationToken cancellationToken)
    {
        var query = db.Elections
            .AsNoTracking()
            .Include(e => e.Results)
            .Include(e => e.DepartmentRef)
            .AsQueryable();
        if (departmentIds is not null)
        {
            query = query.Where(e => departmentIds.Contains(e.DepartmentId));
        }

        var elections = await query
            .OrderByDescending(e => e.OpensAtUtc)
            .ToListAsync(cancellationToken);
        var counts = await CountsAsync(cancellationToken);
        return elections.Select(e => MapSummary(e, counts, null, null, null, null)).ToArray();
    }

    public async Task<ElectionDetailDto> GetDetailAsync(
        Guid electionId,
        bool includeUnapprovedCandidates,
        CancellationToken cancellationToken = default)
    {
        var election = await db.Elections
            .AsNoTracking()
            .Include(e => e.DepartmentRef)
            .Include(e => e.Rule)
            .Include(e => e.Positions)
            .Include(e => e.Candidates)
                .ThenInclude(c => c.Student)
            .SingleOrDefaultAsync(e => e.Id == electionId, cancellationToken)
            ?? throw new NotFoundException("Election was not found.");

        var candidates = election.Candidates
            .Where(c => includeUnapprovedCandidates || c.Status == CandidateStatus.Approved)
            .OrderBy(c => c.DisplayName)
            .ToArray();
        var positions = election.Positions
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new PositionPublicDto(
                p.Id,
                p.Title,
                p.Description,
                p.SeatCount,
                p.MaxSelections,
                candidates
                    .Where(c => c.PositionId == p.Id && c.Status == CandidateStatus.Approved)
                    .Select(c => new CandidatePublicDto(
                        c.Id,
                        c.DisplayName,
                        c.Manifesto,
                        string.IsNullOrWhiteSpace(c.PhotoFileName) ? null : $"/api/candidates/{c.Id}/photo"))
                    .ToArray()))
            .ToArray();

        return new ElectionDetailDto(
            election.Id,
            election.Title,
            election.Description,
            election.AcademicSession,
            election.DepartmentId,
            election.DepartmentRef?.Code ?? election.Department,
            election.DepartmentRef?.Name ?? election.Department,
            election.Status,
            election.NominationOpensAtUtc,
            election.NominationClosesAtUtc,
            election.OpensAtUtc,
            election.ClosesAtUtc,
            new ElectionRuleDto(election.Rule.TieHandling, election.Rule.AllowAbstention,
                election.Rule.NominationRequiresApproval),
            positions,
            election.SuspensionReason,
            election.CancellationReason,
            election.PublishedAtUtc);
    }

    public async Task<IReadOnlyList<CandidateAdminDto>> GetCandidatesForAdminAsync(
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        return await db.Candidates
            .AsNoTracking()
            .Include(c => c.Student)
            .Include(c => c.Position)
            .Where(c => c.ElectionId == electionId)
            .OrderBy(c => c.Position.DisplayOrder)
            .ThenBy(c => c.DisplayName)
            .Select(c => new CandidateAdminDto(
                c.Id,
                c.ElectionId,
                c.PositionId,
                c.Position.Title,
                c.StudentId,
                c.Student.MatricNumber,
                c.Student.FullName,
                c.DisplayName,
                c.Manifesto,
                string.IsNullOrWhiteSpace(c.PhotoFileName) ? null : $"/api/candidates/{c.Id}/photo",
                c.Status,
                c.RejectionReason,
                c.SubmittedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EligibleVoterDto>> GetVotersAsync(
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        return await db.VoterEligibility
            .AsNoTracking()
            .Include(v => v.Student)
            .Include(v => v.Entitlement)
            .Where(v => v.ElectionId == electionId)
            .OrderBy(v => v.Student.MatricNumberNormalized)
            .Select(v => new EligibleVoterDto(
                v.StudentId,
                v.Student.MatricNumber,
                v.Student.FullName,
                v.Student.Email,
                v.Student.Level,
                v.Student.IsActive,
                v.Student.UserId != null,
                v.Entitlement != null && v.Entitlement.ConsumedAtUtc != null,
                v.Entitlement == null ? null : v.Entitlement.ConsumedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(
        Guid? electionId,
        AnnouncementAudience? audience,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<Guid>? allowedElectionIds = null)
    {
        var query = db.Announcements
            .AsNoTracking()
            .Include(a => a.Election)
            .Where(a => a.PublishedAtUtc != null);
        if (electionId is not null)
        {
            query = query.Where(a => a.ElectionId == electionId || a.ElectionId == null);
        }

        if (audience is not null)
        {
            query = query.Where(a => a.Audience == AnnouncementAudience.Everyone || a.Audience == audience);
        }

        if (allowedElectionIds is not null)
        {
            query = query.Where(a => a.ElectionId == null || allowedElectionIds.Contains(a.ElectionId.Value));
        }

        return await query
            .OrderByDescending(a => a.PublishedAtUtc)
            .Take(50)
            .Select(a => new AnnouncementDto(
                a.Id,
                a.ElectionId,
                a.Election == null ? null : a.Election.Title,
                a.Title,
                a.Body,
                a.Audience,
                a.PublishedAtUtc!.Value))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(100)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Subject, n.Body, n.CreatedAtUtc, n.IsRead))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, ElectionCounts>> CountsAsync(CancellationToken cancellationToken)
    {
        var eligible = await db.VoterEligibility
            .AsNoTracking()
            .Where(v => v.IsEligible)
            .GroupBy(v => v.ElectionId)
            .Select(g => new { ElectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ElectionId, x => x.Count, cancellationToken);
        var ballots = await db.Ballots
            .AsNoTracking()
            .GroupBy(b => b.ElectionId)
            .Select(g => new { ElectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ElectionId, x => x.Count, cancellationToken);
        var positions = await db.ElectionPositions
            .AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(p => p.ElectionId)
            .Select(g => new { ElectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ElectionId, x => x.Count, cancellationToken);
        var approved = await db.Candidates
            .AsNoTracking()
            .Where(c => c.Status == CandidateStatus.Approved)
            .GroupBy(c => c.ElectionId)
            .Select(g => new { ElectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ElectionId, x => x.Count, cancellationToken);
        return eligible.Keys
            .Union(ballots.Keys)
            .Union(positions.Keys)
            .Union(approved.Keys)
            .ToDictionary(
                id => id,
                id => new ElectionCounts(
                    eligible.GetValueOrDefault(id),
                    ballots.GetValueOrDefault(id),
                    positions.GetValueOrDefault(id),
                    approved.GetValueOrDefault(id)));
    }

    private static ElectionSummaryDto MapSummary(
        Election election,
        IReadOnlyDictionary<Guid, ElectionCounts> counts,
        Guid? studentId,
        bool? isEligible,
        bool? hasVoted,
        DateTime? votedAt)
    {
        var count = counts.GetValueOrDefault(election.Id, new ElectionCounts());
        var published = election.Results
            .Where(r => r.Status == ResultStatus.Published)
            .OrderByDescending(r => r.Version)
            .FirstOrDefault();
        return new ElectionSummaryDto(
            election.Id,
            election.Title,
            election.Description,
            election.AcademicSession,
            election.DepartmentId,
            election.DepartmentRef?.Code ?? election.Department,
            election.DepartmentRef?.Name ?? election.Department,
            election.Status,
            election.NominationOpensAtUtc,
            election.NominationClosesAtUtc,
            election.OpensAtUtc,
            election.ClosesAtUtc,
            count.EligibleVoters,
            count.AcceptedBallots,
            count.Positions,
            count.ApprovedCandidates,
            isEligible ?? false,
            hasVoted ?? false,
            votedAt,
            published?.Id,
            published?.PublishedAtUtc);
    }

    private sealed record ElectionCounts(
        int EligibleVoters = 0,
        int AcceptedBallots = 0,
        int Positions = 0,
        int ApprovedCandidates = 0);
}
