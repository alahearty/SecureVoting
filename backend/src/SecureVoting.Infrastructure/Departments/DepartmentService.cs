using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Identity;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Departments;

public sealed class DepartmentService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    DepartmentAccessService access,
    AuditWriter auditWriter,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<DepartmentDto>> ListActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (await access.IsSuperAdminAsync(userId, cancellationToken))
        {
            return (await ActiveQuery().OrderBy(d => d.Name).ToListAsync(cancellationToken))
                .Select(Map).ToArray();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return [];
        }

        if (await userManager.IsInRoleAsync(user, RoleNames.Student))
        {
            return (await db.Students.AsNoTracking()
                    .Where(s => s.UserId == userId && s.IsActive && s.DepartmentRef.IsActive)
                    .Select(s => s.DepartmentRef)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .Select(Map).ToArray();
        }

        var assigned = await access.GetAllAssignedDepartmentIdsAsync(userId, cancellationToken);
        if (assigned.Count == 0)
        {
            return [];
        }

        return (await ActiveQuery()
                .Where(d => assigned.Contains(d.Id))
                .OrderBy(d => d.Name)
                .ToListAsync(cancellationToken))
            .Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<DepartmentDto>> ListAllAsync(
        CancellationToken cancellationToken = default)
    {
        return (await db.Departments.AsNoTracking()
                .OrderBy(d => d.Name)
                .ToListAsync(cancellationToken))
            .Select(Map).ToArray();
    }

    public async Task<DepartmentDto> CreateAsync(
        CreateDepartmentRequest request,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim();
        var normalizedCode = NormalizeCode(code);
        Validate(name, normalizedCode);
        if (await db.Departments.AnyAsync(d => d.CodeNormalized == normalizedCode, cancellationToken))
        {
            throw new ConflictException("A department with this code already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var department = new Department
        {
            Name = name,
            Code = code,
            CodeNormalized = normalizedCode,
            Faculty = string.IsNullOrWhiteSpace(request.Faculty) ? null : request.Faculty.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Departments.Add(department);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "DepartmentCreated",
            nameof(Department),
            department.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { name = department.Name, code = department.Code },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(department);
    }

    public async Task<DepartmentDto> UpdateAsync(
        Guid departmentId,
        UpdateDepartmentRequest request,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim();
        var normalizedCode = NormalizeCode(code);
        Validate(name, normalizedCode);
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            ?? throw new NotFoundException("Department was not found.");
        if (await db.Departments.AnyAsync(
                d => d.Id != departmentId && d.CodeNormalized == normalizedCode, cancellationToken))
        {
            throw new ConflictException("A department with this code already exists.");
        }

        department.Name = name;
        department.Code = code;
        department.CodeNormalized = normalizedCode;
        department.Faculty = string.IsNullOrWhiteSpace(request.Faculty) ? null : request.Faculty.Trim();
        department.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "DepartmentUpdated",
            nameof(Department),
            department.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { name = department.Name, code = department.Code, faculty = department.Faculty },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(department);
    }

    public async Task SetActiveAsync(
        Guid departmentId,
        bool active,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            ?? throw new NotFoundException("Department was not found.");
        if (department.IsActive == active)
        {
            return;
        }

        department.IsActive = active;
        department.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            active ? "DepartmentActivated" : "DepartmentDeactivated",
            nameof(Department),
            department.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { active, code = department.Code },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DepartmentAssignmentDto>> GetAssignmentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            throw new NotFoundException("User was not found.");
        }

        return await db.DepartmentAssignments.AsNoTracking()
            .Include(a => a.Department)
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Department.Name)
            .ThenBy(a => a.Scope)
            .Select(a => new DepartmentAssignmentDto(
                a.DepartmentId,
                a.Department.Name,
                a.Department.Code,
                a.Scope))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DepartmentAssignmentDto>> UpdateAssignmentsAsync(
        Guid userId,
        UpdateDepartmentAssignmentsRequest request,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User was not found.");
        var assignments = request.Assignments
            .GroupBy(a => new { a.DepartmentId, a.Scope })
            .Select(g => g.First())
            .ToArray();
        var departmentIds = assignments.Select(a => a.DepartmentId).Distinct().ToArray();
        var departments = await db.Departments
            .Where(d => departmentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);
        if (departments.Count != departmentIds.Length)
        {
            throw new ValidationException("One or more departments could not be found.");
        }

        if (assignments.Any(a => a.Scope == DepartmentAccessScope.ElectoralOfficer) &&
            !await userManager.IsInRoleAsync(user, RoleNames.ElectoralOfficer))
        {
            throw new ValidationException("A user must have the ElectoralOfficer role before receiving an officer scope.");
        }

        if (assignments.Any(a => a.Scope == DepartmentAccessScope.Observer) &&
            !await userManager.IsInRoleAsync(user, RoleNames.Observer))
        {
            throw new ValidationException("A user must have the Observer role before receiving an observer scope.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await db.DepartmentAssignments
                .Where(a => a.UserId == userId)
                .ToListAsync(cancellationToken);
            db.DepartmentAssignments.RemoveRange(existing);
            foreach (var assignment in assignments)
            {
                db.DepartmentAssignments.Add(new DepartmentAssignment
                {
                    UserId = userId,
                    DepartmentId = assignment.DepartmentId,
                    Scope = assignment.Scope,
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                "DepartmentAssignmentsChanged",
                "User",
                userId.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                correlationId,
                new { assignments = assignments.Select(a => new { a.DepartmentId, scope = a.Scope.ToString() }) },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await userManager.UpdateSecurityStampAsync(user);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await GetAssignmentsAsync(userId, cancellationToken);
    }

    private IQueryable<Department> ActiveQuery() =>
        db.Departments.AsNoTracking().Where(d => d.IsActive);

    private static DepartmentDto Map(Department department) =>
        new(department.Id, department.Name, department.Code, department.Faculty, department.IsActive,
            department.CreatedAtUtc, department.UpdatedAtUtc);

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static void Validate(string name, string normalizedCode)
    {
        if (name.Length is < 2 or > 160)
        {
            throw new ValidationException("Department name must be 2-160 characters.");
        }

        if (normalizedCode.Length is < 2 or > 20 ||
            normalizedCode.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_')))
        {
            throw new ValidationException("Department code must be 2-20 letters, digits, hyphens or underscores.");
        }
    }
}
