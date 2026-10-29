namespace SecureVoting.Domain;

public static class RoleNames
{
    public const string SuperAdministrator = "SuperAdministrator";
    public const string ElectoralOfficer = "ElectoralOfficer";
    public const string Student = "Student";
    public const string Candidate = "Candidate";
    public const string Observer = "Observer";

    public static readonly string[] All =
    [
        SuperAdministrator,
        ElectoralOfficer,
        Student,
        Candidate,
        Observer
    ];
}

public enum ElectionStatus
{
    Draft,
    Scheduled,
    Open,
    Closed,
    UnderReview,
    Published,
    Suspended,
    Cancelled
}

public enum CandidateStatus
{
    Pending,
    Approved,
    Rejected,
    Withdrawn,
    Disqualified
}

public enum TieHandling
{
    RunoffRequired,
    SharedOffice,
    ManualResolution
}

public enum ResultStatus
{
    Provisional,
    Verified,
    Published,
    Superseded
}

public enum ApprovalType
{
    ObserverVerification,
    PublicationApproval
}

public enum ApprovalDecision
{
    Approved,
    Rejected
}

public enum AuditOutcome
{
    Success,
    Failure,
    Denied
}

public enum NotificationChannel
{
    InApp,
    Email
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
    Skipped
}

public enum AnnouncementAudience
{
    Everyone,
    EligibleVoters,
    Candidates,
    Observers
}
