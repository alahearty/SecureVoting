namespace SecureVoting.Domain;

public sealed class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ElectionId { get; set; }
    public Election? Election { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.Everyone;
    public DateTime? PublishedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? ElectionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? ErrorSummary { get; set; }
}

public sealed class SystemSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Sequence { get; set; }
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorDisplay { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public AuditOutcome Outcome { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public string PreviousHash { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AuditChainState
{
    public int Id { get; set; } = 1;
    public long LastSequence { get; set; }
    public string LastHash { get; set; } = new('0', 64);
    public DateTime UpdatedAtUtc { get; set; }
}
