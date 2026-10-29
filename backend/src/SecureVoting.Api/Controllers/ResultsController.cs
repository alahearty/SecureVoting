using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Results;

namespace SecureVoting.Api.Controllers;

[Route("api/elections/{electionId:guid}/results")]
[Authorize]
public sealed class ResultsController(
    ResultService resultService,
    DepartmentAccessService access) : ApiControllerBase
{
    [HttpGet("published")]
    public async Task<ActionResult<ElectionResultDto>> Published(Guid electionId, CancellationToken cancellationToken)
    {
        await access.EnsureCanViewPublishedResultAsync(CurrentUserId, electionId, cancellationToken);
        var result = await resultService.GetPublishedAsync(electionId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("published/export.csv")]
    public async Task<IActionResult> ExportPublished(Guid electionId, CancellationToken cancellationToken)
    {
        await access.EnsureCanViewPublishedResultAsync(CurrentUserId, electionId, cancellationToken);
        var csv = await resultService.ExportCsvAsync(electionId, publishedOnly: true, cancellationToken);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8",
            $"securevote-results-{electionId:N}.csv");
    }

    [HttpGet("latest")]
    [Authorize(Policy = "ViewUnpublishedResults")]
    public async Task<ActionResult<ElectionResultDto>> Latest(Guid electionId, CancellationToken cancellationToken)
    {
        await access.EnsureCanViewElectionAsync(CurrentUserId, electionId, cancellationToken);
        var result = await resultService.GetLatestForPrivilegedAsync(electionId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("latest/export.csv")]
    [Authorize(Policy = "ViewUnpublishedResults")]
    public async Task<IActionResult> ExportLatest(Guid electionId, CancellationToken cancellationToken)
    {
        await access.EnsureCanViewElectionAsync(CurrentUserId, electionId, cancellationToken);
        var csv = await resultService.ExportCsvAsync(electionId, publishedOnly: false, cancellationToken);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8",
            $"securevote-results-{electionId:N}.csv");
    }

    [HttpGet("versions")]
    [Authorize(Policy = "ViewUnpublishedResults")]
    public async Task<ActionResult<IReadOnlyList<int>>> Versions(Guid electionId, CancellationToken cancellationToken)
    {
        await access.EnsureCanViewElectionAsync(CurrentUserId, electionId, cancellationToken);
        return Ok(await resultService.GetVersionsAsync(electionId, cancellationToken));
    }

    [HttpPost("review")]
    [Authorize(Policy = "ReviewResults")]
    public async Task<ActionResult<ElectionResultDto>> RequestReview(
        Guid electionId,
        CancellationToken cancellationToken)
    {
        await access.EnsureCanManageElectionAsync(CurrentUserId, electionId, cancellationToken);
        return Ok(await resultService.RequestReviewAsync(
            electionId, CurrentUserId, CurrentActor, CorrelationId, cancellationToken));
    }

    [HttpPost("{resultId:guid}/verify")]
    [Authorize(Policy = "VerifyResults")]
    public async Task<ActionResult<ElectionResultDto>> Verify(
        Guid electionId,
        Guid resultId,
        VerifyResultRequest request,
        CancellationToken cancellationToken)
    {
        await access.EnsureCanObserveElectionAsync(CurrentUserId, electionId, cancellationToken);
        return Ok(await resultService.VerifyAsync(
            electionId, resultId, CurrentUserId, CurrentActor, request.Notes, cancellationToken));
    }

    [HttpPost("{resultId:guid}/publish")]
    [Authorize(Policy = "PublishResults")]
    public async Task<ActionResult<ElectionResultDto>> Publish(
        Guid electionId,
        Guid resultId,
        ResultDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await resultService.PublishAsync(
            electionId, resultId, CurrentUserId, CurrentActor, request.Notes, cancellationToken));
    }

    [HttpPost("recount")]
    [Authorize(Policy = "PublishResults")]
    public async Task<ActionResult<ElectionResultDto>> Recount(
        Guid electionId,
        RecountRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await resultService.RecountAsync(
            electionId, CurrentUserId, CurrentActor, request.Reason, cancellationToken));
    }
}
