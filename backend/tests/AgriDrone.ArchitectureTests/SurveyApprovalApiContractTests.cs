using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AgriDrone.Api.Contracts.Surveys.Approvals;
using AgriDrone.Api.Contracts.Surveys.Requests;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class SurveyApprovalApiContractTests
{
    [Fact]
    public void ReviewChecklistContainsOnlyManualApprovalChecks()
    {
        Assert.Equal("survey-request-review.v6",
            SurveyRequestReviewChecklistDefinition.Version);
        Assert.Equal(
            [
                "candidate-location-sufficient",
                "consent-feasibility",
                "contact-valid",
                "dragon-fruit-farm-confirmed",
                "flight-permission-feasibility",
                "service-area-supported"
            ],
            SurveyRequestReviewChecklistDefinition.Items.Select(
                item => item.Code).OrderBy(code => code, StringComparer.Ordinal));
        Assert.DoesNotContain(
            SurveyRequestReviewChecklistDefinition.Items,
            item => item.Code.Contains(
                "verified",
                StringComparison.OrdinalIgnoreCase));
        Assert.All(
            SurveyRequestReviewChecklistDefinition.Items.Where(item =>
                item.Code is "consent-feasibility" or
                    "flight-permission-feasibility"),
            item => Assert.Contains(
                "does not verify",
                item.Description,
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            new HashSet<string>
            {
                "ContactValid",
                "DragonFruitFarmConfirmed",
                "ServiceAreaSupported",
                "CandidateLocationSufficient",
                "ConsentFeasibility",
                "FlightPermissionFeasibility",
                "Notes"
            },
            typeof(SurveyRequestReviewChecklistRequest)
                .GetProperties()
                .Select(property => property.Name)
                .ToHashSet());
    }

    [Fact]
    public void ReviewChecklistRequiresExactDynamicAnswers()
    {
        var completeAnswers = new Dictionary<string, bool>
        {
            ["contact-valid"] = true,
            ["dragon-fruit-farm-confirmed"] = true,
            ["service-area-supported"] = true,
            ["candidate-location-sufficient"] = true,
            ["consent-feasibility"] = true,
            ["flight-permission-feasibility"] = true
        };

        Assert.True(
            SurveyRequestReviewChecklistDefinition.HasExactAnswers(
                completeAnswers));
        Assert.True(
            SurveyRequestReviewChecklistDefinition.AreAllAnswersConfirmed(
                completeAnswers));

        completeAnswers["contact-valid"] = false;
        Assert.False(
            SurveyRequestReviewChecklistDefinition.AreAllAnswersConfirmed(
                completeAnswers));

        completeAnswers["unknown-check"] = true;
        Assert.False(
            SurveyRequestReviewChecklistDefinition.HasExactAnswers(
                completeAnswers));
    }

    [Fact]
    public void ReviewChecklistSerializesStableRequestFields()
    {
        var payload = new SurveyRequestReviewChecklistRequest(
            ContactValid: true,
            DragonFruitFarmConfirmed: true,
            ServiceAreaSupported: true,
            CandidateLocationSufficient: true,
            ConsentFeasibility: true,
            FlightPermissionFeasibility: true,
            Notes: null);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));

        Assert.True(json.RootElement.GetProperty("contact-valid").GetBoolean());
        Assert.True(json.RootElement
            .GetProperty("service-area-supported")
            .GetBoolean());
        Assert.True(json.RootElement
            .GetProperty("candidate-location-sufficient")
            .GetBoolean());
        Assert.True(json.RootElement
            .GetProperty("consent-feasibility")
            .GetBoolean());
        Assert.True(json.RootElement
            .GetProperty("flight-permission-feasibility")
            .GetBoolean());
        Assert.False(json.RootElement.TryGetProperty("version", out _));
        Assert.False(json.RootElement.TryGetProperty("answers", out _));
    }

    [Fact]
    public void InboxAndAcknowledgementDoNotExposeContactPii()
    {
        var prohibitedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "ApplicantName",
            "ApplicantEmail",
            "ApplicantPhone",
            "FarmAddress",
            "Notes"
        };

        Assert.DoesNotContain(
            typeof(SurveyRequestInboxItemApiResponse)
                .GetProperties()
                .Select(property => property.Name),
            prohibitedProperties.Contains);
        Assert.DoesNotContain(
            typeof(SurveyRequestAcknowledgementApiResponse)
                .GetProperties()
                .Select(property => property.Name),
            prohibitedProperties.Contains);
    }

    [Theory]
    [InlineData("ContactValid")]
    [InlineData("DragonFruitFarmConfirmed")]
    [InlineData("ServiceAreaSupported")]
    [InlineData("CandidateLocationSufficient")]
    [InlineData("ConsentFeasibility")]
    [InlineData("FlightPermissionFeasibility")]
    public void ChecklistRequiredMetadataIsOnConstructorParameters(
        string parameterName)
    {
        var constructor = Assert.Single(
            typeof(SurveyRequestReviewChecklistRequest).GetConstructors());
        var parameter = Assert.Single(
            constructor.GetParameters(),
            candidate => candidate.Name == parameterName);

        Assert.Contains(
            parameter.GetCustomAttributes(inherit: true),
            attribute => attribute.GetType() == typeof(RequiredAttribute));
    }

    [Theory]
    [InlineData("Reason", typeof(RequiredAttribute))]
    [InlineData("Reason", typeof(StringLengthAttribute))]
    [InlineData("Checklist", typeof(RequiredAttribute))]
    [InlineData("ExpectedVersion", typeof(RequiredAttribute))]
    [InlineData("ExpectedVersion", typeof(RangeAttribute))]
    public void ApprovalValidationMetadataIsOnConstructorParameters(
        string parameterName,
        Type attributeType)
    {
        var constructor = Assert.Single(
            typeof(ApproveSurveyRequest).GetConstructors());
        var parameter = Assert.Single(
            constructor.GetParameters(),
            candidate => candidate.Name == parameterName);

        Assert.Contains(
            parameter.GetCustomAttributes(inherit: true),
            attribute => attribute.GetType() == attributeType);
    }

    [Fact]
    public void ApprovalResponseExposesTheVersionedCompatibilityDecision()
    {
        var properties = typeof(ApproveSurveyRequestApiResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("PreviousCompatibleOrderId", properties);
        Assert.Contains(
            "PreviousOrderCompatibilityPolicyVersion",
            properties);
        Assert.Contains("PreviousOrderCompatibilityReason", properties);
    }
}
