using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.IntegrationContracts.Health;

public enum ComboAnalysisImportDisposition
{
    Completed,
    Retry,
    Rejected
}

public sealed record ComboAnalysisImportOutcome(
    ComboAnalysisImportDisposition Disposition,
    Guid? BatchId = null,
    bool Idempotent = false,
    int BlockingCount = 0,
    string? ErrorCode = null,
    string? Error = null);

public interface IComboAnalysisPendingImport
{
    Task<ComboAnalysisImportOutcome> ImportAsync(
        IntegrationEventEnvelope<ComboAnalysisReadyV1> envelope,
        CancellationToken cancellationToken);
}
