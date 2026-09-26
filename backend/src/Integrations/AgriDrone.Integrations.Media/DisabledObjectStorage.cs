using AgriDrone.Modules.Missions.Application.Abstractions.Media;

namespace AgriDrone.Integrations.Media;

internal sealed class DisabledObjectStorage : IObjectStorage, IObjectStorageWriter
{
    public Task<ObjectUploadSession> CreateUploadSessionAsync(
        ObjectUploadRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<ObjectUploadSession>(CreateDisabledException());

    public Task<StoredObjectInfo?> GetInfoAsync(
        string storageUri,
        CancellationToken cancellationToken = default) =>
        Task.FromException<StoredObjectInfo?>(CreateDisabledException());

    public Task ReadAsync(
        string storageUri,
        Func<Stream, CancellationToken, Task> reader,
        CancellationToken cancellationToken = default) =>
        Task.FromException(CreateDisabledException());

    public Task<Uri> CreateDownloadUriAsync(
        string storageUri,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default) =>
        Task.FromException<Uri>(CreateDisabledException());

    public Task DeleteAsync(
        string storageUri,
        CancellationToken cancellationToken = default) =>
        Task.FromException(CreateDisabledException());

    public Task UploadAsync(
        string storageUri,
        Stream content,
        long length,
        string mimeType,
        CancellationToken cancellationToken = default) =>
        Task.FromException(CreateDisabledException());

    private static InvalidOperationException CreateDisabledException() =>
        new(
            "Object storage is disabled. Configure ObjectStorage and set " +
            "ObjectStorage:Enabled to true before using media operations.");
}
