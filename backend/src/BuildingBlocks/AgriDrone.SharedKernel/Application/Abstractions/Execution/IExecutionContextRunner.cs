namespace AgriDrone.SharedKernel.Application.Abstractions.Execution;

public interface IExecutionContextRunner
{
    Task RunAsync<TService>(
        ExecutionContextSnapshot snapshot,
        Func<TService, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull;
}
