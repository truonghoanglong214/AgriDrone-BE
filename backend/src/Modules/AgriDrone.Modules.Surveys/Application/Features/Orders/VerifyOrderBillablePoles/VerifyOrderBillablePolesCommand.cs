using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.VerifyOrderBillablePoles;

public sealed record VerifyOrderBillablePolesCommand(
    Guid OrderId) : IRequest<Result<int>>;
