namespace SecureVoting.Domain;

public sealed class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CodeNormalized { get; set; } = string.Empty;
    public string? Faculty { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<Student> Students { get; set; } = [];
    public ICollection<Election> Elections { get; set; } = [];
    public ICollection<DepartmentAssignment> Assignments { get; set; } = [];
}

public sealed class DepartmentAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public DepartmentAccessScope Scope { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public enum DepartmentAccessScope
{
    ElectoralOfficer,
    Observer
}
