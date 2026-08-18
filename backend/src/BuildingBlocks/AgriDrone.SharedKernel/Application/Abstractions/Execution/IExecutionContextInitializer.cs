namespace AgriDrone.SharedKernel.Application.Abstractions.Execution;

public interface IExecutionContextInitializer
{
    IDisposable Begin(ExecutionContextSnapshot snapshot);
}
