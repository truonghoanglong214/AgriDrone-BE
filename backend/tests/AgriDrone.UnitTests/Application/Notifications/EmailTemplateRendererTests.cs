using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Notifications.Application.EmailTemplates;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using Xunit;

namespace AgriDrone.UnitTests.Application.Notifications;

public sealed class EmailTemplateRendererTests
{
    [Fact]
    public void InvitationTemplateRendersAndHtmlEncodesVariables()
    {
        var renderer = new EmailTemplateRenderer(
            [new TenantInvitationEmailTemplate()]);

        var message = renderer.Render(
            EmailTemplateKeys.TenantInvitation,
            [new EmailRecipient("owner@example.com")],
            new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.TenantName] = "Farm <One>",
                [EmailTemplateVariableKeys.RoleName] = "Tenant Owner",
                [EmailTemplateVariableKeys.ActionUrl] =
                    "https://example.test/accept?token=a&next=b",
                [EmailTemplateVariableKeys.ExpiresAt] =
                    "2026-09-12T00:00:00.0000000+00:00"
            },
            "notification@example.test");

        Assert.Contains("Farm &lt;One&gt;", message.HtmlBody);
        Assert.Contains("token=a&amp;next=b", message.HtmlBody);
        Assert.Contains("Farm <One>", message.Subject);
        Assert.DoesNotContain("&lt;", message.Subject);
        Assert.Equal("notification@example.test", message.MessageId);
    }

    [Fact]
    public void WelcomeTemplateEncodesHtmlButKeepsSubjectAsPlainText()
    {
        var renderer = new EmailTemplateRenderer(
            [new TenantWelcomeEmailTemplate()]);

        var message = renderer.Render(
            EmailTemplateKeys.TenantWelcome,
            [new EmailRecipient("owner@example.com", "Owner")],
            new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.UserName] = "Owner <Admin>",
                [EmailTemplateVariableKeys.TenantName] = "Farm <One>",
                [EmailTemplateVariableKeys.RoleName] = "Tenant & Owner"
            },
            "welcome@example.test");

        Assert.Contains("Owner &lt;Admin&gt;", message.HtmlBody);
        Assert.Contains("Farm &lt;One&gt;", message.HtmlBody);
        Assert.Contains("Tenant &amp; Owner", message.HtmlBody);
        Assert.Equal("Welcome to Farm <One>", message.Subject);
        Assert.Contains("Farm <One>", message.TextBody);
        Assert.Equal("welcome@example.test", message.MessageId);
    }

    [Fact]
    public void UnknownTemplateIsRejected()
    {
        var renderer = new EmailTemplateRenderer(
            [new TenantWelcomeEmailTemplate()]);

        var exception = Assert.Throws<EmailTemplateException>(() =>
            renderer.Render(
                "unknown",
                [new EmailRecipient("owner@example.com")],
                new Dictionary<string, string>(),
                "notification@example.test"));

        Assert.Equal("EMAIL_TEMPLATE_NOT_FOUND", exception.Code);
    }

    [Fact]
    public void RegistrationTemplateRendersAllVariablesAndMessageId()
    {
        var renderer = new EmailTemplateRenderer(
            [new TenantRegistrationSuccessTemplate()]);

        var message = renderer.Render(
            EmailTemplateKeys.TenantRegistrationSuccess,
            [new EmailRecipient("owner@example.com", "Owner")],
            new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.UserName] = "Owner",
                [EmailTemplateVariableKeys.TenantName] = "Farm <One>",
                [EmailTemplateVariableKeys.TenantCode] = "FARM-01",
                [EmailTemplateVariableKeys.RoleName] = "Tenant Owner",
                [EmailTemplateVariableKeys.RegisteredAt] =
                    "2026-09-12T03:00:00.0000000+00:00",
                [EmailTemplateVariableKeys.LoginUrl] =
                    "https://app.example.test/login?from=email&ready=true"
            },
            "registration@example.test");

        Assert.Contains("Farm &lt;One&gt;", message.HtmlBody);
        Assert.Contains("from=email&amp;ready=true", message.HtmlBody);
        Assert.Contains("Farm <One>", message.Subject);
        Assert.DoesNotContain("&lt;", message.Subject);
        Assert.Contains("Farm <One>", message.TextBody);
        Assert.Contains("FARM-01", message.TextBody);
        Assert.Contains("https://app.example.test/login", message.TextBody);
        Assert.Equal("registration@example.test", message.MessageId);
    }

    [Fact]
    public void TemplateSubjectsRemoveLineBreaks()
    {
        var renderer = new EmailTemplateRenderer(
            [new TenantWelcomeEmailTemplate()]);

        var message = renderer.Render(
            EmailTemplateKeys.TenantWelcome,
            [new EmailRecipient("owner@example.com")],
            new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.UserName] = "Owner",
                [EmailTemplateVariableKeys.TenantName] = "Farm\r\nInjected",
                [EmailTemplateVariableKeys.RoleName] = "Tenant Owner"
            },
            "welcome@example.test");

        Assert.DoesNotContain('\r', message.Subject);
        Assert.DoesNotContain('\n', message.Subject);
        Assert.Equal("Welcome to Farm  Injected", message.Subject);
    }
}
