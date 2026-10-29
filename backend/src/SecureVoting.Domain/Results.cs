namespace SecureVoting.Domain;

public sealed class ElectionResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ElectionId { get; set; }
    public Election Election { get; set; } = null!;
    public int Version { get; set; }
    public ResultStatus Status { get; set; } = ResultStatus.Provisional;
    public int TotalEligible { get; set; }
    public int TotalBallots { get; set; }
    public decimal TurnoutPercentage { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public Guid? PreviousResultId { get; set; }
    public string? ChangeReason { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;

    public ICollection<ElectionResultLine> Lines { get; set; } = [];
    public ICollection<ResultApproval> Approvals { get; set; } = [];
}

public sealed class ElectionResultLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ElectionResultId { get; set; }
    public ElectionResult ElectionResult { get; set; } = null!;
    public Guid PositionId { get; set; }
    public Guid CandidateId { get; set; }
    public int VoteCount { get; set; }
    public int PositionSeatCount { get; set; }
    public int PositionAbstentions { get; set; }
    public decimal Percentage { get; set; }
    public int Rank { get; set; }
    public bool IsWinner { get; set; }
    public bool IsTie { get; set; }
}

public sealed class ResultApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ElectionResultId { get; set; }
    public ElectionResult ElectionResult { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public ApprovalType ApprovalType { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
