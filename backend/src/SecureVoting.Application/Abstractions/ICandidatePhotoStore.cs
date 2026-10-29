namespace SecureVoting.Application.Abstractions;

public interface ICandidatePhotoStore
{
    Task<string> SaveAsync(Guid candidateId, Stream source, string extension, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType)?> OpenAsync(string fileName, CancellationToken cancellationToken = default);
    Task DeleteIfExistsAsync(string fileName, CancellationToken cancellationToken = default);
}
