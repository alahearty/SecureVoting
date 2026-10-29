using System.ComponentModel.DataAnnotations;
using SecureVoting.Domain;

namespace SecureVoting.Application.Contracts;

public sealed record ResultCandidateDto(
    Guid CandidateId,
    string CandidateName,
    int Votes,
    decimal Percentage,
    int Rank,
    bool IsWinner,
    bool IsTie);

public sealed record ResultPositionDto(
    Guid PositionId,
    string PositionTitle,
    int SeatCount,
    int Abstentions,
    IReadOnlyList<ResultCandidateDto> Candidates,
    string? TieNote);

public sealed record ResultApprovalDto(
    string Role,
    string ApprovalType,
    string Decision,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record ElectionResultDto(
    Guid Id,
    Guid ElectionId,
    string ElectionTitle,
    int Version,
    ResultStatus Status,
    int TotalEligible,
    int TotalBallots,
    decimal TurnoutPercentage,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? PublishedAt,
    string IntegrityHash,
    IReadOnlyList<ResultPositionDto> Positions,
    IReadOnlyList<ResultApprovalDto> Approvals,
    string? ChangeReason);

public sealed class VerifyResultRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class ResultDecisionRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class RecountRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}
