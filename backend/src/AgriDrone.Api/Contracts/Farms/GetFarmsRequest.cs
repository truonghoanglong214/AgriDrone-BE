namespace AgriDrone.Api.Contracts.Farms
{
    public sealed record GetFarmsRequest
    {
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }
}
