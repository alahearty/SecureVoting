using System.ComponentModel.DataAnnotations;
using SecureVoting.Domain;

namespace SecureVoting.Application.Contracts;

public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string Code,
    string? Faculty,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateDepartmentRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [StringLength(160)]
    public string? Faculty { get; set; }
}

public sealed class UpdateDepartmentRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [StringLength(160)]
    public string? Faculty { get; set; }
}

public sealed record DepartmentAssignmentDto(
    Guid DepartmentId,
    string DepartmentName,
    string DepartmentCode,
    DepartmentAccessScope Scope);

public sealed class DepartmentAssignmentRequest
{
    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public DepartmentAccessScope Scope { get; set; }
}

public sealed class UpdateDepartmentAssignmentsRequest
{
    [Required]
    public List<DepartmentAssignmentRequest> Assignments { get; set; } = [];
}
