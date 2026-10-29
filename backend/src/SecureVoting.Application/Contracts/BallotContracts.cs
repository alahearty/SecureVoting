using System.ComponentModel.DataAnnotations;

namespace SecureVoting.Application.Contracts;

public sealed class CastBallotRequest
{
    [Required, MinLength(1)]
    public List<PositionChoiceRequest> Positions { get; set; } = [];
}

public sealed class PositionChoiceRequest
{
    [Required]
    public Guid PositionId { get; set; }

    public List<Guid> CandidateIds { get; set; } = [];
}

public sealed record BallotCandidateDto(
    Guid Id,
    string DisplayName,
    string Manifesto,
    string? PhotoUrl);

public sealed record BallotPositionDto(
    Guid Id,
    string Title,
    string Description,
    int SeatCount,
    int MaxSelections,
    bool AllowAbstention,
    IReadOnlyList<BallotCandidateDto> Candidates);

public sealed record BallotDto(
    Guid ElectionId,
    string Title,
    Guid DepartmentId,
    string DepartmentCode,
    string Department,
    DateTimeOffset ClosesAt,
    bool HasVoted,
    DateTimeOffset? VotedAt,
    bool IsEligible,
    IReadOnlyList<BallotPositionDto> Positions);

public sealed record BallotAcceptedDto(
    Guid ElectionId,
    DateTimeOffset SubmittedAtUtc,
    string ConfirmationReference,
    string Message);

public sealed record VoteStatusDto(
    Guid ElectionId,
    bool HasVoted,
    DateTimeOffset? VotedAt,
    string? ConfirmationHint);
