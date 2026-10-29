using System.ComponentModel.DataAnnotations;
using SecureVoting.Domain;

namespace SecureVoting.Application.Contracts;

public class CreateElectionRequest
{
    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string AcademicSession { get; set; } = string.Empty;

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public DateTimeOffset NominationOpensAt { get; set; }

    [Required]
    public DateTimeOffset NominationClosesAt { get; set; }

    [Required]
    public DateTimeOffset OpensAt { get; set; }

    [Required]
    public DateTimeOffset ClosesAt { get; set; }

    public bool AllowAbstention { get; set; } = true;
    public TieHandling TieHandling { get; set; } = TieHandling.RunoffRequired;
    public bool NominationRequiresApproval { get; set; } = true;
}

public sealed class UpdateElectionRequest : CreateElectionRequest;

public class CreatePositionRequest
{
    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(0, 1000)]
    public int DisplayOrder { get; set; }

    [Range(1, 20)]
    public int SeatCount { get; set; } = 1;

    [Range(1, 20)]
    public int MaxSelections { get; set; } = 1;
}

public sealed class UpdatePositionRequest : CreatePositionRequest
{
    public bool IsActive { get; set; } = true;
}

public sealed class ElectionStatusRequest
{
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CreateAnnouncementRequest
{
    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Body { get; set; } = string.Empty;

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.Everyone;
    public Guid? ElectionId { get; set; }
}

public sealed record ElectionSummaryDto(
    Guid Id,
    string Title,
    string Description,
    string AcademicSession,
    Guid DepartmentId,
    string DepartmentCode,
    string Department,
    ElectionStatus Status,
    DateTimeOffset NominationOpensAt,
    DateTimeOffset NominationClosesAt,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    int EligibleVoters,
    int AcceptedBallots,
    int PositionCount,
    int ApprovedCandidates,
    bool IsEligible,
    bool HasVoted,
    DateTimeOffset? VotedAt,
    Guid? CurrentResultId,
    DateTimeOffset? PublishedAt);

public sealed record CandidatePublicDto(
    Guid Id,
    string DisplayName,
    string Manifesto,
    string? PhotoUrl);

public sealed record PositionPublicDto(
    Guid Id,
    string Title,
    string Description,
    int SeatCount,
    int MaxSelections,
    IReadOnlyList<CandidatePublicDto> Candidates);

public sealed record ElectionRuleDto(
    TieHandling TieHandling,
    bool AllowAbstention,
    bool NominationRequiresApproval);

public sealed record ElectionDetailDto(
    Guid Id,
    string Title,
    string Description,
    string AcademicSession,
    Guid DepartmentId,
    string DepartmentCode,
    string Department,
    ElectionStatus Status,
    DateTimeOffset NominationOpensAt,
    DateTimeOffset NominationClosesAt,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    ElectionRuleDto Rule,
    IReadOnlyList<PositionPublicDto> Positions,
    string? SuspensionReason,
    string? CancellationReason,
    DateTimeOffset? PublishedAt);

public sealed class CandidateNominationRequest
{
    [Required]
    public Guid ElectionId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    [Required, StringLength(2000, MinimumLength = 20)]
    public string Manifesto { get; set; } = string.Empty;
}

public sealed class UpdateCandidateProfileRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 20)]
    public string Manifesto { get; set; } = string.Empty;
}

public sealed class CandidateDecisionRequest
{
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CandidateWithdrawalRequest
{
    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed record CandidateProfileDto(
    Guid Id,
    Guid ElectionId,
    string ElectionTitle,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    Guid PositionId,
    string PositionTitle,
    string DisplayName,
    string Manifesto,
    string? PhotoUrl,
    CandidateStatus Status,
    string? RejectionReason,
    string? WithdrawalReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt);

public sealed record CandidateAdminDto(
    Guid Id,
    Guid ElectionId,
    Guid PositionId,
    string PositionTitle,
    Guid StudentId,
    string MatricNumber,
    string StudentName,
    string DisplayName,
    string Manifesto,
    string? PhotoUrl,
    CandidateStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAt);

public sealed class VoterImportResponse
{
    public int RowsProcessed { get; init; }
    public int EligibleCreated { get; init; }
    public int EligibilityUpdated { get; init; }
    public int AccountsCreated { get; init; }
    public int DuplicateRows { get; init; }
    public IReadOnlyList<VoterImportIssue> Errors { get; init; } = [];
    public IReadOnlyList<ActivationCredentialDto> ActivationTokens { get; init; } = [];
}

public sealed record VoterImportIssue(int RowNumber, string Message);

public sealed record ActivationCredentialDto(string MatricNumber, string Token, DateTimeOffset ExpiresAt);

public sealed record EligibleVoterDto(
    Guid StudentId,
    string MatricNumber,
    string FullName,
    string Email,
    int Level,
    bool IsActive,
    bool HasAccount,
    bool HasVoted,
    DateTimeOffset? VotedAt);

public sealed record AnnouncementDto(
    Guid Id,
    Guid? ElectionId,
    string? ElectionTitle,
    string Title,
    string Body,
    AnnouncementAudience Audience,
    DateTimeOffset PublishedAt);

public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Subject,
    string Body,
    DateTimeOffset CreatedAt,
    bool IsRead);
