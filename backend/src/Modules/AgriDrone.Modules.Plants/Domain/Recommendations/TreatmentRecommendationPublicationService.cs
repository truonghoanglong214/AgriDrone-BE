namespace AgriDrone.Modules.Plants.Domain.Recommendations;

public static class TreatmentRecommendationPublicationService
{
    public static void Publish(
        TreatmentRecommendation draft,
        TreatmentRecommendation? currentPublished,
        Guid publishedBy,
        DateTimeOffset publishedAt)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (currentPublished is not null)
        {
            currentPublished.EnsureCanSupersede(draft, publishedAt);
        }

        draft.Publish(publishedBy, publishedAt);
        currentPublished?.Supersede(draft.Id, publishedAt);
    }
}
