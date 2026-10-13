using AgriDrone.Modules.Plants.Infrastructure.Initialization;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Plants;

public static class PlantsInitializationExtensions
{
    public static async Task ValidateCoreMasterDataAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var validator = scope.ServiceProvider
            .GetRequiredService<HealthLevelSeedValidator>();
        var result = await validator.ValidateAsync(cancellationToken);

        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
    }
}
