using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Identity;

public sealed class UserAdminService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    AuditWriter auditWriter,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<UserSummaryDto>> ListAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            users = users.Where(u =>
                u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (u.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (u.UserName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }

        var studentLinks = await db.Students.AsNoTracking()
            .Where(s => s.UserId != null)
            .ToDictionaryAsync(s => s.UserId!.Value, s => s.MatricNumber, cancellationToken);
        var roleMap = new Dictionary<Guid, List<string>>();
        foreach (var user in users)
        {
            roleMap[user.Id] = (await userManager.GetRolesAsync(user)).ToList();
        }

        var now = timeProvider.GetUtcNow();
        return users.Select(user => new UserSummaryDto(
            user.Id,
            user.DisplayName,
            user.Email,
            studentLinks.GetValueOrDefault(user.Id),
            user.IsActive,
            user.LockoutEnd is not null && user.LockoutEnd > now,
            roleMap.GetValueOrDefault(user.Id, []),
            user.CreatedAtUtc)).ToArray();
    }

    public async Task AssignRolesAsync(
        Guid targetUserId,
        AssignRolesRequest request,
        Guid actorUserId,
        string actorDisplay,
        CancellationToken cancellationToken = default)
    {
        var requested = request.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var invalid = requested.Where(r => !RoleNames.All.Contains(r, StringComparer.Ordinal)).ToArray();
        if (invalid.Length > 0)
        {
            throw new ValidationException($"Unknown role(s): {string.Join(", ", invalid)}.");
        }

        foreach (var role in requested)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                throw new ValidationException($"Role {role} is not configured.");
            }
        }

        var user = await userManager.FindByIdAsync(targetUserId.ToString())
            ?? throw new NotFoundException("User was not found.");
        var current = await userManager.GetRolesAsync(user);
        var removingSuperAdmin = current.Contains(RoleNames.SuperAdministrator) &&
                                 !requested.Contains(RoleNames.SuperAdministrator, StringComparer.Ordinal);
        if (removingSuperAdmin)
        {
            var superAdmins = await userManager.GetUsersInRoleAsync(RoleNames.SuperAdministrator);
            if (superAdmins.Count <= 1)
            {
                throw new ConflictException("The final Super Administrator cannot lose that role.");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var toRemove = current.Except(requested, StringComparer.Ordinal).ToArray();
            var toAdd = requested.Except(current, StringComparer.Ordinal).ToArray();
            if (toRemove.Length > 0)
            {
                var result = await userManager.RemoveFromRolesAsync(user, toRemove);
                if (!result.Succeeded)
                {
                    throw new ConflictException(string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }

            if (toAdd.Length > 0)
            {
                var result = await userManager.AddToRolesAsync(user, toAdd);
                if (!result.Succeeded)
                {
                    throw new ConflictException(string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }

            if (!requested.Contains(RoleNames.ElectoralOfficer, StringComparer.Ordinal))
            {
                var officerAssignments = await db.DepartmentAssignments
                    .Where(a => a.UserId == user.Id && a.Scope == DepartmentAccessScope.ElectoralOfficer)
                    .ToListAsync(cancellationToken);
                db.DepartmentAssignments.RemoveRange(officerAssignments);
            }

            if (!requested.Contains(RoleNames.Observer, StringComparer.Ordinal))
            {
                var observerAssignments = await db.DepartmentAssignments
                    .Where(a => a.UserId == user.Id && a.Scope == DepartmentAccessScope.Observer)
                    .ToListAsync(cancellationToken);
                db.DepartmentAssignments.RemoveRange(observerAssignments);
            }

            await userManager.UpdateAsync(user);
            await userManager.UpdateSecurityStampAsync(user);
            await auditWriter.AppendAsync(
                db,
                "UserRolesChanged",
                "User",
                user.Id.ToString(),
                AuditOutcome.Success,
                actorUserId,
                actorDisplay,
                null,
                new { added = toAdd, removed = toRemove },
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SetActiveAsync(
        Guid targetUserId,
        bool active,
        string reason,
        Guid actorUserId,
        string actorDisplay,
        CancellationToken cancellationToken = default)
    {
        if (reason.Trim().Length < 5)
        {
            throw new ValidationException("A reason is required.");
        }

        var user = await userManager.FindByIdAsync(targetUserId.ToString())
            ?? throw new NotFoundException("User was not found.");
        if (user.Id == actorUserId && !active)
        {
            throw new ConflictException("You cannot suspend your own account.");
        }

        if (!active)
        {
            var superAdmins = await userManager.GetUsersInRoleAsync(RoleNames.SuperAdministrator);
            if (superAdmins.Any(x => x.Id == user.Id) && superAdmins.Count <= 1)
            {
                throw new ConflictException("The final Super Administrator cannot be suspended.");
            }
        }

        user.IsActive = active;
        if (!active)
        {
            user.LockoutEnd = DateTimeOffset.MaxValue;
        }
        else
        {
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
        await auditWriter.AppendAsync(
            db,
            active ? "UserActivated" : "UserSuspended",
            "User",
            user.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            null,
            new { active, reason = reason.Trim() },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? [] : (await userManager.GetRolesAsync(user)).ToArray();
    }
}
