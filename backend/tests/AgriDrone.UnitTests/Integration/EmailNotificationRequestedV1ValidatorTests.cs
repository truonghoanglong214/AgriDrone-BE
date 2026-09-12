using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.IntegrationContracts.Notifications.Validation;
using Xunit;

namespace AgriDrone.UnitTests.Integration;

public sealed class EmailNotificationRequestedV1ValidatorTests
{
    [Fact]
    public void ValidPayloadHasNoErrors()
    {
        var payload = new EmailNotificationRequestedV1(
            Guid.NewGuid(),
            EmailTemplateKeys.TenantWelcome,
            [new EmailRecipientV1("owner@example.com", "Owner")],
            new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.UserName] = "Owner"
            });

        var errors = EmailNotificationRequestedV1Validator.Validate(payload);

        Assert.Empty(errors);
    }

    [Fact]
    public void InvalidAndDuplicatedRecipientsAreRejected()
    {
        var payload = new EmailNotificationRequestedV1(
            Guid.NewGuid(),
            EmailTemplateKeys.TenantWelcome,
            [
                new EmailRecipientV1("not-an-email"),
                new EmailRecipientV1("owner@example.com"),
                new EmailRecipientV1("OWNER@example.com")
            ],
            new Dictionary<string, string>());

        var errors = EmailNotificationRequestedV1Validator.Validate(payload);

        Assert.Contains(
            errors,
            error => error.Contains("invalid", StringComparison.Ordinal));
        Assert.Contains(
            errors,
            error => error.Contains("duplicated", StringComparison.Ordinal));
    }
}
