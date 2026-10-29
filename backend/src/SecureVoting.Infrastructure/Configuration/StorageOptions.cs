namespace SecureVoting.Infrastructure.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string CandidatePhotosPath { get; set; } = string.Empty;
    public long MaximumPhotoBytes { get; set; } = 2 * 1024 * 1024;
}
