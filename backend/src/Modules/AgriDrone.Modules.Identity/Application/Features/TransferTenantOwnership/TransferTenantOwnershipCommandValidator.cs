using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.TransferTenantOwnership
{
    internal sealed class TransferTenantOwnershipCommandValidator : AbstractValidator<TransferTenantOwnershipCommand>
    {
        public TransferTenantOwnershipCommandValidator()
        {
            RuleFor(command => command.NewOwnerUserId)
                .NotEmpty()
                .WithMessage("New tenant owner id is required.");
        }
    }
}
