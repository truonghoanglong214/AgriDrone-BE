namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

public interface IMultipartObjectStorage
{
    Task<string> InitiateAsync(string storageUri, string mimeType,
        CancellationToken cancellationToken = default);

    Task<Uri> CreatePartUploadUriAsync(string storageUri, string uploadId,
        int partNumber, TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MultipartUploadedPart>> ListPartsAsync(
        string storageUri, string uploadId,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(string storageUri, string uploadId,
        IReadOnlyList<MultipartUploadedPart> parts,
        CancellationToken cancellationToken = default);

    Task AbortAsync(string storageUri, string uploadId,
        CancellationToken cancellationToken = default);
}
