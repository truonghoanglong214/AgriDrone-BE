namespace AgriDrone.Modules.Farms.Domain.Maps;

public sealed class FarmBaseMapPublicationService : IFarmBaseMapPublicationService
{
    public FarmBaseMapVersion PrepareDraft(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt) =>
        FarmBaseMapVersion.PrepareDraft(context, createdAt);
}
