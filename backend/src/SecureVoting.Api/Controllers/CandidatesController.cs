using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Abstractions;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Api.Controllers;

[Route("api/candidates")]
[Authorize]
public sealed class CandidatesController(
    AppDbContext db,
    ICandidatePhotoStore photoStore,
    DepartmentAccessService access) : ApiControllerBase
{
    [HttpGet("{candidateId:guid}/photo")]
    public async Task<IActionResult> Photo(Guid candidateId, CancellationToken cancellationToken)
    {
        var candidate = await db.Candidates.AsNoTracking()
            .Include(c => c.Student)
            .Include(c => c.Election)
            .SingleOrDefaultAsync(c => c.Id == candidateId, cancellationToken);
        if (candidate?.PhotoFileName is null)
        {
            return NotFound();
        }

        var isOwner = candidate.Student.UserId == CurrentUserId;
        var canRead = isOwner || await access.HasElectionViewAccessAsync(
            CurrentUserId, candidate.ElectionId, cancellationToken);
        if (candidate.Status != CandidateStatus.Approved && !canRead)
        {
            return NotFound();
        }

        if (!canRead)
        {
            return NotFound();
        }

        var result = await photoStore.OpenAsync(candidate.PhotoFileName, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=300";
        return File(result.Value.Stream, result.Value.ContentType);
    }
}
