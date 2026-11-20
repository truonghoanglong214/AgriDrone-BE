namespace AgriDrone.SharedKernel.Application.Abstractions;

public interface IRefundEvidenceStorage
{
    Task<string> UploadAsync(Guid tenantId, Guid caseId, Stream content, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid tenantId, Guid caseId, string reference, CancellationToken cancellationToken);
    Task<Uri> GetDownloadUriAsync(string reference, CancellationToken cancellationToken);
}
