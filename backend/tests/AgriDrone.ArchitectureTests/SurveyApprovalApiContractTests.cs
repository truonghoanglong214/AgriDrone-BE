using System.ComponentModel.DataAnnotations;
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
        Assert.Equal("survey-request-review.v3",
            SurveyRequestReviewChecklistDefinition.Version);
        Assert.Equal(
            [
                "contact-valid",
                "service-area-supported",
                "legal-flight-feasible"
            ],
            SurveyRequestReviewChecklistDefinition.Items.Select(
                item => item.Code));

        Assert.Equal(
            new HashSet<string>
            {
                "Version",
                "ContactValid",
                "IsServiceAreaSupported",
                "IsPreliminaryLegalAndFlightFeasible",
                "Notes"
            },
            typeof(SurveyRequestReviewChecklistRequest)
                .GetProperties()
                .Select(property => property.Name)
                .ToHashSet());
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
}
