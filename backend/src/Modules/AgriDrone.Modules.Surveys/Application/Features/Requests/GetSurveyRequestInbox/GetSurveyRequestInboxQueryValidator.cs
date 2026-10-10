using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestInbox;

internal sealed class GetSurveyRequestInboxQueryValidator
    : AbstractValidator<GetSurveyRequestInboxQuery>
{
    public GetSurveyRequestInboxQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => !status.HasValue || Enum.IsDefined(status.Value))
            .WithMessage("Status is not supported.");
        RuleFor(query => query.Kind)
            .Must(kind => !kind.HasValue || Enum.IsDefined(kind.Value))
            .WithMessage("Kind is not supported.");
        RuleFor(query => query.SurveyServiceId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("SurveyServiceId cannot be empty when provided.");
        RuleFor(query => query.CreatedFrom)
            .Must(IsUtcWhenProvided)
            .WithMessage("CreatedFrom must use the UTC offset.");
        RuleFor(query => query.CreatedTo)
            .Must(IsUtcWhenProvided)
            .WithMessage("CreatedTo must use the UTC offset.");
        RuleFor(query => query)
            .Must(query =>
                !query.CreatedFrom.HasValue ||
                !query.CreatedTo.HasValue ||
                query.CreatedFrom.Value < query.CreatedTo.Value)
            .WithMessage("CreatedFrom must be earlier than CreatedTo.");
    }

    private static bool IsUtcWhenProvided(DateTimeOffset? value) =>
        !value.HasValue ||
        value.Value != default && value.Value.Offset == TimeSpan.Zero;
}
