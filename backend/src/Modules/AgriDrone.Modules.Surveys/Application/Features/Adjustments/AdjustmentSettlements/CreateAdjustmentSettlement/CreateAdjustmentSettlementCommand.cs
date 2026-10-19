using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.CreateAdjustmentSettlement;

public sealed record CreateAdjustmentSettlementCommand(
    Guid PriceAdjustmentId,
    AdjustmentSettlementType Type,
    decimal Amount,
    string Currency,
    string IdempotencyKey)
    : IRequest<Result<AdjustmentSettlementResponse>>;

public sealed record AdjustmentSettlementResponse(
    Guid Id,
    Guid PriceAdjustmentId,
    AdjustmentSettlementType Type,
    AdjustmentSettlementStatus Status,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? ProviderReference,
    string? FailureReason,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
