using AgriDrone.SharedInfrastructure.Execution;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure;

public sealed class ExecutionContextFoundationTests
{
    [Fact]
    public async Task RabbitMqOperationRestoresEnvelopeContext()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExecutionContext();
        services.AddScoped<ContextCaptureService>();
        await using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<IExecutionContextRunner>();
        ExecutionContextSnapshot? actual = null;

        await runner.RunAsync<ContextCaptureService>(
            ExecutionContextSnapshot.ForRabbitMq(
                tenantId,
                actorId,
                correlationId,
                messageId),
            (service, _) =>
            {
                actual = service.Capture();
                return Task.CompletedTask;
            });

        Assert.NotNull(actual);
        Assert.Equal(tenantId, actual.TenantId);
        Assert.Equal(actorId, actual.ActorId);
        Assert.Equal(correlationId, actual.CorrelationId);
        Assert.Equal(messageId, actual.MessageId);
        Assert.Equal(ExecutionContextSource.RabbitMq, actual.Source);
    }

    private sealed class ContextCaptureService(IExecutionContext context)
    {
        public ExecutionContextSnapshot Capture() =>
            new(
                context.TenantId,
                context.ActorId,
                context.CorrelationId,
                context.MessageId,
                context.Source);
    }
}
