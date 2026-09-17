using AgriDrone.Modules.Harvests.Application.Abstractions.Queries;
using AgriDrone.Modules.Harvests.Application.Features.GetActiveHarvestQualityGrades;
using Xunit;

namespace AgriDrone.UnitTests.Application.Harvests;

public sealed class GetActiveHarvestQualityGradesQueryHandlerTests
{
    [Fact]
    public async Task HandleReturnsCatalogItemsFromQueryService()
    {
        var expected = new HarvestQualityGradeCatalogResponse(
            Guid.NewGuid(),
            "PREMIUM",
            "Premium",
            DisplayOrder: 1,
            RevisionNumber: 2);
        var queries = new FakeHarvestQualityGradeQueries([expected]);
        var handler = new GetActiveHarvestQualityGradesQueryHandler(queries);

        var result = await handler.Handle(
            new GetActiveHarvestQualityGradesQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([expected], result.Value);
        Assert.Equal(1, queries.CallCount);
    }

    private sealed class FakeHarvestQualityGradeQueries(
        IReadOnlyList<HarvestQualityGradeCatalogResponse> response)
        : IHarvestQualityGradeQueries
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<HarvestQualityGradeCatalogResponse>> GetActiveAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(response);
        }
    }
}
