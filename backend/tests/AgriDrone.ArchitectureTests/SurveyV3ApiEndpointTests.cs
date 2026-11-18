using AgriDrone.Api.Contracts.Surveys.Approvals;
using AgriDrone.Api.Contracts.Surveys.Requests;
using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Api.Controllers.V3;
using AgriDrone.Api.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServiceV3;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicCatalogueV3;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServicePriceV3;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServiceRevision;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishBusinessPolicyVersion;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishCancellationPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishClaimPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.PublishSurveyDepositPolicy;
using AgriDrone.Modules.Surveys.Application.Features.Policies.TransitionBusinessPolicyActivation;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequestV3;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitTenantOwnerSurveyRequestV3;
using AgriDrone.Modules.Surveys.Application.Features.Requests.TrackSurveyRequestV3;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class SurveyV3ApiEndpointTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task V3CatalogControllerReturns200WithCatalogueWhenConfigured()
    {
        var sender = new FakeSender();
        var expectedCatalogue = new PublicCatalogueV3Response(
            EvaluatedAt: T0,
            IsBookable: false,
            PolicySummary: new PublicPolicySummaryResponse(
                BundleVersionId: Guid.NewGuid(),
                BundleVersionNumber: 1,
                PolicyFingerprint: "fp123",
                PricingCurrency: "VND",
                ClaimWindowDays: 7,
                ClaimTimeZoneId: "Asia/Ho_Chi_Minh",
                ExcludeNoticeDate: true,
                RefundableBeforeCapture: true,
                RetainDepositAfterCapture: true,
                AllowedFreeNoShowReschedules: 1),
            SharedDeposit: new PublicSharedDepositResponse(
                DepositPolicyId: Guid.NewGuid(),
                VersionNumber: 1,
                Amount: 100000m,
                Currency: "VND"),
            Services: []);

        sender.RegisterHandler<GetPublicCatalogueV3Query, Result<PublicCatalogueV3Response?>>(
            _ => Task.FromResult(Result.Success<PublicCatalogueV3Response?>(expectedCatalogue)));

        var controller = new V3CatalogController(sender)
        {
            ControllerContext = CreateControllerContext()
        };

        var httpResult = await controller.GetCatalogue(CancellationToken.None);
        var okResult = Assert.IsType<Ok<PublicCatalogueV3Response>>(httpResult);
        Assert.Equal(expectedCatalogue, okResult.Value);
    }

    [Fact]
    public async Task V3CatalogControllerReturns404WhenCatalogueNull()
    {
        var sender = new FakeSender();
        sender.RegisterHandler<GetPublicCatalogueV3Query, Result<PublicCatalogueV3Response?>>(
            _ => Task.FromResult(Result.Success<PublicCatalogueV3Response?>(null)));

        var controller = new V3CatalogController(sender)
        {
            ControllerContext = CreateControllerContext()
        };

        var httpResult = await controller.GetCatalogue(CancellationToken.None);
        Assert.IsType<NotFound>(httpResult);
    }

    [Fact]
    public async Task V3PublicSurveyRequestsControllerReturnsValidationProblemWhenMissingIdempotency()
    {
        var sender = new FakeSender();
        var callerScope = new FakeCallerScope();
        var controller = new V3PublicSurveyRequestsController(sender, callerScope)
        {
            ControllerContext = CreateControllerContext()
        };

        var request = new SubmitNewCustomerSurveyRequestV3ApiRequest(
            SurveyServiceId: Guid.NewGuid(),
            TenantCode: "AGRI",
            TenantName: "Agri Farm",
            ApplicantName: "John Doe",
            ApplicantEmail: "john@example.com",
            ApplicantPhone: "0901234567",
            FarmName: "Green Farm",
            FarmAddress: "123 Agri Road",
            ApproximateAreaHa: 10m,
            Longitude: 106.6,
            Latitude: 10.7,
            EstimatedPoleCount: 100,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var httpResult = await controller.Submit(
            idempotencyKey: "   ", // whitespace only!
            request: request,
            cancellationToken: CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(httpResult);
    }

    [Fact]
    public async Task V3PublicSurveyRequestsControllerSubmitsAndReturns201Created()
    {
        var sender = new FakeSender();
        var callerScope = new FakeCallerScope();
        var expectedResponse = new SubmitNewCustomerSurveyRequestV3Response(
            Id: Guid.NewGuid(),
            RequestNumber: "REQ-2026-0001",
            TenantCode: "AGRI",
            TenantName: "Agri Farm",
            Kind: SurveyRequestKind.NewCustomer,
            Status: SurveyRequestStatus.Submitted,
            TrackingToken: "trk_test_1234567890abcdef",
            IndicativeQuote: new SurveyRequestQuoteResponse(
                BusinessPolicyVersionId: Guid.NewGuid(),
                PolicyFingerprint: "fp123",
                SurveyServiceRevisionId: Guid.NewGuid(),
                SurveyServiceRevisionVersion: 1,
                PriceVersionId: Guid.NewGuid(),
                PriceVersionNumber: 1,
                PricePerPole: 5000m,
                Currency: "VND",
                DepositPolicyId: Guid.NewGuid(),
                DepositVersionNumber: 1,
                DepositAmount: 100000m,
                EstimatedPoleCount: 100,
                IndicativeTotalAmount: 500000m,
                EvaluatedAt: T0),
            CreatedAt: T0);

        sender.RegisterHandler<SubmitNewCustomerSurveyRequestV3Command, Result<SubmitNewCustomerSurveyRequestV3Response>>(
            _ => Task.FromResult(Result.Success(expectedResponse)));

        var controller = new V3PublicSurveyRequestsController(sender, callerScope)
        {
            ControllerContext = CreateControllerContext()
        };

        var request = new SubmitNewCustomerSurveyRequestV3ApiRequest(
            SurveyServiceId: Guid.NewGuid(),
            TenantCode: "AGRI",
            TenantName: "Agri Farm",
            ApplicantName: "John Doe",
            ApplicantEmail: "john@example.com",
            ApplicantPhone: "0901234567",
            FarmName: "Green Farm",
            FarmAddress: "123 Agri Road",
            ApproximateAreaHa: 10m,
            Longitude: 106.6,
            Latitude: 10.7,
            EstimatedPoleCount: 100,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var httpResult = await controller.Submit(
            idempotencyKey: "idemp_test_valid",
            request: request,
            cancellationToken: CancellationToken.None);

        var jsonResult = Assert.IsType<JsonHttpResult<SubmitNewCustomerSurveyRequestV3Response>>(httpResult);
        Assert.Equal(StatusCodes.Status201Created, jsonResult.StatusCode);
        Assert.Equal(expectedResponse.RequestNumber, jsonResult.Value?.RequestNumber);
        Assert.Equal(expectedResponse.TrackingToken, jsonResult.Value?.TrackingToken);
    }

    [Fact]
    public async Task V3PublicSurveyRequestsControllerTrackReturns200WithMinimalStatus()
    {
        var sender = new FakeSender();
        var callerScope = new FakeCallerScope();
        var expectedTrack = new TrackSurveyRequestV3Response(
            RequestNumber: "REQ-2026-0001",
            Status: SurveyRequestStatus.Submitted,
            ServiceName: "Orthophoto Mapping",
            EstimatedPoleCount: 100,
            IndicativeTotalAmount: 500000m,
            DepositAmount: 100000m,
            Currency: "VND",
            EvaluatedAt: T0,
            SubmittedAt: T0);

        sender.RegisterHandler<TrackSurveyRequestV3Query, Result<TrackSurveyRequestV3Response>>(
            _ => Task.FromResult(Result.Success(expectedTrack)));

        var controller = new V3PublicSurveyRequestsController(sender, callerScope)
        {
            ControllerContext = CreateControllerContext()
        };

        var httpResult = await controller.Track(
            new TrackSurveyRequestV3ApiRequest("trk_valid_token_1234567890"),
            CancellationToken.None);

        var okResult = Assert.IsType<Ok<TrackSurveyRequestV3Response>>(httpResult);
        Assert.Equal(expectedTrack.RequestNumber, okResult.Value?.RequestNumber);
    }

    [Fact]
    public async Task V3TenantOwnerSurveyRequestsControllerSubmitsAndReturns201Created()
    {
        var sender = new FakeSender();
        var expectedResponse = new SubmitTenantOwnerSurveyRequestV3Response(
            Id: Guid.NewGuid(),
            RequestNumber: "REQ-2026-0002",
            TenantId: Guid.NewGuid(),
            FarmId: null,
            Kind: SurveyRequestKind.ExistingTenantNewFarm,
            Status: SurveyRequestStatus.Submitted,
            IndicativeQuote: new SurveyRequestQuoteResponse(
                BusinessPolicyVersionId: Guid.NewGuid(),
                PolicyFingerprint: "fp123",
                SurveyServiceRevisionId: Guid.NewGuid(),
                SurveyServiceRevisionVersion: 1,
                PriceVersionId: Guid.NewGuid(),
                PriceVersionNumber: 1,
                PricePerPole: 5000m,
                Currency: "VND",
                DepositPolicyId: Guid.NewGuid(),
                DepositVersionNumber: 1,
                DepositAmount: 100000m,
                EstimatedPoleCount: 150,
                IndicativeTotalAmount: 750000m,
                EvaluatedAt: T0),
            CreatedAt: T0);

        sender.RegisterHandler<SubmitTenantOwnerSurveyRequestV3Command, Result<SubmitTenantOwnerSurveyRequestV3Response>>(
            _ => Task.FromResult(Result.Success(expectedResponse)));

        var controller = new V3TenantOwnerSurveyRequestsController(sender)
        {
            ControllerContext = CreateControllerContext()
        };

        var request = new SubmitTenantOwnerSurveyRequestV3ApiRequest(
            SurveyServiceId: Guid.NewGuid(),
            FarmId: null,
            FarmName: "Proposed Farm",
            FarmAddress: "456 Farm Rd",
            ApproximateAreaHa: 12m,
            Longitude: 106.5,
            Latitude: 10.8,
            EstimatedPoleCount: 150,
            PreferredStartAt: null,
            PreferredEndAt: null,
            Notes: null);

        var httpResult = await controller.Submit(
            idempotencyKey: "idemp_tenant_owner_1",
            request: request,
            cancellationToken: CancellationToken.None);

        var jsonResult = Assert.IsType<JsonHttpResult<SubmitTenantOwnerSurveyRequestV3Response>>(httpResult);
        Assert.Equal(StatusCodes.Status201Created, jsonResult.StatusCode);
        Assert.Equal(expectedResponse.RequestNumber, jsonResult.Value?.RequestNumber);
    }

    [Fact]
    public async Task V3SystemAdminCommercialPoliciesControllerPublishesPoliciesSuccessfully()
    {
        var sender = new FakeSender();
        var serviceId = Guid.NewGuid();

        sender.RegisterHandler<CreateSurveyServiceV3Command, Result<CreateSurveyServiceV3Response>>(
            cmd => Task.FromResult(Result.Success(new CreateSurveyServiceV3Response(
                serviceId, cmd.Code, cmd.Name, cmd.Description, "V3", "ACTIVE", T0))));

        sender.RegisterHandler<PublishSurveyServiceRevisionCommand, Result<PublishSurveyServiceRevisionResponse>>(
            cmd => Task.FromResult(Result.Success(new PublishSurveyServiceRevisionResponse(
                Guid.NewGuid(), cmd.SurveyServiceId, 1, null, cmd.Name, cmd.Description, cmd.MapsPoles, cmd.ScreensDisease, T0, null, cmd.ReasonCode, cmd.EvidenceReference))));

        sender.RegisterHandler<PublishSurveyServicePriceV3Command, Result<PublishSurveyServicePriceV3Response>>(
            cmd => Task.FromResult(Result.Success(new PublishSurveyServicePriceV3Response(
                Guid.NewGuid(), cmd.SurveyServiceId, 1, null, cmd.AmountPerPole, cmd.Currency, T0, null, cmd.ReasonCode, cmd.EvidenceReference))));

        sender.RegisterHandler<PublishSurveyDepositPolicyCommand, Result<PublishSurveyDepositPolicyResponse>>(
            cmd => Task.FromResult(Result.Success(new PublishSurveyDepositPolicyResponse(
                Guid.NewGuid(), 1, null, cmd.Amount, cmd.Currency, T0, null, cmd.ReasonCode, cmd.EvidenceReference))));

        sender.RegisterHandler<PublishClaimPolicyCommand, Result<PublishClaimPolicyResponse>>(
            cmd => Task.FromResult(Result.Success(new PublishClaimPolicyResponse(
                Guid.NewGuid(), 1, null, cmd.CalendarWindowDays, cmd.TimeZoneId, cmd.ExcludeNoticeDate, T0, null, cmd.ReasonCode, cmd.EvidenceReference))));

        sender.RegisterHandler<PublishCancellationPolicyCommand, Result<PublishCancellationPolicyResponse>>(
            cmd => Task.FromResult(Result.Success(new PublishCancellationPolicyResponse(
                Guid.NewGuid(), 1, null, cmd.RefundableBeforeCapture, cmd.RetainDepositAfterCapture, cmd.AllowedFreeNoShowReschedules, cmd.OperationalPostponementRefundable, cmd.ServiceFailureRefundable, T0, null, cmd.ReasonCode, cmd.EvidenceReference))));

        sender.RegisterHandler<PublishBusinessPolicyVersionCommand, Result<PublishBusinessPolicyVersionResponse>>(
            cmd => Task.FromResult(Result.Success(new PublishBusinessPolicyVersionResponse(
                Guid.NewGuid(), cmd.StreamKey, 1, null, cmd.SurveyDepositPolicyId, cmd.ClaimPolicyId, cmd.CancellationPolicyId, cmd.ServiceRevisionIds.ToArray(), cmd.PricingCurrency, "fp_test", T0, null, cmd.ReasonCode, cmd.EvidenceReference))));

        sender.RegisterHandler<TransitionBusinessPolicyActivationCommand, Result<TransitionBusinessPolicyActivationResponse>>(
            cmd => Task.FromResult(Result.Success(new TransitionBusinessPolicyActivationResponse(
                Guid.NewGuid(), cmd.BusinessPolicyVersionId, cmd.PublicIntakeEnabled, cmd.CaptureEnabled, cmd.ReleaseEnabled, Guid.NewGuid(), T0, cmd.ReasonCode, cmd.EvidenceReference))));

        var controller = new V3SystemAdminCommercialPoliciesController(sender)
        {
            ControllerContext = CreateControllerContext()
        };

        var serviceResult = await controller.CreateService(
            new CreateSurveyServiceV3ApiRequest("MAP", "Orthophoto Mapping", "Desc", true),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<JsonHttpResult<CreateSurveyServiceV3Response>>(serviceResult).StatusCode);

        var revResult = await controller.PublishRevision(
            new PublishSurveyServiceRevisionApiRequest(serviceId, "MAP Rev", "Desc", true, false, null, "INIT", null),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<JsonHttpResult<PublishSurveyServiceRevisionResponse>>(revResult).StatusCode);

        var priceResult = await controller.PublishPrice(
            new PublishSurveyServicePriceV3ApiRequest(serviceId, 5000m, "VND", null, "INIT", null),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<JsonHttpResult<PublishSurveyServicePriceV3Response>>(priceResult).StatusCode);

        var depResult = await controller.PublishDepositPolicy(
            new PublishSurveyDepositPolicyApiRequest(100000m, "VND", null, "INIT", null),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<JsonHttpResult<PublishSurveyDepositPolicyResponse>>(depResult).StatusCode);

        var claimResult = await controller.PublishClaimPolicy(
            new PublishClaimPolicyApiRequest(7, "Asia/Ho_Chi_Minh", true, null, "INIT", null),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<JsonHttpResult<PublishClaimPolicyResponse>>(claimResult).StatusCode);

        var cancelResult = await controller.PublishCancellationPolicy(
            new PublishCancellationPolicyApiRequest(true, true, 1, true, true, null, "INIT", null),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<JsonHttpResult<PublishCancellationPolicyResponse>>(cancelResult).StatusCode);

        var bundleResult = await controller.PublishBundleVersion(
            new PublishBusinessPolicyVersionApiRequest("DEFAULT", [Guid.NewGuid()], Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "VND", null, "INIT", null),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<JsonHttpResult<PublishBusinessPolicyVersionResponse>>(bundleResult).StatusCode);

        var actResult = await controller.TransitionActivation(
            new TransitionBusinessPolicyActivationApiRequest(Guid.NewGuid(), false, false, false, null, "INIT", null),
            CancellationToken.None);
        Assert.IsType<Ok<TransitionBusinessPolicyActivationResponse>>(actResult);
    }

    [Fact]
    public async Task V3SystemAdminSurveyRequestsControllerApprovesAtomicallyAndReturns200()
    {
        var sender = new FakeSender();
        var requestId = Guid.NewGuid();
        var expectedResponse = new ApproveSurveyRequestResponse(
            SurveyRequestId: requestId,
            RequestNumber: "REQ-2026-0001",
            RequestStatus: SurveyRequestStatus.Approved,
            RequestVersion: 2,
            ApprovalReviewId: Guid.NewGuid(),
            SurveyOrderId: Guid.NewGuid(),
            OrderNumber: "SO-2026-0001",
            OrderStatus: SurveyOrderStatus.AwaitingBaselineAppointment,
            TenantId: Guid.NewGuid(),
            FarmId: Guid.NewGuid(),
            SystemManagerProfileId: Guid.NewGuid(),
            PrimaryManagerAssignmentId: Guid.NewGuid(),
            OwnerInvitationId: Guid.NewGuid(),
            RequiresBaselineMapping: true,
            PreviousCompatibleOrderId: null,
            PreviousOrderCompatibilityPolicyVersion: null,
            PreviousOrderCompatibilityReason: null,
            ApprovedAt: T0);

        sender.RegisterHandler<ApproveSurveyRequestCommand, Result<ApproveSurveyRequestResponse>>(
            _ => Task.FromResult(Result.Success(expectedResponse)));

        var controller = new V3SystemAdminSurveyRequestsController(sender)
        {
            ControllerContext = CreateControllerContext()
        };

        var checklist = new SurveyRequestReviewChecklistRequest(
            ContactValid: true,
            DragonFruitFarmConfirmed: true,
            ServiceAreaSupported: true,
            CandidateLocationSufficient: true,
            Notes: null);

        var approveRequest = new ApproveSurveyRequest(
            SelectedSystemManagerProfileId: Guid.NewGuid(),
            Reason: "All requirements met and verified.",
            Checklist: checklist,
            ExpectedVersion: 1);

        var httpResult = await controller.Approve(
            requestId: requestId,
            idempotencyKey: "idemp_approve_1",
            request: approveRequest,
            cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<Ok<ApproveSurveyRequestApiResponse>>(httpResult);
        Assert.Equal(expectedResponse.OrderNumber, okResult.Value?.OrderNumber);
        Assert.Equal("AWAITING_BASELINE_APPOINTMENT", okResult.Value?.OrderStatus);
    }

    private static Microsoft.AspNetCore.Mvc.ControllerContext CreateControllerContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v3/test";
        httpContext.TraceIdentifier = "trace_test_123";
        return new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = httpContext };
    }

    private sealed class FakeCallerScope : IPublicSurveyRequestCallerScope
    {
        public string GetOrCreate(HttpContext httpContext) => "public-session:test-session-id";
    }

    private sealed class FakeSender : ISender
    {
        private readonly Dictionary<Type, Func<object, Task<object>>> _handlers = [];

        public void RegisterHandler<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handler)
        {
            _handlers[typeof(TRequest)] = async obj => (await handler((TRequest)obj))!;
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (_handlers.TryGetValue(request.GetType(), out var handler))
            {
                var result = await handler(request);
                return (TResponse)result;
            }

            throw new InvalidOperationException($"No fake handler registered for request {request.GetType().Name}");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
