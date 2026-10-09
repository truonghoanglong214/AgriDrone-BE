namespace AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;

internal sealed class SurveyOrderMissionPlanningUnavailableException()
    : Exception(
        "The authoritative Survey Order planning query is unavailable.");
