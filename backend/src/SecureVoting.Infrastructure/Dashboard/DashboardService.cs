using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Candidates;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Elections;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Dashboard;

public sealed class DashboardService(
    AppDbContext db,
    ElectionReadService electionRead,
    CandidateService candidateService,
    DepartmentAccessService access)
{
    public async Task<AdminDashboardDto> GetAdminAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var elections = await electionRead.ListForAdminAsync(userId, cancellationToken);
        var totalEligible = elections.Sum(e => e.EligibleVoters);
        var totalBallots = elections.Sum(e => e.AcceptedBallots);
        var electionIds = elections.Select(e => e.Id).ToHashSet();
        var pendingCandidates = await db.Candidates.CountAsync(
            c => c.Status == CandidateStatus.Pending && electionIds.Contains(c.ElectionId), cancellationToken);
        IReadOnlySet<Guid>? departmentIds = await access.IsSuperAdminAsync(userId, cancellationToken)
            ? null
            : await access.GetAssignedDepartmentIdsAsync(
                userId, DepartmentAccessScope.ElectoralOfficer, cancellationToken);
        var metrics = new[]
        {
            new DashboardMetric("Eligible voters", totalEligible.ToString(), "Across all elections"),
            new DashboardMetric("Accepted ballots", totalBallots.ToString(), "Anonymous ballots only"),
            new DashboardMetric("Turnout", totalEligible == 0 ? "0%" : $"{totalBallots * 100m / totalEligible:0.0}%", "All elections"),
            new DashboardMetric("Candidate approvals", pendingCandidates.ToString(), "Pending decision")
        };
        return new AdminDashboardDto(
            metrics,
            elections.Where(e => e.Status is ElectionStatus.Open or ElectionStatus.Scheduled or ElectionStatus.UnderReview)
                .Take(6).ToArray(),
            await RecentAuditAsync(departmentIds, cancellationToken),
            await TurnoutChartAsync(electionIds, cancellationToken));
    }

    public async Task<StudentDashboardDto> GetStudentAsync(
        Guid studentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var elections = await electionRead.ListForStudentAsync(studentId, cancellationToken);
        var announcements = await electionRead.GetAnnouncementsAsync(
            null, AnnouncementAudience.EligibleVoters, cancellationToken,
            elections.Select(e => e.Id).ToArray());
        var notifications = await electionRead.GetNotificationsAsync(userId, cancellationToken);
        return new StudentDashboardDto(elections, announcements, notifications);
    }

    public async Task<ObserverDashboardDto> GetObserverAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var elections = await electionRead.ListForObserverAsync(userId, cancellationToken);
        var electionIds = elections.Select(e => e.Id).ToHashSet();
        var departmentIds = await access.GetAssignedDepartmentIdsAsync(
            userId, DepartmentAccessScope.Observer, cancellationToken);
        var totalEligible = elections.Sum(e => e.EligibleVoters);
        var totalBallots = elections.Sum(e => e.AcceptedBallots);
        var metrics = new[]
        {
            new DashboardMetric("Published results", elections.Count(e => e.Status == ElectionStatus.Published).ToString()),
            new DashboardMetric("Awaiting verification", elections.Count(e => e.Status == ElectionStatus.UnderReview).ToString()),
            new DashboardMetric("Turnout", totalEligible == 0 ? "0%" : $"{totalBallots * 100m / totalEligible:0.0}%", "Aggregate"),
            new DashboardMetric("Audit chain", "Verifiable", "Open Audit Trail to verify")
        };
        return new ObserverDashboardDto(
            metrics,
            elections,
            await RecentAuditAsync(departmentIds, cancellationToken),
            await TurnoutChartAsync(electionIds, cancellationToken));
    }

    public async Task<CandidateDashboardDto> GetCandidateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var nomination = await candidateService.GetMyNominationAsync(userId, cancellationToken);
        var announcements = await electionRead.GetAnnouncementsAsync(
            null, AnnouncementAudience.Candidates, cancellationToken,
            nomination is null ? [] : [nomination.ElectionId]);
        return new CandidateDashboardDto(nomination, announcements);
    }

    private async Task<IReadOnlyList<AuditLogDto>> RecentAuditAsync(
        IReadOnlySet<Guid>? departmentIds,
        CancellationToken cancellationToken)
    {
        var query = db.AuditLogs
            .AsNoTracking()
            .AsQueryable();
        if (departmentIds is not null)
        {
            query = query.Where(a => a.DepartmentId != null && departmentIds.Contains(a.DepartmentId.Value));
        }

        return await query
            .OrderByDescending(a => a.Sequence)
            .Take(10)
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
            .ToListAsync(cancellationToken);
    }

    private async Task<DashboardChartSeries> TurnoutChartAsync(
        IReadOnlySet<Guid>? electionIds,
        CancellationToken cancellationToken)
    {
        var query = db.Elections.AsNoTracking().AsQueryable();
        if (electionIds is not null)
        {
            query = query.Where(e => electionIds.Contains(e.Id));
        }

        var elections = await query
            .OrderByDescending(e => e.OpensAtUtc)
            .Take(6)
            .Select(e => new
            {
                e.Title,
                e.Id,
                e.OpensAtUtc,
                Eligible = e.VoterEligibility.Count(v => v.IsEligible),
                Ballots = e.Ballots.Count()
            })
            .ToListAsync(cancellationToken);
        return new DashboardChartSeries(
            "Turnout by election",
            elections.OrderBy(e => e.OpensAtUtc).Select(e => new DashboardChartPoint(
                e.Title.Length > 24 ? e.Title[..24] + "..." : e.Title,
                e.Eligible == 0 ? 0 : Math.Round(e.Ballots * 100m / e.Eligible, 1))).ToArray());
    }
}
