namespace StayBook.Helpers.Storage;

/// <summary>
/// Private storage for uploaded files, addressed by the server-generated stored name. Implementations must never
/// expose files through a public URL; downloads go through <c>FilesController</c> after authorization.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken);

    Task DeleteAsync(string storedName, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string storedName, CancellationToken cancellationToken);
}
