namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record ManagerMobileWorkItemV1(
    Guid WorkItemId,
    Guid SurveyOrderId,
    Guid FarmId,
    Guid AssignedManagerId,
    string WorkType,
    string ChecklistDefinitionVersion,
    uint ServerVersion,
    DateTimeOffset AssignmentExpiresAt,
    DateTimeOffset AuthorizationExpiresAt,
    IReadOnlyList<ManagerMobileChecklistItemV1> Checklist,
    IReadOnlyList<ManagerMobileNoteV1> Notes);

public sealed record ManagerMobileChecklistItemV1(
    string Code,
    string Label,
    bool IsRequired,
    bool IsCompleted);

public sealed record ManagerMobileNoteV1(
    Guid NoteId,
    string Text,
    DateTimeOffset CapturedAt,
    bool IsCommitted);

public sealed record ManagerMobileSyncCommandV1(
    Guid ClientOperationId,
    Guid WorkItemId,
    uint BaseVersion,
    DateTimeOffset CapturedAt,
    string PayloadHash,
    IReadOnlyList<ManagerMobileSyncMutationV1> Mutations);

public sealed record ManagerMobileSyncMutationV1(
    string MutationType,
    string TargetKey,
    string Payload);

public sealed record ManagerMobileSyncResultV1(
    Guid ClientOperationId,
    uint ServerVersion,
    IReadOnlyList<ManagerMobileSyncItemOutcomeV1> Outcomes);

public sealed record ManagerMobileSyncItemOutcomeV1(
    string TargetKey,
    string Outcome,
    string? ConflictCode);
