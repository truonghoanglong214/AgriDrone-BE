namespace AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;

public sealed record SurveyOrderMissionPlanningContext(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    SurveyServiceType SelectedService,
    bool RequiresBaselineMapping,
    Guid? CurrentBaseMapVersionId,
    IReadOnlyCollection<Guid> ScopeZoneIds,
    DateTimeOffset AppointmentStartAt,
    DateTimeOffset AppointmentEndAt,
    bool IsReadyForOperations,
    string? ReadinessFailureCode,
    bool IsEligibleForPlanning = true,
    bool IsReadyToSchedule = true,
    Guid? FarmBoundaryVersionId = null,
    Guid? PrimarySystemManagerId = null);
