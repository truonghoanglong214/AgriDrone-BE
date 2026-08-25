namespace AgriDrone.Modules.Identity.Application.Abstractions;

internal interface ISystemAdminBootstrapLock
{
    Task AcquireAsync(CancellationToken cancellationToken = default);
}
