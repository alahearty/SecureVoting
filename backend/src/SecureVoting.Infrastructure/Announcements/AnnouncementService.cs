using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Identity;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Announcements;

public sealed class AnnouncementService(
    AppDbContext db,
    AuditWriter auditWriter,
    TimeProvider timeProvider,
    DepartmentAccessService access)
{
    public async Task<Guid> CreateAsync(
        CreateAnnouncementRequest request,
        Guid actorUserId,
        string actorDisplay,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        await access.EnsureCanManageAnnouncementAsync(actorUserId, request.ElectionId, cancellationToken);
        Election? election = null;
        if (request.ElectionId is not null)
        {
            election = await db.Elections.SingleOrDefaultAsync(e => e.Id == request.ElectionId, cancellationToken)
                ?? throw new NotFoundException("Election was not found.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var announcement = new Announcement
        {
            ElectionId = election?.Id,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Audience = request.Audience,
            PublishedAtUtc = now,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(cancellationToken);
        var recipients = await RecipientsAsync(request.Audience, election?.Id, cancellationToken);
        foreach (var user in recipients)
        {
            db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                ElectionId = election?.Id,
                Type = "Announcement",
                Subject = announcement.Title,
                Body = announcement.Body,
                Channel = NotificationChannel.InApp,
                Status = NotificationStatus.Sent,
                CreatedAtUtc = now,
                SentAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "AnnouncementPublished",
            nameof(Announcement),
            announcement.Id.ToString(),
            AuditOutcome.Success,
            actorUserId,
            actorDisplay,
            correlationId,
            new { electionId = election?.Id, audience = request.Audience.ToString(), recipients = recipients.Count },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return announcement.Id;
    }

    public async Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Notification was not found.");
        notification.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<ApplicationUser>> RecipientsAsync(
        AnnouncementAudience audience,
        Guid? electionId,
        CancellationToken cancellationToken)
    {
        var activeUsers = await db.Users.AsNoTracking().Where(u => u.IsActive).ToListAsync(cancellationToken);
        Guid? departmentId = null;
        if (electionId is not null)
        {
            departmentId = await db.Elections.AsNoTracking()
                .Where(e => e.Id == electionId)
                .Select(e => (Guid?)e.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (audience == AnnouncementAudience.Everyone)
        {
            if (departmentId is null)
            {
                return activeUsers;
            }

            var eligibleUserIds = await db.VoterEligibility.AsNoTracking()
                .Where(v => v.ElectionId == electionId && v.IsEligible && v.Student.UserId != null)
                .Select(v => v.Student.UserId!.Value)
                .ToListAsync(cancellationToken);
            var staffUserIds = await db.DepartmentAssignments.AsNoTracking()
                .Where(a => a.DepartmentId == departmentId)
                .Select(a => a.UserId)
                .ToListAsync(cancellationToken);
            var superAdminIds = (await UsersInRoleAsync(RoleNames.SuperAdministrator, activeUsers, cancellationToken))
                .Select(u => u.Id);
            var allowed = eligibleUserIds.Concat(staffUserIds).Concat(superAdminIds).ToHashSet();
            return activeUsers.Where(u => allowed.Contains(u.Id)).ToList();
        }

        if (audience == AnnouncementAudience.Observers)
        {
            var observers = await UsersInRoleAsync(RoleNames.Observer, activeUsers, cancellationToken);
            if (departmentId is null)
            {
                return observers;
            }

            var assigned = await db.DepartmentAssignments.AsNoTracking()
                .Where(a => a.DepartmentId == departmentId && a.Scope == DepartmentAccessScope.Observer)
                .Select(a => a.UserId)
                .ToListAsync(cancellationToken);
            return observers.Where(u => assigned.Contains(u.Id)).ToList();
        }

        if (audience == AnnouncementAudience.Candidates)
        {
            var candidates = db.Candidates.AsNoTracking()
                .Where(c => c.Status == CandidateStatus.Approved);
            if (electionId is not null)
            {
                candidates = candidates.Where(c => c.ElectionId == electionId);
            }

            var candidateUserIds = await candidates
                .Select(c => c.Student.UserId)
                .Where(id => id != null)
                .Select(id => id!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            return activeUsers.Where(u => candidateUserIds.Contains(u.Id)).ToList();
        }

        var studentQuery = db.Students.AsNoTracking().Where(s => s.IsActive && s.UserId != null);
        if (electionId is not null)
        {
            var eligibleStudentIds = db.VoterEligibility
                .Where(v => v.ElectionId == electionId && v.IsEligible)
                .Select(v => v.StudentId);
            studentQuery = studentQuery.Where(s => eligibleStudentIds.Contains(s.Id));
        }

        var studentUserIds = await studentQuery.Select(s => s.UserId!.Value).ToListAsync(cancellationToken);
        return activeUsers.Where(u => studentUserIds.Contains(u.Id)).ToList();
    }

    private async Task<List<ApplicationUser>> UsersInRoleAsync(
        string roleName,
        IReadOnlyCollection<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var userIds = await db.UserRoles
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .Where(x => x.Name == roleName)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        return users.Where(u => userIds.Contains(u.Id)).ToList();
    }
}