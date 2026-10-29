using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Settings;

namespace SecureVoting.Api.Controllers;

[Route("api/admin/settings")]
[Authorize(Policy = "ManageSettings")]
public sealed class AdminSettingsController(SettingsService settings) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await settings.ListAsync(cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateSettingRequest request, CancellationToken cancellationToken)
    {
        await settings.UpdateAsync(CurrentUserId, CurrentActor, request, cancellationToken);
        return NoContent();
    }
}
