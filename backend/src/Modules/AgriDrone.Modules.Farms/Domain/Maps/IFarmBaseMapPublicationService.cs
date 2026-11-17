namespace AgriDrone.Modules.Farms.Domain.Maps;

public interface IFarmBaseMapPublicationService
{
    FarmBaseMapVersion CreateFirstBaseline(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt);

    FarmBaseMapVersion CreateFollowUp(
        FarmBaseMapPublicationContext context,
        FarmBaseMapVersion predecessor,
        FarmBaseMapVersion baseline,
        DateTimeOffset createdAt);
}
