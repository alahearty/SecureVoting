using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Identity;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Departments;

public sealed class DepartmentAccessService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager)
{
    public async Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && await userManager.IsInRoleAsync(user, RoleNames.SuperAdministrator);
    }

    public async Task<IReadOnlySet<Guid>> GetAssignedDepartmentIdsAsync(
        Guid userId,
        DepartmentAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        return await db.DepartmentAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Scope == scope)
            .Select(a => a.DepartmentId)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetAllAssignedDepartmentIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.DepartmentAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.DepartmentId)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<bool> HasAssignedDepartmentAsync(
        Guid userId,
        Guid departmentId,
        DepartmentAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        return await db.DepartmentAssignments
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.DepartmentId == departmentId && a.Scope == scope,
                cancellationToken);
    }

    public async Task<bool> HasAnyDepartmentAssignmentAsync(
        Guid userId,
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        return await db.DepartmentAssignments
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.DepartmentId == departmentId,
                cancellationToken);
    }

    public async Task EnsureCanManageDepartmentAsync(
        Guid userId,
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null ||
            !await userManager.IsInRoleAsync(user, RoleNames.ElectoralOfficer) ||
            !await HasAssignedDepartmentAsync(userId, departmentId, DepartmentAccessScope.ElectoralOfficer,
                cancellationToken))
        {
            throw new ForbiddenOperationException("You are not authorised to manage elections for this department.");
        }
    }

    public async Task EnsureCanManageElectionAsync(
        Guid userId,
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return;
        }

        var departmentId = await db.Elections
            .AsNoTracking()
            .Where(e => e.Id == electionId)
            .Select(e => (Guid?)e.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            throw new NotFoundException("Election was not found.");
        }

        await EnsureCanManageDepartmentAsync(userId, departmentId.Value, cancellationToken);
    }

    public async Task EnsureCanObserveElectionAsync(
        Guid userId,
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return;
        }

        var departmentId = await db.Elections
            .AsNoTracking()
            .Where(e => e.Id == electionId)
            .Select(e => (Guid?)e.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            throw new NotFoundException("Election was not found.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null ||
            !await userManager.IsInRoleAsync(user, RoleNames.Observer) ||
            !await HasAssignedDepartmentAsync(userId, departmentId.Value, DepartmentAccessScope.Observer,
                cancellationToken))
        {
            throw new ForbiddenOperationException("You are not authorised to observe this election.");
        }
    }

    public async Task EnsureCanViewElectionAsync(
        Guid userId,
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return;
        }

        var departmentId = await db.Elections
            .AsNoTracking()
            .Where(e => e.Id == electionId)
            .Select(e => (Guid?)e.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            throw new NotFoundException("Election was not found.");
        }

        if (!await HasAnyDepartmentAssignmentAsync(userId, departmentId.Value, cancellationToken))
        {
            throw new ForbiddenOperationException("You are not authorised to access this election.");
        }
    }

    public async Task EnsureCanViewPublishedResultAsync(
        Guid userId,
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return;
        }

        var departmentId = await db.Elections
            .AsNoTracking()
            .Where(e => e.Id == electionId)
            .Select(e => (Guid?)e.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            throw new NotFoundException("Election was not found.");
        }

        if (await HasAnyDepartmentAssignmentAsync(userId, departmentId.Value, cancellationToken))
        {
            return;
        }

        var studentId = await db.Students
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .Select(s => (Guid?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (studentId is not null &&
            await db.VoterEligibility.AnyAsync(
                v => v.ElectionId == electionId && v.StudentId == studentId.Value && v.IsEligible,
                cancellationToken))
        {
            return;
        }

        throw new ForbiddenOperationException("You are not authorised to view this result.");
    }

    public async Task<bool> HasElectionViewAccessAsync(
        Guid userId,
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return true;
        }

        var departmentId = await db.Elections
            .AsNoTracking()
            .Where(e => e.Id == electionId)
            .Select(e => (Guid?)e.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            return false;
        }

        if (await HasAnyDepartmentAssignmentAsync(userId, departmentId.Value, cancellationToken))
        {
            return true;
        }

        var studentId = await db.Students
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .Select(s => (Guid?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return studentId is not null &&
               await db.VoterEligibility.AnyAsync(
                   v => v.ElectionId == electionId && v.StudentId == studentId.Value && v.IsEligible,
                   cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetStudentEligibleElectionIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.VoterEligibility
            .AsNoTracking()
            .Where(v => v.Student.UserId == userId && v.Student.IsActive && v.IsEligible)
            .Select(v => v.ElectionId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetAssignedElectionIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var departments = await GetAllAssignedDepartmentIdsAsync(userId, cancellationToken);
        if (departments.Count == 0)
        {
            return [];
        }

        return await db.Elections
            .AsNoTracking()
            .Where(e => departments.Contains(e.DepartmentId))
            .Select(e => e.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task EnsureCanManageAnnouncementAsync(
        Guid userId,
        Guid? electionId,
        CancellationToken cancellationToken = default)
    {
        if (electionId is null)
        {
            if (await IsSuperAdminAsync(userId, cancellationToken))
            {
                return;
            }

            throw new ForbiddenOperationException("Only a university administrator may publish an announcement without an election scope.");
        }

        await EnsureCanManageElectionAsync(userId, electionId.Value, cancellationToken);
    }
}
