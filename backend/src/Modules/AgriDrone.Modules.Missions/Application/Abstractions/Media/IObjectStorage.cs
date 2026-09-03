namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

public interface IObjectStorage
{
    Task<ObjectUploadSession> CreateUploadSessionAsync(
        ObjectUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<StoredObjectInfo?> GetInfoAsync(
        string storageUri,
        CancellationToken cancellationToken = default);

    Task ReadAsync(
        string storageUri,
        Func<Stream, CancellationToken, Task> reader,
        CancellationToken cancellationToken = default);

    Task<Uri> CreateDownloadUriAsync(
        string storageUri,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageUri,
        CancellationToken cancellationToken = default);
}