using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfile;

internal sealed class GetSystemManagerProfileQueryValidator
    : AbstractValidator<GetSystemManagerProfileQuery>
{
    public GetSystemManagerProfileQueryValidator()
    {
        RuleFor(query => query.ProfileId).NotEmpty();
    }
}
