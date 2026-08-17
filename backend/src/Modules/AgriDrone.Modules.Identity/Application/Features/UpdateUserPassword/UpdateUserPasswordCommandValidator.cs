using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateUserPassword
{
    internal sealed class UpdateUserPasswordCommandValidator : AbstractValidator<UpdateUserPasswordCommand>
    {
        public UpdateUserPasswordCommandValidator()
        {
            RuleFor(x => x.newPassword)
                .NotEmpty().WithMessage("New password is required.");

            RuleFor(x => x.oldPassword)
                .NotEmpty().WithMessage("Old password is required.");
        }
    }
}
