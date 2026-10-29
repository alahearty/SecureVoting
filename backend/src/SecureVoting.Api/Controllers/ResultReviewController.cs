using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Elections;

namespace SecureVoting.Api.Controllers;

[Route("api/result-review")]
[Authorize(Policy = "ViewUnpublishedResults")]
public sealed class ResultReviewController(
    ElectionReadService electionRead,
    DepartmentAccessService access) : ApiControllerBase
{
    [HttpGet("elections")]
    public async Task<ActionResult<IReadOnlyList<ElectionSummaryDto>>> Elections(CancellationToken cancellationToken)
    {
        return Ok(await electionRead.ListForReviewAsync(CurrentUserId, cancellationToken));
    }

    [HttpGet("elections/{electionId:guid}")]
    public async Task<ActionResult<ElectionDetailDto>> Election(Guid electionId, CancellationToken cancellationToken)
    {
        await access.EnsureCanViewElectionAsync(CurrentUserId, electionId, cancellationToken);
        return Ok(await electionRead.GetDetailAsync(electionId, false, cancellationToken));
    }
}
