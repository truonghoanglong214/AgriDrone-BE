using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal static class SystemManagerValidationRules
{
    public const int ReasonMaximumLength = 1000;

    public static IRuleBuilderOptions<T, string> ValidReason<T>(
        this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(ReasonMaximumLength);
}
