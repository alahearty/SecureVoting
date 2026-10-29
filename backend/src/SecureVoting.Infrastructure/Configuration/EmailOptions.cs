namespace SecureVoting.Infrastructure.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "IAUE SecureVote";
    public bool UseStartTls { get; set; } = true;
}
