namespace AgriDrone.Modules.Farms.Application.Services;

public sealed record MappingBatchValidationIssue(
    Guid? CandidateId,
    string Code,
    string Message,
    string Severity);
