using AgriDrone.Modules.Surveys.Application.Authorization;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class SurveyAuthorizationPolicyTests
{
    [Fact]
    public void PublicActorCanOnlyViewCatalogueAndSubmitNewCustomerRequest()
    {
        var context = new SurveyAuthorizationContext(SurveyActorKind.Public);

        Assert.True(SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.ViewPublicCatalogue,
            context));
        Assert.True(SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.SubmitNewCustomerRequest,
            context));
        Assert.False(SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.ReviewRequest,
            context));
    }

    [Fact]
    public void SystemAdminOwnsReviewAndReconciliationButNotManagerActions()
    {
        var context = new SurveyAuthorizationContext(SurveyActorKind.SystemAdmin);

        Assert.True(SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.ReviewRequest,
            context));
        Assert.True(SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.ReconcilePayment,
            context));
        Assert.False(SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.ConfirmScope,
            context));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ManagerMutationRequiresActiveAssignmentAndQualification(
        bool assigned,
        bool qualified,
        bool expected)
    {
        var context = new SurveyAuthorizationContext(
            SurveyActorKind.SystemManager,
            HasActiveFarmAssignment: assigned,
            HasCurrentQualification: qualified);

        Assert.Equal(expected, SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.ReviewOrPublishResult,
            context));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void OwnerActionsRequireCorrectTenant(bool ownsTenant, bool expected)
    {
        var context = new SurveyAuthorizationContext(
            SurveyActorKind.TenantOwner,
            OwnsTenant: ownsTenant);

        Assert.Equal(expected, SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.InitiatePayment,
            context));
        Assert.False(SurveyAuthorizationPolicy.IsAllowed(
            SurveyAuthorizationAction.ViewPendingResult,
            context));
    }
}
