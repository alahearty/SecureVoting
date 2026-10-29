using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Elections;

namespace SecureVoting.Api.Controllers;

[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(ElectionReadService electionRead) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await electionRead.GetNotificationsAsync(CurrentUserId, cancellationToken));
    }
}
