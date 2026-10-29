using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Api.Controllers;

[Route("api/meta")]
public sealed class MetaController(AppDbContext db, TimeProvider timeProvider) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<MetaResponse>> Get(CancellationToken cancellationToken)
    {
        var settings = await db.SystemSettings.AsNoTracking()
            .Where(s => new[] { "ApplicationName", "InstitutionName", "DemoMode" }.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        return Ok(new MetaResponse(
            settings.GetValueOrDefault("ApplicationName", "IAUE SecureVote"),
            settings.GetValueOrDefault("InstitutionName", "Ignatius Ajuru University of Education"),
            string.Equals(settings.GetValueOrDefault("DemoMode"), "true", StringComparison.OrdinalIgnoreCase),
            timeProvider.GetUtcNow(),
            version));
    }
}
