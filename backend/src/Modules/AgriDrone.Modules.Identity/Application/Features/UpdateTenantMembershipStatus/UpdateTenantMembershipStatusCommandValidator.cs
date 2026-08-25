using AgriDrone.SharedKernel.Domain;
using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateTenantMembershipStatus;

internal sealed class UpdateTenantMembershipStatusCommandValidator
    : AbstractValidator<UpdateTenantMembershipStatusCommand>
{
    public UpdateTenantMembershipStatusCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Status)
            .Must(status => status is
                GeneralStatus.Active or GeneralStatus.Inactive)
            .WithMessage("Status must be ACTIVE or INACTIVE.");
    }
}
