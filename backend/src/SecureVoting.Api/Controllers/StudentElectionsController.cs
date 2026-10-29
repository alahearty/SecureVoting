using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Ballots;
using SecureVoting.Infrastructure.Elections;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Api.Controllers;

[Route("api/student/elections")]
[Authorize(Policy = "StudentPortal")]
public sealed class StudentElectionsController(
    AppDbContext db,
    ElectionReadService electionRead,
    BallotService ballotService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ElectionSummaryDto>>> List(CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsync(cancellationToken);
        return Ok(await electionRead.ListForStudentAsync(student.Id, cancellationToken));
    }

    [HttpGet("{electionId:guid}")]
    public async Task<ActionResult<ElectionDetailDto>> Detail(Guid electionId, CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsync(cancellationToken);
        var eligible = await db.VoterEligibility.AsNoTracking().AnyAsync(
            v => v.ElectionId == electionId && v.StudentId == student.Id && v.IsEligible,
            cancellationToken);
        if (!eligible)
        {
            throw new ForbiddenOperationException("You are not eligible for this election.");
        }

        return Ok(await electionRead.GetDetailAsync(electionId, false, cancellationToken));
    }

    [HttpGet("{electionId:guid}/ballot")]
    public async Task<ActionResult<BallotDto>> Ballot(Guid electionId, CancellationToken cancellationToken)
    {
        return Ok(await ballotService.GetBallotAsync(electionId, CurrentUserId, cancellationToken));
    }

    [HttpPost("{electionId:guid}/ballots")]
    [EnableRateLimiting("ballot")]
    public async Task<ActionResult<BallotAcceptedDto>> Submit(
        Guid electionId,
        CastBallotRequest request,
        CancellationToken cancellationToken)
    {
        var accepted = await ballotService.SubmitAsync(
            electionId,
            CurrentUserId,
            request,
            CorrelationId,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, accepted);
    }

    [HttpGet("{electionId:guid}/vote-status")]
    public async Task<ActionResult<VoteStatusDto>> VoteStatus(Guid electionId, CancellationToken cancellationToken)
    {
        return Ok(await ballotService.GetStatusAsync(electionId, CurrentUserId, cancellationToken));
    }

    private async Task<Student> CurrentStudentAsync(CancellationToken cancellationToken)
    {
        return await db.Students.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == CurrentUserId && s.IsActive, cancellationToken)
            ?? throw new ForbiddenOperationException("An active student profile is required.");
    }
}
