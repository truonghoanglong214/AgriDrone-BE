namespace AgriDrone.IntegrationContracts.Media;

public sealed record MediaChecksumV1(
    string Algorithm,
    string Value);