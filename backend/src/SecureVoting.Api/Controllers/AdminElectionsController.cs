using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Candidates;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Elections;
using SecureVoting.Infrastructure.Students;

namespace SecureVoting.Api.Controllers;

[Route("api/admin/elections")]
[Authorize(Policy = "ManageElections")]
public sealed class AdminElectionsController(
    ElectionReadService electionRead,
    ElectionAdminService electionAdmin,
    CandidateService candidateService,
    StudentImportService studentImport,
    DepartmentAccessService access) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ElectionSummaryDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await electionRead.ListForAdminAsync(CurrentUserId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(
        CreateElectionRequest request,
        CancellationToken cancellationToken)
    {
        var id = await electionAdmin.CreateAsync(request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return CreatedAtAction(nameof(Detail), new { electionId = id }, new { id });
    }

    [HttpGet("{electionId:guid}")]
    public async Task<ActionResult<ElectionDetailDto>> Detail(Guid electionId, CancellationToken cancellationToken)
    {
        await access.EnsureCanManageElectionAsync(CurrentUserId, electionId, cancellationToken);
        return Ok(await electionRead.GetDetailAsync(electionId, false, cancellationToken));
    }

    [HttpPut("{electionId:guid}")]
    public async Task<IActionResult> Update(
        Guid electionId,
        UpdateElectionRequest request,
        CancellationToken cancellationToken)
    {
        await electionAdmin.UpdateAsync(electionId, request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{electionId:guid}/positions")]
    public async Task<ActionResult<object>> AddPosition(
        Guid electionId,
        CreatePositionRequest request,
        CancellationToken cancellationToken)
    {
        var id = await electionAdmin.AddPositionAsync(
            electionId, request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return Created($"/api/admin/elections/{electionId}", new { id });
    }

    [HttpPut("{electionId:guid}/positions/{positionId:guid}")]
    public async Task<IActionResult> UpdatePosition(
        Guid electionId,
        Guid positionId,
        UpdatePositionRequest request,
        CancellationToken cancellationToken)
    {
        await electionAdmin.UpdatePositionAsync(
            electionId, positionId, request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{electionId:guid}/voters")]
    public async Task<ActionResult<IReadOnlyList<EligibleVoterDto>>> Voters(
        Guid electionId,
        CancellationToken cancellationToken)
    {
        await access.EnsureCanManageElectionAsync(CurrentUserId, electionId, cancellationToken);
        return Ok(await electionRead.GetVotersAsync(electionId, cancellationToken));
    }

    [HttpPost("{electionId:guid}/voters/import")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<VoterImportResponse>> ImportVoters(
        Guid electionId,
        [FromForm, Required] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > 2 * 1024 * 1024)
        {
            return BadRequest(new { message = "Upload a non-empty CSV file no larger than 2 MB." });
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only .csv files are accepted." });
        }

        await using var stream = file.OpenReadStream();
        var result = await studentImport.ImportAsync(
            electionId, stream, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{electionId:guid}/candidates")]
    public async Task<ActionResult<IReadOnlyList<CandidateAdminDto>>> Candidates(
        Guid electionId,
        CancellationToken cancellationToken)
    {
        await access.EnsureCanManageElectionAsync(CurrentUserId, electionId, cancellationToken);
        return Ok(await electionRead.GetCandidatesForAdminAsync(electionId, cancellationToken));
    }

    [HttpPost("{electionId:guid}/candidates/{candidateId:guid}/approve")]
    public async Task<IActionResult> ApproveCandidate(
        Guid electionId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        await access.EnsureCanManageElectionAsync(CurrentUserId, electionId, cancellationToken);
        await EnsureCandidateInElectionAsync(electionId, candidateId, cancellationToken);
        await candidateService.ApproveAsync(candidateId, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{electionId:guid}/candidates/{candidateId:guid}/reject")]
    public async Task<IActionResult> RejectCandidate(
        Guid electionId,
        Guid candidateId,
        CandidateDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await access.EnsureCanManageElectionAsync(CurrentUserId, electionId, cancellationToken);
        await EnsureCandidateInElectionAsync(electionId, candidateId, cancellationToken);
        await candidateService.RejectAsync(
            candidateId, CurrentUserId, CurrentActor, request.Reason, CorrelationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{electionId:guid}/candidates/{candidateId:guid}/disqualify")]
    public async Task<IActionResult> DisqualifyCandidate(
        Guid electionId,
        Guid candidateId,
        CandidateDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await access.EnsureCanManageElectionAsync(CurrentUserId, electionId, cancellationToken);
        await EnsureCandidateInElectionAsync(electionId, candidateId, cancellationToken);
        await candidateService.DisqualifyAsync(
            candidateId, CurrentUserId, CurrentActor, request.Reason, CorrelationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{electionId:guid}/actions/{statusAction}")]
    public async Task<IActionResult> ChangeStatus(
        Guid electionId,
        string statusAction,
        ElectionStatusRequest request,
        CancellationToken cancellationToken)
    {
        await electionAdmin.ChangeStatusAsync(
            electionId, statusAction, request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return NoContent();
    }

    private async Task EnsureCandidateInElectionAsync(
        Guid electionId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var candidates = await electionRead.GetCandidatesForAdminAsync(electionId, cancellationToken);
        if (candidates.All(c => c.Id != candidateId))
        {
            throw new NotFoundException("Candidate was not found in this election.");
        }
    }
}
