namespace AgriDrone.Api.Contracts.HarvestQualityGrades
{
    public sealed record CreateHarvestQualityGradeRequest(
    string Code,
    string Name,
    int DisplayOrder);
}
