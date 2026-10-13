using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.AcceptSystemManagerInvitation;

public sealed record AcceptSystemManagerInvitationCommand(
    string Token,
    string? Password,
    string? FullName,
    string? Phone)
    : IRequest<Result<AcceptSystemManagerInvitationResponse>>;