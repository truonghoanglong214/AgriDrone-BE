namespace AgriDrone.Modules.Farms.Domain.Maps.Staging;

public enum MappingCandidateReviewDecision
{
    Pending,
    Accept,
    Reject,
    Relocate,
    Replace,
    CreateNew = Accept,
    MatchExisting = Relocate
}
