using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantOwner
{
    internal sealed class InviteTenantOwnerCommandValidator : AbstractValidator<InviteTenantOwnerCommand>
    {
        public InviteTenantOwnerCommandValidator()
        {
            RuleFor(invite => invite.email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");
        }
    }
}
