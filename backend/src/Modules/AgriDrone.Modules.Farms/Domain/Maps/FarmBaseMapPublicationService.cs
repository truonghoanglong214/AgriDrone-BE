namespace AgriDrone.Modules.Farms.Domain.Maps;

public sealed class FarmBaseMapPublicationService : IFarmBaseMapPublicationService
{
    public FarmBaseMapVersion CreateFirstBaseline(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt) =>
        FarmBaseMapVersion.CreateFirstBaseline(context, createdAt);

    public FarmBaseMapVersion CreateFollowUp(
        FarmBaseMapPublicationContext context,
        FarmBaseMapVersion predecessor,
        FarmBaseMapVersion baseline,
        DateTimeOffset createdAt) =>
        FarmBaseMapVersion.CreateFollowUp(context, predecessor, baseline, createdAt);
}
