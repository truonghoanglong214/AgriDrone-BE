namespace AgriDrone.SharedKernel.Application.Abstractions.Notifications;

public interface IEmailSender
{
    Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
