using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgriDrone.SharedInfrastructure.Execution;

internal sealed class ExecutionContextRunner(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ExecutionContextRunner> logger) : IExecutionContextRunner
{
    public async Task RunAsync<TService>(
        ExecutionContextSnapshot snapshot,
        Func<TService, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(operation);

        await using var serviceScope = serviceScopeFactory.CreateAsyncScope();
        var serviceProvider = serviceScope.ServiceProvider;
        var initializer = serviceProvider
            .GetRequiredService<IExecutionContextInitializer>();

        using var contextLease = initializer.Begin(snapshot);
        using var loggingScope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["ExecutionSource"] = snapshot.Source.ToString(),
                ["TenantId"] = snapshot.TenantId,
                ["ActorId"] = snapshot.ActorId,
                ["CorrelationId"] = snapshot.CorrelationId,
                ["MessageId"] = snapshot.MessageId
            });

        var service = serviceProvider.GetRequiredService<TService>();
        await operation(service, cancellationToken);
    }
}
