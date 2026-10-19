using AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.CreateAdjustmentSettlement;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.FailAdjustmentSettlement;

public sealed record FailAdjustmentSettlementCommand(
    Guid SettlementId,
    string FailureReason,
    uint ExpectedVersion)
    : IRequest<Result<AdjustmentSettlementResponse>>;
