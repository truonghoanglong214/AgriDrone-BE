namespace AgriDrone.Modules.Plants.Domain.Changes;

public enum PlantInventoryChangeStatus
{
    Submitted,
    UnderReview,
    AwaitingSurveyEvidence,
    Verified,
    Applied,
    Rejected,
    Withdrawn
}
