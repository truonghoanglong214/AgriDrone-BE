namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

public interface IChecksumCalculator
{
    Task<string> CalculateAsync(
        Stream content,
        string algorithm,
        CancellationToken cancellationToken = default);
}