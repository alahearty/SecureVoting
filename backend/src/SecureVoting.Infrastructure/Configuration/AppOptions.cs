namespace SecureVoting.Infrastructure.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
    public int PasswordResetMinutes { get; set; } = 30;
}
