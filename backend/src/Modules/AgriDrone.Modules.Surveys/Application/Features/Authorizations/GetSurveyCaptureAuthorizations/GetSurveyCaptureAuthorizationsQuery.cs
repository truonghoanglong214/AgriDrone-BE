using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.GetSurveyCaptureAuthorizationDetail;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Authorizations.GetSurveyCaptureAuthorizations;

public sealed record GetSurveyCaptureAuthorizationsQuery(
    Guid SurveyOrderId)
    : IRequest<Result<IReadOnlyList<SurveyCaptureAuthorizationDetailResponse>>>;

internal sealed class GetSurveyCaptureAuthorizationsQueryHandler(
    ISurveyCaptureAuthorizationRepository repository)
    : IRequestHandler<GetSurveyCaptureAuthorizationsQuery,
        Result<IReadOnlyList<SurveyCaptureAuthorizationDetailResponse>>>
{
    public async Task<Result<IReadOnlyList<SurveyCaptureAuthorizationDetailResponse>>> Handle(
        GetSurveyCaptureAuthorizationsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.ListByOrderIdAsync(
            query.SurveyOrderId, cancellationToken);

        var response = items.Select(authorization => new SurveyCaptureAuthorizationDetailResponse(
            authorization.Id,
            authorization.SurveyOrderId,
            authorization.Type,
            authorization.Status,
            authorization.EvidenceReference,
            authorization.IssuedAt,
            authorization.VerifiedAt,
            authorization.ExpiresAt,
            authorization.RevokedAt,
            authorization.VerifiedBy,
            authorization.Version)).ToList();

        return Result.Success<IReadOnlyList<SurveyCaptureAuthorizationDetailResponse>>(response);
    }
}
