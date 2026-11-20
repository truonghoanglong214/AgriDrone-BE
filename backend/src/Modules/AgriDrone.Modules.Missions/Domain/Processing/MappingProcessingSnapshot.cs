namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed record MappingProcessingSnapshot(
    int SourceVersion,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    string AlgorithmVersion,
    string InputFingerprint,
    string OutputFingerprint);
