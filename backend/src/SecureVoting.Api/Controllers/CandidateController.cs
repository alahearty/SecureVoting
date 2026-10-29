using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Abstractions;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Candidates;
using SecureVoting.Infrastructure.Elections;
using SecureVoting.Infrastructure.Persistence;
using SecureVoting.Domain;

namespace SecureVoting.Api.Controllers;

[Route("api/candidate")]
[Authorize(Policy = "CandidatePortal")]
public sealed class CandidateController(
    CandidateService candidateService,
    ElectionReadService electionRead,
    ICandidatePhotoStore photoStore,
    AppDbContext db) : ApiControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<CandidateProfileDto?>> Profile(CancellationToken cancellationToken)
    {
        return Ok(await candidateService.GetMyNominationAsync(CurrentUserId, cancellationToken));
    }

    [HttpGet("elections")]
    public async Task<ActionResult<IReadOnlyList<ElectionSummaryDto>>> Elections(CancellationToken cancellationToken)
    {
        var student = await GetStudentIdAsync(cancellationToken);
        return Ok(await electionRead.ListForStudentAsync(student, cancellationToken));
    }

    [HttpGet("elections/{electionId:guid}")]
    public async Task<ActionResult<ElectionDetailDto>> Election(Guid electionId, CancellationToken cancellationToken)
    {
        var studentId = await GetStudentIdAsync(cancellationToken);
        var eligible = await db.VoterEligibility.AsNoTracking().AnyAsync(
            v => v.ElectionId == electionId && v.StudentId == studentId && v.IsEligible,
            cancellationToken);
        if (!eligible)
        {
            throw new ForbiddenOperationException("You are not eligible for this election.");
        }

        return Ok(await electionRead.GetDetailAsync(electionId, false, cancellationToken));
    }

    [HttpPost("nominations")]
    public async Task<ActionResult<object>> Nominate(
        CandidateNominationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await candidateService.NominateAsync(CurrentUserId, request, CorrelationId, cancellationToken);
        return Created($"/api/candidate/profile", new { id });
    }

    [HttpPut("nominations/{candidateId:guid}")]
    public async Task<IActionResult> Update(
        Guid candidateId,
        UpdateCandidateProfileRequest request,
        CancellationToken cancellationToken)
    {
        await candidateService.UpdateMyProfileAsync(candidateId, CurrentUserId, request, CorrelationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("nominations/{candidateId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(
        Guid candidateId,
        CandidateWithdrawalRequest request,
        CancellationToken cancellationToken)
    {
        await candidateService.WithdrawAsync(candidateId, CurrentUserId, request, CorrelationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("nominations/{candidateId:guid}/photo")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(
        Guid candidateId,
        [FromForm, Required] IFormFile photo,
        CancellationToken cancellationToken)
    {
        await candidateService.EnsureOwnedByAsync(candidateId, CurrentUserId, cancellationToken);

        if (photo.Length is <= 0 or > 2 * 1024 * 1024)
        {
            return BadRequest(new { message = "Photo must be between 1 byte and 2 MB." });
        }

        var extension = await DetectImageExtensionAsync(photo, cancellationToken);
        if (extension is null)
        {
            return BadRequest(new { message = "Only JPEG, PNG or WebP images are accepted." });
        }

        string? storedName = null;
        try
        {
            await using var stream = photo.OpenReadStream();
            storedName = await photoStore.SaveAsync(candidateId, stream, extension, cancellationToken);
            var previous = await candidateService.SetPhotoAsync(
                candidateId, CurrentUserId, storedName, CorrelationId, cancellationToken);
            if (previous is not null && !string.Equals(previous, storedName, StringComparison.Ordinal))
            {
                await photoStore.DeleteIfExistsAsync(previous, cancellationToken);
            }

            return Ok(new { message = "Candidate photo updated." });
        }
        catch
        {
            if (storedName is not null)
            {
                await photoStore.DeleteIfExistsAsync(storedName, cancellationToken);
            }

            throw;
        }
    }

    private async Task<Guid> GetStudentIdAsync(CancellationToken cancellationToken)
    {
        var student = await db.Students.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == CurrentUserId && s.IsActive, cancellationToken);
        return student?.Id ?? throw new ForbiddenOperationException("A student profile is required.");
    }
    private static async Task<string?> DetectImageExtensionAsync(
        IFormFile photo,
        CancellationToken cancellationToken)
    {
        await using var stream = photo.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header, cancellationToken);
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }

        if (read >= 12 &&
            header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }
}
