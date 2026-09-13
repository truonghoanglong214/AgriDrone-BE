using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.PreviewTenantInvitation;

public sealed record PreviewTenantInvitationQuery(string Token)
    : IRequest<Result<PreviewTenantInvitationResponse>>;
