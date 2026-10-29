using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Departments;

namespace SecureVoting.Api.Controllers;

[Route("api/departments")]
[Authorize]
public sealed class DepartmentsController(DepartmentService departments) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await departments.ListActiveForUserAsync(CurrentUserId, cancellationToken));
    }
}
