using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.GetBoundaryExceptions;

public sealed record BoundaryExceptionDto(
    Guid Id,
    Guid FarmId,
    Guid FarmBoundaryVersionId,
    string Source,
    string SourceReferenceId,
    Guid? SurveyOrderId,
    Guid? MissionId,
    Point OriginalPosition,
    string State,
    decimal MeasuredDistanceMeters,
    decimal ThresholdMeters,
    string? Decision,
    Point? CorrectedPosition,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewReason,
    uint Version,
    DateTimeOffset CreatedAt);

public sealed record GetBoundaryExceptionsQuery(
    Guid FarmId,
    BoundaryExceptionState? State = null)
    : IRequest<Result<IReadOnlyList<BoundaryExceptionDto>>>;

internal sealed class GetBoundaryExceptionsHandler(
    IBoundaryExceptionRepository exceptionRepository,
    ISystemManagerAccessService managerAccessService)
    : IRequestHandler<GetBoundaryExceptionsQuery, Result<IReadOnlyList<BoundaryExceptionDto>>>
{
    public async Task<Result<IReadOnlyList<BoundaryExceptionDto>>> Handle(
        GetBoundaryExceptionsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            query.FarmId,
            cancellationToken);

        if (!access.IsAllowed || access.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<BoundaryExceptionDto>>(FarmError.NotFound());
        }

        var list = await exceptionRepository.ListByFarmAsync(
            tenantId,
            query.FarmId,
            query.State,
            cancellationToken);

        var dtos = list.Select(e => new BoundaryExceptionDto(
            e.Id,
            e.FarmId,
            e.FarmBoundaryVersionId,
            e.Source.ToString(),
            e.SourceReferenceId,
            e.SurveyOrderId,
            e.MissionId,
            e.OriginalPosition,
            e.State.ToString(),
            e.MeasuredDistanceMeters,
            e.ThresholdMeters,
            e.Decision?.ToString(),
            e.CorrectedPosition,
            e.ReviewedBy,
            e.ReviewedAt,
            e.ReviewReason,
            e.Version,
            e.CreatedAt)).ToList();

        return Result.Success<IReadOnlyList<BoundaryExceptionDto>>(dtos);
    }
}
