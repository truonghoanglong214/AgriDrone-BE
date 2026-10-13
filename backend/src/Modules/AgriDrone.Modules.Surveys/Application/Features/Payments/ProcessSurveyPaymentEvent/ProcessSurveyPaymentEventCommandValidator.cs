using AgriDrone.Modules.Surveys.Application.Features.Payments.Common;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;

internal sealed class ProcessSurveyPaymentEventCommandValidator
    : AbstractValidator<ProcessSurveyPaymentEventCommand>
{
    public ProcessSurveyPaymentEventCommandValidator()
    {
        RuleFor(command => command.Provider).NotEmpty().MaximumLength(50);
        RuleFor(command => command.ProviderReference)
            .NotEmpty()
            .MaximumLength(150);
        RuleFor(command => command.ProviderEventId)
            .NotEmpty()
            .MaximumLength(150);
        RuleFor(command => command.EventType)
            .NotEmpty()
            .MaximumLength(100)
            .Must(SurveyPaymentCommandSupport.IsSupportedEvent)
            .WithMessage("The provider payment event type is not supported.");
        RuleFor(command => command.Amount).GreaterThan(0m);
        RuleFor(command => command.Currency)
            .Matches("^[A-Z]{3}$");
        RuleFor(command => command.OccurredAt)
            .Must(value => value != default && value.Offset == TimeSpan.Zero);
        RuleFor(command => command.ReceivedAt)
            .Must(value => value != default && value.Offset == TimeSpan.Zero);
        RuleFor(command => command.Payload).NotNull();
    }
}
