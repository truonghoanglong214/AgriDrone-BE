namespace AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;

internal sealed class FarmMembershipAssignmentConflictException(
    Exception innerException)
    : Exception(
        "A farm membership already exists for the selected user and farm.",
        innerException);
