using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Announcements;

namespace SecureVoting.Api.Controllers;

[Route("api/admin/announcements")]
[Authorize(Policy = "ManageAnnouncements")]
public sealed class AdminAnnouncementsController(AnnouncementService announcements) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var id = await announcements.CreateAsync(
            request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return Created($"/api/announcements", new { id });
    }
}
