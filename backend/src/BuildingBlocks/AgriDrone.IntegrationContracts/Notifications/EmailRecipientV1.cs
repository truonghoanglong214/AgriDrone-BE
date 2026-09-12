namespace AgriDrone.IntegrationContracts.Notifications;

public sealed record EmailRecipientV1(
    string Address,
    string? DisplayName = null);
