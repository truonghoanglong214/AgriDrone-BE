namespace AgriDrone.Api.Contracts.HarvestQualityGrades
{
    public sealed record VersionHarvestQualityGradeRequest(
        string Name,
        int DisplayOrder,
        long ExpectedVersion);
}
