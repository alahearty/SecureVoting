using Microsoft.Extensions.Options;
using SecureVoting.Application.Abstractions;
using SecureVoting.Infrastructure.Configuration;

namespace SecureVoting.Infrastructure.Storage;

public sealed class CandidatePhotoStore : ICandidatePhotoStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly string _root;
    private readonly long _maximumBytes;

    public CandidatePhotoStore(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(options.Value.CandidatePhotosPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "CandidatePhotos")
            : options.Value.CandidatePhotosPath);
        _maximumBytes = options.Value.MaximumPhotoBytes;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        Guid candidateId,
        Stream source,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported photo type.");
        }

        var safeName = $"{candidateId:N}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var finalPath = Path.Combine(_root, safeName);
        var temporaryPath = finalPath + ".tmp";
        try
        {
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.WriteThrough))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > _maximumBytes)
                    {
                        throw new InvalidOperationException("Photo exceeds the permitted size.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, finalPath, true);
            return safeName;
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }

            throw;
        }
    }

    public Task<(Stream Stream, string ContentType)?> OpenAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafeFileName(fileName))
        {
            return Task.FromResult<(Stream, string)?>(null);
        }

        var path = Path.Combine(_root, fileName);
        if (!File.Exists(path))
        {
            return Task.FromResult<(Stream, string)?>(null);
        }

        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<(Stream, string)?>((stream, contentType));
    }

    public Task DeleteIfExistsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (IsSafeFileName(fileName))
        {
            var path = Path.Combine(_root, fileName);
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsSafeFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) &&
        AllowedExtensions.Contains(Path.GetExtension(fileName));
}
