using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Departments;

namespace SecureVoting.Api.Controllers;

[Route("api/admin/departments")]
[Authorize(Policy = "ManageSettings")]
public sealed class AdminDepartmentsController(DepartmentService departments) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await departments.ListAllAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(
        CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await departments.CreateAsync(
            request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return Created($"/api/admin/departments/{created.Id}", created);
    }

    [HttpPut("{departmentId:guid}")]
    public async Task<ActionResult<DepartmentDto>> Update(
        Guid departmentId,
        UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await departments.UpdateAsync(
            departmentId, request, CurrentUserId, CurrentActor, CorrelationId, cancellationToken));
    }

    [HttpPost("{departmentId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid departmentId, CancellationToken cancellationToken)
    {
        await departments.SetActiveAsync(
            departmentId, true, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{departmentId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid departmentId, CancellationToken cancellationToken)
    {
        await departments.SetActiveAsync(
            departmentId, false, CurrentUserId, CurrentActor, CorrelationId, cancellationToken);
        return NoContent();
    }
}
