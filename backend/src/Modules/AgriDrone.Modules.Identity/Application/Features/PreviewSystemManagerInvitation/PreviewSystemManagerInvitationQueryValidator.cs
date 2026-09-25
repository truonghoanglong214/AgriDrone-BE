using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.PreviewSystemManagerInvitation;

internal sealed class PreviewSystemManagerInvitationQueryValidator
    : AbstractValidator<PreviewSystemManagerInvitationQuery>
{
    public PreviewSystemManagerInvitationQueryValidator()
    {
        RuleFor(query => query.Token)
            .NotEmpty()
            .MaximumLength(512);
    }
}