using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.PreviewTenantInvitation;

internal sealed class PreviewTenantInvitationQueryValidator
    : AbstractValidator<PreviewTenantInvitationQuery>
{
    public PreviewTenantInvitationQueryValidator()
    {
        RuleFor(query => query.Token)
            .NotEmpty()
            .MaximumLength(512);
    }
}
