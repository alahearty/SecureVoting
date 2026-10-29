namespace SecureVoting.Application.Contracts;

public sealed record MetaResponse(
    string ApplicationName,
    string Institution,
    bool DemoMode,
    DateTimeOffset ServerTimeUtc,
    string Version);
