namespace SecureVoting.Domain;

public sealed class Election
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AcademicSession { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public Department DepartmentRef { get; set; } = null!;
    public ElectionStatus Status { get; set; } = ElectionStatus.Draft;
    public DateTime NominationOpensAtUtc { get; set; }
    public DateTime NominationClosesAtUtc { get; set; }
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public ElectionStatus? SuspendedFromStatus { get; set; }
    public string? SuspensionReason { get; set; }
    public string? ClosureReason { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? ResultsRequestedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public uint Version { get; set; }

    public ElectionRule Rule { get; set; } = new();
    public ICollection<ElectionPosition> Positions { get; set; } = [];
    public ICollection<VoterEligibility> VoterEligibility { get; set; } = [];
    public ICollection<VoterEntitlement> VoterEntitlements { get; set; } = [];
    public ICollection<Candidate> Candidates { get; set; } = [];
    public ICollection<Ballot> Ballots { get; set; } = [];
    public ICollection<ElectionResult> Results { get; set; } = [];
    public ICollection<Announcement> Announcements { get; set; } = [];
}

public sealed class ElectionRule
{
    public Guid ElectionId { get; set; }
    public Election Election { get; set; } = null!;
    public TieHandling TieHandling { get; set; } = TieHandling.RunoffRequired;
    public bool AllowAbstention { get; set; } = true;
    public bool NominationRequiresApproval { get; set; } = true;
}

public sealed class ElectionPosition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ElectionId { get; set; }
    public Election Election { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int SeatCount { get; set; } = 1;
    public int MaxSelections { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<Candidate> Candidates { get; set; } = [];
    public ICollection<BallotSelection> BallotSelections { get; set; } = [];
    public ICollection<BallotAbstention> BallotAbstentions { get; set; } = [];
}

public sealed class Candidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ElectionId { get; set; }
    public Election Election { get; set; } = null!;
    public Guid PositionId { get; set; }
    public ElectionPosition Position { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public string Manifesto { get; set; } = string.Empty;
    public string? PhotoFileName { get; set; }
    public CandidateStatus Status { get; set; } = CandidateStatus.Pending;
    public string? RejectionReason { get; set; }
    public string? WithdrawalReason { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<BallotSelection> BallotSelections { get; set; } = [];
}
