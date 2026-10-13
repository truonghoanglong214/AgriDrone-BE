namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class ActiveFarmManagerAssignmentConflictException(
    Exception innerException)
    : Exception(
        "The Farm already has an active primary SystemManager assignment.",
        innerException);
