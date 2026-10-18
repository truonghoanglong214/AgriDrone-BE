using AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.CreateAdjustmentSettlement;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.ConfirmAdjustmentSettlement;

public sealed record ConfirmAdjustmentSettlementCommand(
    Guid SettlementId,
    string? ProviderReference,
    uint ExpectedVersion)
    : IRequest<Result<AdjustmentSettlementResponse>>;
