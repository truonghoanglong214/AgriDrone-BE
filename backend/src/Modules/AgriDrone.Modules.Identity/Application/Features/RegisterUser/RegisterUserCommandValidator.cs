using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.RegisterUser
{
    internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserCommandValidator() 
        {
            RuleFor(x => x.email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");

            RuleFor(x => x.password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters long.");

            RuleFor(x => x.fullName)
                .NotEmpty().WithMessage("Full name is required.")
                .MaximumLength(150).WithMessage("Full Name must be less than 150 characters long.");

            RuleFor(x => x.phone)
                .NotEmpty().WithMessage("Phone is required.")
                .MaximumLength(11).WithMessage("Phone number must be less than 11 numbers.");

            RuleFor(x => x.tenantCode)
                .NotEmpty().WithMessage("Tenant Code is required.")
                .MaximumLength(30).WithMessage("Tenant Code must be less than 30 characters long.");

            RuleFor(x => x.tenantName)
                .NotEmpty().WithMessage("Tenant Name is required.")
                .MaximumLength(150).WithMessage("Tenant Name must be less than 150 characters long.");
        }
    }
}
