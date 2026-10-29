using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace SecureVoting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var identifier)
                ? identifier
                : throw new UnauthorizedAccessException("The authenticated user identifier is missing.");
        }
    }

    protected string CurrentActor => User.Identity?.Name ?? "Authenticated user";
    protected string CorrelationId => HttpContext.TraceIdentifier;
}
