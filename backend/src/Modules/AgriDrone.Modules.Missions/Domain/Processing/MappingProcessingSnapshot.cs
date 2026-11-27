namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed record MappingProcessingSnapshot(
    int SourceVersion,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    string AlgorithmVersion,
    string InputFingerprint,
    string OutputFingerprint)
{
    public GridMeasurements? SubmittedGrid { get; init; }

    internal bool MatchesSubmission(MappingProcessingSnapshot submission)
    {
        if (SubmittedGrid is null)
        {
            return this == submission;
        }

        return (this with
        {
            GridBearingDeg = SubmittedGrid.BearingDeg,
            RowSpacingM = SubmittedGrid.RowSpacingM,
            PlantSpacingM = SubmittedGrid.PlantSpacingM,
            SubmittedGrid = null
        }) == submission;
    }

    public sealed record GridMeasurements(double BearingDeg, double RowSpacingM, double PlantSpacingM);
}
