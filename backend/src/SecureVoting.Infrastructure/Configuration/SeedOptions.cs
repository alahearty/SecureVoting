namespace SecureVoting.Infrastructure.Configuration;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";
    public bool Enabled { get; set; }
    public string DemoPassword { get; set; } = string.Empty;
}
