namespace AgriDrone.Modules.Farms.Domain.Maps.Staging;

public enum MappingCandidateReviewDecision
{
    Pending = 0,
    MatchExisting = 1,
    Relocate = 2,
    CreateNew = 3,
    Replace = 4,
    Reject = 5
}
