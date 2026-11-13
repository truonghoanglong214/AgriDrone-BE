using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class LegacySurveyDepositPolicy : Entity
{
    private LegacySurveyDepositPolicy() { }

    public int VersionNumber { get; private set; }
    public decimal AmountVnd { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }

    public static LegacySurveyDepositPolicy Create(
        int versionNumber,
        decimal amountVnd,
        Guid createdBy,
        DateTimeOffset effectiveFrom)
    {
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(effectiveFrom);
        if (versionNumber < 1 || amountVnd < 0m ||
            amountVnd != decimal.Truncate(amountVnd))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountVnd),
                "The shared VND deposit must be a non-negative whole amount.");
        }

        return new LegacySurveyDepositPolicy
        {
            Id = Guid.CreateVersion7(),
            VersionNumber = versionNumber,
            AmountVnd = amountVnd,
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
                "The deposit policy is already closed or has an invalid end time.");
        }

        EffectiveTo = effectiveTo;
    }
}
