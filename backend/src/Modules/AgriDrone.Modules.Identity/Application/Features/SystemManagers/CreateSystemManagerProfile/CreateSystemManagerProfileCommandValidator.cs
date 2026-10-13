using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class CreateSystemManagerProfileCommandValidator
    : AbstractValidator<CreateSystemManagerProfileCommand>
{
    public CreateSystemManagerProfileCommandValidator() =>
        RuleFor(command => command.UserId).NotEmpty();
}
