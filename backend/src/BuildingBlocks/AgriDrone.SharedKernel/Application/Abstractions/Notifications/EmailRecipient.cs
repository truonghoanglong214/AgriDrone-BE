namespace AgriDrone.SharedKernel.Application.Abstractions.Notifications;

public sealed record EmailRecipient(
    string Address,
    string? DisplayName = null);
