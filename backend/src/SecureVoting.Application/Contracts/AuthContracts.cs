using System.ComponentModel.DataAnnotations;

namespace SecureVoting.Application.Contracts;

public sealed class LoginRequest
{
    [Required, StringLength(160)]
    public string Identifier { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

public sealed record CsrfTokenResponse(string Token);

public sealed record MeResponse(
    Guid UserId,
    string DisplayName,
    string? Email,
    string? MatricNumber,
    IReadOnlyList<string> Roles,
    bool HasStudentProfile,
    bool HasCandidateProfile,
    string? Department,
    bool IsDemo);

public sealed class ActivateAccountRequest
{
    [Required, StringLength(40)]
    public string MatricNumber { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Token { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    [Required, StringLength(160)]
    public string Identifier { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required, StringLength(160)]
    public string Identifier { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Token { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed record SimpleMessageResponse(string Message);
