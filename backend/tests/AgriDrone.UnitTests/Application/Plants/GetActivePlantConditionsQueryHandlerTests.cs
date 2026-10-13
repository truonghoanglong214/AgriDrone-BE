using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;
using AgriDrone.Modules.Plants.Domain.Conditions;
using Xunit;

namespace AgriDrone.UnitTests.Application.Plants;

public sealed class GetActivePlantConditionsQueryHandlerTests
{
    [Fact]
    public async Task HandleReturnsCatalogItemsFromQueryService()
    {
        var expected = new PlantConditionCatalogResponse(
            Guid.NewGuid(),
            "SUNBURN",
            "Sunburn",
            ScientificName: null,
            ConditionType.AbioticDamage,
            "Sun damage",
            RevisionNumber: 2);
        var queries = new FakePlantConditionQueries([expected]);
        var handler = new GetActivePlantConditionsQueryHandler(queries);

        var result = await handler.Handle(
            new GetActivePlantConditionsQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([expected], result.Value);
        Assert.Equal(1, queries.CallCount);
    }

    private sealed class FakePlantConditionQueries(
        IReadOnlyList<PlantConditionCatalogResponse> response)
        : IPlantConditionQueries
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<PlantConditionCatalogResponse>> GetActiveAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(response);
        }
    }
}
