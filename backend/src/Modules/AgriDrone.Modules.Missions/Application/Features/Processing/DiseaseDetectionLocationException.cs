namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class DiseaseDetectionLocationException(string imageName)
    : Exception($"GPS for detection image '{imageName}' is invalid or too far from the Farm to match poles. " +
        "Check the original image GPS and flight telemetry before retrying detection.");
