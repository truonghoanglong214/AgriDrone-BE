using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Orders;

public sealed record SurveyOrderBoundaryVerificationState(
    SurveyOrder Order,
    Guid SystemManagerProfileId,
    Guid? CurrentPublishedBaseMapVersionId,
    int? CurrentConfirmedPoleCount,
    IReadOnlyList<SurveyZoneSelectionSnapshot> ActiveZones);
