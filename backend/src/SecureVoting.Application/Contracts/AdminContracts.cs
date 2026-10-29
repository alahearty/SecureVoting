using System.ComponentModel.DataAnnotations;
using SecureVoting.Domain;

namespace SecureVoting.Application.Contracts;

public sealed record UserSummaryDto(
    Guid Id,
    string DisplayName,
    string? Email,
    string? MatricNumber,
    bool IsActive,
    bool IsLockedOut,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

public sealed class AssignRolesRequest
{
    [Required]
    public List<string> Roles { get; set; } = [];
}

public sealed class SuspendUserRequest
{
    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed record DashboardMetric(string Label, string Value, string? Detail = null);

public sealed record DashboardChartSeries(string Label, IReadOnlyList<DashboardChartPoint> Points);

public sealed record DashboardChartPoint(string Label, decimal Value);

public sealed record AdminDashboardDto(
    IReadOnlyList<DashboardMetric> Metrics,
    IReadOnlyList<ElectionSummaryDto> ActiveElections,
    IReadOnlyList<AuditLogDto> RecentAudit,
    DashboardChartSeries TurnoutByElection);

public sealed record StudentDashboardDto(
    IReadOnlyList<ElectionSummaryDto> Elections,
    IReadOnlyList<AnnouncementDto> Announcements,
    IReadOnlyList<NotificationDto> Notifications);

public sealed record ObserverDashboardDto(
    IReadOnlyList<DashboardMetric> Metrics,
    IReadOnlyList<ElectionSummaryDto> Elections,
    IReadOnlyList<AuditLogDto> RecentAudit,
    DashboardChartSeries TurnoutByElection);

public sealed record CandidateDashboardDto(
    CandidateProfileDto? Nomination,
    IReadOnlyList<AnnouncementDto> Announcements);

public sealed record AuditLogDto(
    long Sequence,
    Guid EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    string ActorDisplay,
    string EventType,
    string EntityType,
    string? EntityId,
    AuditOutcome Outcome,
    string? CorrelationId,
    string DetailsJson,
    string Hash);

public sealed record AuditVerificationDto(
    bool IsValid,
    long EntriesChecked,
    long? BrokenAtSequence,
    string Message,
    string LastHash);

public sealed class UpdateSettingRequest
{
    [Required, StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string Value { get; set; } = string.Empty;
}

public sealed record SystemSettingDto(string Key, string Value, DateTimeOffset UpdatedAt);
