namespace AgriDrone.Api.Contracts.Farms
{
    public sealed record GetFarmByIdRequest(
        Guid FarmId
    );
}
