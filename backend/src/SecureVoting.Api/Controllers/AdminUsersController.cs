using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Departments;
using SecureVoting.Infrastructure.Identity;

namespace SecureVoting.Api.Controllers;

[Route("api/admin/users")]
[Authorize(Policy = "ManageUsers")]
public sealed class AdminUsersController(
    UserAdminService users,
    DepartmentService departments) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> List(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        return Ok(await users.ListAsync(search, cancellationToken));
    }

    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> Roles(
        Guid userId,
        AssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        await users.AssignRolesAsync(userId, request, CurrentUserId, CurrentActor, cancellationToken);
        return NoContent();
    }

    [HttpGet("{userId:guid}/departments")]
    public async Task<ActionResult<IReadOnlyList<DepartmentAssignmentDto>>> Departments(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await departments.GetAssignmentsAsync(userId, cancellationToken));
    }

    [HttpPut("{userId:guid}/departments")]
    public async Task<ActionResult<IReadOnlyList<DepartmentAssignmentDto>>> UpdateDepartments(
        Guid userId,
        UpdateDepartmentAssignmentsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await departments.UpdateAssignmentsAsync(
            userId, request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken));
    }

    [HttpPost("{userId:guid}/suspend")]
    public async Task<IActionResult> Suspend(
        Guid userId,
        SuspendUserRequest request,
        CancellationToken cancellationToken)
    {
        await users.SetActiveAsync(userId, false, request.Reason, CurrentUserId, CurrentActor, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid userId,
        SuspendUserRequest request,
        CancellationToken cancellationToken)
    {
        await users.SetActiveAsync(userId, true, request.Reason, CurrentUserId, CurrentActor, cancellationToken);
        return NoContent();
    }
}
