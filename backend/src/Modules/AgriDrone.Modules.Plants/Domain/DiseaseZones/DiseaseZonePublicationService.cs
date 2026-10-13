namespace AgriDrone.Modules.Plants.Domain.DiseaseZones;

public static class DiseaseZonePublicationService
{
    public static void Publish(
        DiseaseZone reviewedZone,
        DiseaseZone? currentPublished,
        Guid publishedBy,
        DateTimeOffset publishedAt)
    {
        ArgumentNullException.ThrowIfNull(reviewedZone);

        currentPublished?.EnsureCanSupersede(reviewedZone, publishedAt);
        reviewedZone.Publish(publishedBy, publishedAt);
        currentPublished?.Supersede(reviewedZone.Id, publishedAt);
    }
}
