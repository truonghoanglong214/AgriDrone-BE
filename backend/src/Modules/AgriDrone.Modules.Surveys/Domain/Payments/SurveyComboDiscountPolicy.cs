using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyComboDiscountPolicy : Entity
{
    private SurveyComboDiscountPolicy() { }

    public int VersionNumber { get; private set; }
    public decimal DiscountPercentage { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }

    public static SurveyComboDiscountPolicy Create(
        int versionNumber,
        decimal discountPercentage,
        Guid createdBy,
        DateTimeOffset effectiveFrom)
    {
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(effectiveFrom);
        ArgumentOutOfRangeException.ThrowIfNegative(discountPercentage);
        discountPercentage = decimal.Round(discountPercentage, 2, MidpointRounding.AwayFromZero);
        if (versionNumber < 1 || discountPercentage < 0m || discountPercentage >= 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountPercentage),
                "The combo discount percentage must be at least 0 and less than 100.");
        }

        return new SurveyComboDiscountPolicy
        {
            Id = Guid.CreateVersion7(),
            VersionNumber = versionNumber,
            DiscountPercentage = decimal.Round(discountPercentage, 2, MidpointRounding.AwayFromZero),
            CreatedBy = createdBy,
            EffectiveFrom = effectiveFrom
        };
    }

    public void CloseAt(DateTimeOffset effectiveTo)
    {
        DomainGuard.Utc(effectiveTo);
        if (EffectiveTo.HasValue || effectiveTo <= EffectiveFrom)
        {
            throw new InvalidOperationException(
                "The combo discount policy is already closed or has an invalid end time.");
        }

        EffectiveTo = effectiveTo;
    }
}

