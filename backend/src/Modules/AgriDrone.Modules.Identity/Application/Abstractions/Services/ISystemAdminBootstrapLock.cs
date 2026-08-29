namespace AgriDrone.Modules.Identity.Application.Abstractions.Services;

internal interface ISystemAdminBootstrapLock
{
    Task AcquireAsync(CancellationToken cancellationToken = default);
}
