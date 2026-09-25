using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.PreviewSystemManagerInvitation;

public sealed record PreviewSystemManagerInvitationQuery(
    string Token)
    : IRequest<Result<PreviewSystemManagerInvitationResponse>>;