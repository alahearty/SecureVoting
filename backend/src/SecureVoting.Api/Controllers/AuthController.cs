using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Auditing;
using SecureVoting.Infrastructure.Identity;
using SecureVoting.Infrastructure.Persistence;
using SecureVoting.Infrastructure.Students;

namespace SecureVoting.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    AuditWriter auditWriter,
    ActivationService activationService,
    PasswordResetService passwordResetService) : ApiControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken ?? string.Empty));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<MeResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(request.Identifier);
        if (user is null || !user.IsActive)
        {
            await RecordAuthFailureAsync("Invalid credentials or account unavailable.", cancellationToken);
            return Unauthorized(new { message = "Invalid credentials or account unavailable." });
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await RecordAuthFailureAsync("Invalid credentials, unconfirmed account, or lockout.", cancellationToken);
            return Unauthorized(new { message = "Invalid credentials or account unavailable." });
        }

        await RecordAuthSuccessAsync(user, "Password sign-in", cancellationToken);
        return Ok(await BuildMeAsync(user, cancellationToken));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        await signInManager.SignOutAsync();
        if (user is not null)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await auditWriter.AppendAsync(
                db,
                "SignedOut",
                "User",
                user.Id.ToString(),
                AuditOutcome.Success,
                user.Id,
                user.DisplayName,
                CorrelationId,
                null,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User)
            ?? throw new UnauthorizedAccessException("The authenticated user was not found.");
        return Ok(await BuildMeAsync(user, cancellationToken));
    }

    [HttpPost("activate")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<MeResponse>> Activate(
        ActivateAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Password confirmation does not match." });
        }

        var user = await activationService.ActivateAsync(
            request.MatricNumber,
            request.Token,
            request.Password,
            CorrelationId,
            cancellationToken);
        var signIn = await signInManager.PasswordSignInAsync(user, request.Password, false, false);
        if (!signIn.Succeeded)
        {
            return Ok(new { message = "Account activated. Sign in with your new password." });
        }

        return Ok(await BuildMeAsync(user, cancellationToken));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await passwordResetService.RequestAsync(
            request.Identifier,
            db,
            CorrelationId,
            cancellationToken);
        return Accepted(new
        {
            message = "If the account exists and email delivery is configured, a reset link will be sent."
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Password confirmation does not match." });
        }

        await passwordResetService.ResetAsync(
            request.Identifier,
            request.Token,
            request.Password,
            db,
            CorrelationId,
            cancellationToken);
        return Ok(new { message = "Password reset completed. Sign in with the new password." });
    }

    private async Task<ApplicationUser?> FindUserAsync(string identifier)
    {
        var value = identifier.Trim();
        if (value.Contains('@', StringComparison.Ordinal))
        {
            return await userManager.FindByEmailAsync(value);
        }

        if (MatriculationNumber.TryNormalize(value, out var normalized))
        {
            return await userManager.FindByNameAsync(normalized);
        }

        return null;
    }

    private async Task<MeResponse> BuildMeAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var student = await db.Students.AsNoTracking()
            .Include(s => s.DepartmentRef)
            .SingleOrDefaultAsync(s => s.UserId == user.Id, cancellationToken);
        var hasCandidate = student is not null &&
                           await db.Candidates.AnyAsync(c => c.StudentId == student.Id, cancellationToken);
        var demoMode = await db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == "DemoMode")
            .Select(s => s.Value)
            .SingleOrDefaultAsync(cancellationToken);
        return new MeResponse(
            user.Id,
            user.DisplayName,
            user.Email,
            student?.MatricNumber,
            roles.ToArray(),
            student is not null,
            hasCandidate,
            student?.DepartmentRef?.Name ?? student?.Department,
            string.Equals(demoMode, "true", StringComparison.OrdinalIgnoreCase));
    }

    private async Task RecordAuthSuccessAsync(
        ApplicationUser user,
        string method,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "AuthenticationSucceeded",
            "User",
            user.Id.ToString(),
            AuditOutcome.Success,
            user.Id,
            user.DisplayName,
            CorrelationId,
            new { method },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RecordAuthFailureAsync(string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await auditWriter.AppendAsync(
            db,
            "AuthenticationFailed",
            "User",
            null,
            AuditOutcome.Failure,
            null,
            "Anonymous",
            CorrelationId,
            new { reason },
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
