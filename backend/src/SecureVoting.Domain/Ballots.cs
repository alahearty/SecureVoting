namespace SecureVoting.Domain;

public sealed class Ballot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ElectionId { get; set; }
    public Election Election { get; set; } = null!;
    public DateTime SubmittedAtUtc { get; set; }
    public string ContentHash { get; set; } = string.Empty;

    public ICollection<BallotSelection> Selections { get; set; } = [];
    public ICollection<BallotAbstention> Abstentions { get; set; } = [];
    public BallotReceipt? Receipt { get; set; }
}

public sealed class BallotSelection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BallotId { get; set; }
    public Ballot Ballot { get; set; } = null!;
    public Guid ElectionPositionId { get; set; }
    public ElectionPosition ElectionPosition { get; set; } = null!;
    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class BallotAbstention
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BallotId { get; set; }
    public Ballot Ballot { get; set; } = null!;
    public Guid ElectionPositionId { get; set; }
    public ElectionPosition ElectionPosition { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class BallotReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BallotId { get; set; }
    public Ballot Ballot { get; set; } = null!;
    public string ReceiptHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
