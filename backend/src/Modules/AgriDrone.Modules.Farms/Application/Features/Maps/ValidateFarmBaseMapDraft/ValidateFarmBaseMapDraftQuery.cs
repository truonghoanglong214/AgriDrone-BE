using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ValidateFarmBaseMapDraft;

public sealed record ValidateFarmBaseMapDraftQuery(
    Guid FarmId,
    Guid DraftId) : IRequest<Result<BaselineMappingBatchValidationResult>>;
