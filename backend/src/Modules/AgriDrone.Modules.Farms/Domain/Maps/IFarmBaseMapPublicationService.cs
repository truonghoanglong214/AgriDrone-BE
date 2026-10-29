namespace AgriDrone.Modules.Farms.Domain.Maps;

public interface IFarmBaseMapPublicationService
{
    FarmBaseMapVersion PrepareDraft(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt);
}
