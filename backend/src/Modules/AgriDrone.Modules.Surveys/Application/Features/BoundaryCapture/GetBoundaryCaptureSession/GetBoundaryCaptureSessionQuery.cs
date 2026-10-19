using AgriDrone.Modules.Surveys.Application.Abstractions.BoundaryCapture;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.BoundaryCapture;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.BoundaryCapture.GetBoundaryCaptureSession;

public sealed record GetBoundaryCaptureSessionQuery(
    Guid SurveyOrderId,
    Guid SessionId)
    : IRequest<Result<BoundaryCaptureResponse>>;

internal sealed class GetBoundaryCaptureSessionQueryHandler(
    IBoundaryCaptureRepository repository,
    IExecutionContext executionContext)
    : IRequestHandler<GetBoundaryCaptureSessionQuery, Result<BoundaryCaptureResponse>>
{
    public async Task<Result<BoundaryCaptureResponse>> Handle(
        GetBoundaryCaptureSessionQuery query,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is null ||
            executionContext.ActorId == Guid.Empty)
        {
            return Result.Failure<BoundaryCaptureResponse>(BoundaryCaptureError.Forbidden());
        }

        var session = await repository.GetAsync(
            query.SurveyOrderId,
            query.SessionId,
            cancellationToken);

        if (session is null)
        {
            return Result.Failure<BoundaryCaptureResponse>(
                BoundaryCaptureError.NotFound(query.SessionId));
        }

        // Project directly to BoundaryCaptureResponse
        var response = new BoundaryCaptureResponse(
            session.SurveyOrderId,
            session.Id,
            session.StartOperationId,
            session.FarmId,
            session.AppointmentId,
            session.AppointmentVersion,
            session.SystemManagerProfileId,
            session.DroneId,
            session.Status,
            session.FarmBoundaryVersionId,
            null, // BaselineRoutePlanId will be updated by caller/order projection if needed
            SurveyOrderStatus.AwaitingBoundaryCapture,
            0,
            session.Version,
            session.Samples
                .OrderBy(item => item.SequenceNumber)
                .Select(item => new BoundaryCaptureSampleResponse(
                    item.Id,
                    item.ClientOperationId,
                    item.SequenceNumber,
                    item.Latitude,
                    item.Longitude,
                    item.HorizontalAccuracyMeters,
                    item.CapturedAt,
                    item.ReceivedAt))
                .ToArray());

        return Result.Success(response);
    }
}
