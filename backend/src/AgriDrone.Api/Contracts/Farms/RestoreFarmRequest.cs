namespace AgriDrone.Api.Contracts.Farms
{
    public sealed record RestoreFarmRequest(
        long ExpectedVersion
    );
}
