namespace SecureVoting.Domain;

public sealed class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MatricNumber { get; set; } = string.Empty;
    public string MatricNumberNormalized { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public Department DepartmentRef { get; set; } = null!;
    public int Level { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<VoterEligibility> EligibilityRecords { get; set; } = [];
    public ICollection<Candidate> Candidates { get; set; } = [];
    public ICollection<StudentActivationToken> ActivationTokens { get; set; } = [];
}

public sealed class StudentActivationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class VoterEligibility
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ElectionId { get; set; }
    public Election Election { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public bool IsEligible { get; set; } = true;
    public DateTime ImportedAtUtc { get; set; }
    public string Source { get; set; } = "CSV import";
    public VoterEntitlement? Entitlement { get; set; }
}

public sealed class VoterEntitlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ElectionId { get; set; }
    public Election Election { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid VoterEligibilityId { get; set; }
    public VoterEligibility VoterEligibility { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public string? ConfirmationCodeHash { get; set; }
}
