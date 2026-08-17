using FluentValidation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUser
{
    internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
    {
        public UpdateUserCommandValidator()
        {

            RuleFor(x => x.fullName)
                .NotEmpty().WithMessage("Full name is required.");

            RuleFor(x => x.phone)
                .NotEmpty().WithMessage("Full name is required.");
        }
    }
}
