using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Authorizations.GetSurveyCaptureAuthorizationDetail;

internal sealed class GetSurveyCaptureAuthorizationDetailQueryHandler(
    ISurveyCaptureAuthorizationRepository repository)
    : IRequestHandler<GetSurveyCaptureAuthorizationDetailQuery,
        Result<SurveyCaptureAuthorizationDetailResponse>>
{
    public async Task<Result<SurveyCaptureAuthorizationDetailResponse>> Handle(
        GetSurveyCaptureAuthorizationDetailQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = await repository.GetAsync(
            query.SurveyOrderId, query.Type, cancellationToken);

        if (authorization is null)
        {
            return Result.Failure<SurveyCaptureAuthorizationDetailResponse>(
                SurveyCaptureAuthorizationError.NotFound());
        }

        return Result.Success(new SurveyCaptureAuthorizationDetailResponse(
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
            authorization.Version));
    }
}
