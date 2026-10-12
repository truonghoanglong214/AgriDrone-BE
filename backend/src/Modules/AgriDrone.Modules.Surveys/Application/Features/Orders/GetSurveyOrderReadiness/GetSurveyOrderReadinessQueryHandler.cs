using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;

internal sealed class GetSurveyOrderReadinessQueryHandler(
    ISurveyOrderReadinessService readinessService,
    IExecutionContext executionContext)
    : IRequestHandler<GetSurveyOrderReadinessQuery,
        Result<SurveyOrderReadinessResponse>>
{
    public async Task<Result<SurveyOrderReadinessResponse>> Handle(
        GetSurveyOrderReadinessQuery query,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<SurveyOrderReadinessResponse>(
                SurveyAppointmentError.Forbidden());
        }

        var evaluationResult = await readinessService.EvaluateAsync(
            query.SurveyOrderId,
            query.MissionId,
            query.Purpose,
            cancellationToken);
        if (evaluationResult.IsFailure)
        {
            return Result.Failure<SurveyOrderReadinessResponse>(
                evaluationResult.Error);
        }

        var evaluation = evaluationResult.Value;
        if (evaluation.SystemManagerUserId != actorId)
        {
            return Result.Failure<SurveyOrderReadinessResponse>(
                SurveyAppointmentError.Forbidden());
        }

        return Result.Success(new SurveyOrderReadinessResponse(
            evaluation.SurveyOrderId,
            evaluation.MissionId,
            evaluation.TenantId,
            evaluation.FarmId,
            MapPurpose(evaluation.Purpose),
            evaluation.OrderVersion,
            evaluation.AppointmentId,
            evaluation.SystemManagerProfileId,
            evaluation.IsReady,
            evaluation.FailureCodes,
            evaluation.EvaluatedAt));
    }

    private static string MapPurpose(Domain.SurveyOperationPurpose purpose) =>
        purpose switch
        {
            Domain.SurveyOperationPurpose.BaselineMapping =>
                "BASELINE_MAPPING",
            Domain.SurveyOperationPurpose.PlantHealth => "PLANT_HEALTH",
            Domain.SurveyOperationPurpose.HarvestReadiness =>
                "HARVEST_READINESS",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
}
