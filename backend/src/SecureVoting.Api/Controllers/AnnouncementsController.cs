using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Announcements;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Elections;

namespace SecureVoting.Api.Controllers;

[Route("api/announcements")]
[Authorize]
public sealed class AnnouncementsController(
    ElectionReadService electionRead,
    AnnouncementService announcementService,
    DepartmentAccessService access) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> List(CancellationToken cancellationToken)
    {
        var audiences = new List<AnnouncementAudience> { AnnouncementAudience.Everyone };
        if (User.IsInRole(RoleNames.Student))
        {
            audiences.Add(AnnouncementAudience.EligibleVoters);
        }

        if (User.IsInRole(RoleNames.Candidate))
        {
            audiences.Add(AnnouncementAudience.Candidates);
        }

        if (User.IsInRole(RoleNames.Observer))
        {
            audiences.Add(AnnouncementAudience.Observers);
        }

        IReadOnlyCollection<Guid>? allowedElectionIds = null;
        if (!User.IsInRole(RoleNames.SuperAdministrator))
        {
            allowedElectionIds = User.IsInRole(RoleNames.Student) || User.IsInRole(RoleNames.Candidate)
                ? await access.GetStudentEligibleElectionIdsAsync(CurrentUserId, cancellationToken)
                : await access.GetAssignedElectionIdsAsync(CurrentUserId, cancellationToken);
        }

        if (User.IsInRole(RoleNames.ElectoralOfficer))
        {
            return Ok(await electionRead.GetAnnouncementsAsync(
                null, null, cancellationToken, allowedElectionIds));
        }

        var results = new Dictionary<Guid, AnnouncementDto>();
        foreach (var audience in audiences.Distinct())
        {
            foreach (var item in await electionRead.GetAnnouncementsAsync(
                         null, audience, cancellationToken, allowedElectionIds))
            {
                results[item.Id] = item;
            }
        }

        return Ok(results.Values.OrderByDescending(a => a.PublishedAt).ToArray());
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await announcementService.MarkReadAsync(notificationId, CurrentUserId, cancellationToken);
        return NoContent();
    }
}
