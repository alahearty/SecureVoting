using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Dashboard;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Api.Controllers;

[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(
    DashboardService dashboard,
    AppDbContext db) : ApiControllerBase
{
    [HttpGet("admin")]
    [Authorize(Policy = "AdminDashboard")]
    public async Task<ActionResult<AdminDashboardDto>> Admin(CancellationToken cancellationToken)
    {
        return Ok(await dashboard.GetAdminAsync(CurrentUserId, cancellationToken));
    }

    [HttpGet("student")]
    [Authorize(Policy = "StudentPortal")]
    public async Task<ActionResult<StudentDashboardDto>> Student(CancellationToken cancellationToken)
    {
        var student = await db.Students.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == CurrentUserId && s.IsActive, cancellationToken)
            ?? throw new ForbiddenOperationException("An active student profile is required.");
        return Ok(await dashboard.GetStudentAsync(student.Id, CurrentUserId, cancellationToken));
    }

    [HttpGet("candidate")]
    [Authorize(Policy = "CandidatePortal")]
    public async Task<ActionResult<CandidateDashboardDto>> Candidate(CancellationToken cancellationToken)
    {
        return Ok(await dashboard.GetCandidateAsync(CurrentUserId, cancellationToken));
    }

    [HttpGet("observer")]
    [Authorize(Policy = "ObserverPortal")]
    public async Task<ActionResult<ObserverDashboardDto>> Observer(CancellationToken cancellationToken)
    {
        return Ok(await dashboard.GetObserverAsync(CurrentUserId, cancellationToken));
    }
}
