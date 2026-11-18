using Microsoft.Extensions.Options;
using StayBook.Helpers.Options;

namespace StayBook.Helpers.Storage;

/// <summary>Stores uploads in <c>storage/uploads</c> (outside <c>wwwroot</c>).</summary>
public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string root = Path.GetFullPath(options.Value.UploadsPath);

    public async Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        await using var file = new FileStream(PathFor(storedName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken)
    {
        var path = PathFor(storedName);
        return Task.FromResult<Stream?>(File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null);
    }

    public Task DeleteAsync(string storedName, CancellationToken cancellationToken)
    {
        File.Delete(PathFor(storedName));
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storedName, CancellationToken cancellationToken) => Task.FromResult(File.Exists(PathFor(storedName)));

    /// <summary>Rejects names that could escape the uploads directory.</summary>
    private string PathFor(string storedName)
    {
        if (storedName.Length == 0 || storedName != Path.GetFileName(storedName) || storedName.StartsWith('.'))
        {
            throw new ArgumentException("Invalid stored file name.", nameof(storedName));
        }

        return Path.Combine(root, storedName);
    }
}
