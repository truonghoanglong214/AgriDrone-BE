using AgriDrone.Modules.Identity.Application.Features.BootstrapSystemAdmin;
using AgriDrone.Modules.Identity.Application.Options;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity;

public static partial class IdentityInitializationExtensions
{
    public static async Task BootstrapSystemAdminAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<SystemAdminBootstrapOptions>>()
            .Value;

        if (!options.Enabled)
        {
            return;
        }

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(
            new BootstrapSystemAdminCommand(
                options.Email,
                options.FullName),
            cancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"System Admin bootstrap failed: {result.Error.Code} - " +
                result.Error.Description);
        }

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(IdentityInitializationExtensions));

        if (result.Value.Created)
        {
            LogSystemAdminCreated(
                logger,
                result.Value.UserId,
                result.Value.Email);
        }
        else
        {
            LogSystemAdminAlreadyConfigured(logger);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Created the initial System Admin account {UserId} for {Email}.")]
    private static partial void LogSystemAdminCreated(
        ILogger logger,
        Guid? userId,
        string email);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "System Admin bootstrap skipped because an active System Admin already exists.")]
    private static partial void LogSystemAdminAlreadyConfigured(ILogger logger);
}
