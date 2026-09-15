namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

public interface IObjectStorageWriter
{
    Task UploadAsync(string storageUri, Stream content, long length,
        string mimeType, CancellationToken cancellationToken = default);
}
