using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

internal static class PolicyValidationGuard
{
    public static void ValidateVersionAndPredecessor(
        Guid id,
        int versionNumber,
        Guid? predecessorVersionId)
    {
        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(versionNumber),
                versionNumber,
                "Version number must be at least 1.");
        }

        if (versionNumber == 1 && predecessorVersionId.HasValue)
        {
            throw new ArgumentException(
                "Initial version cannot have a predecessor.",
                nameof(predecessorVersionId));
        }

        if (versionNumber > 1 && !predecessorVersionId.HasValue)
        {
            throw new ArgumentException(
                "Subsequent versions must reference a predecessor version.",
                nameof(predecessorVersionId));
        }

        if (predecessorVersionId.HasValue && predecessorVersionId.Value == id)
        {
            throw new ArgumentException(
                "A version cannot reference itself as its predecessor.",
                nameof(predecessorVersionId));
        }

        if (predecessorVersionId.HasValue && predecessorVersionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Predecessor identifier cannot be empty.",
                nameof(predecessorVersionId));
        }
    }

    public static void ValidateEffectiveWindow(
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo)
    {
        DomainGuard.Utc(effectiveFrom, nameof(effectiveFrom));
        if (effectiveTo.HasValue)
        {
            DomainGuard.Utc(effectiveTo.Value, nameof(effectiveTo));
            if (effectiveTo.Value <= effectiveFrom)
            {
                throw new ArgumentException(
                    "Effective end must be after its start.",
                    nameof(effectiveTo));
            }
        }
    }

    public static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    public static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    public static decimal ValidatePositiveWholeVnd(
        decimal amount,
        string parameterName)
    {
        if (amount <= 0 || decimal.Floor(amount) != amount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                amount,
                "Amount must be a positive whole VND value.");
        }

        return amount;
    }

    public static string ValidateVndCurrency(
        CurrencyCode currency,
        string parameterName)
    {
        if (currency != CurrencyCode.Vnd)
        {
            throw new ArgumentException(
                "Only VND currency is supported.",
                parameterName);
        }

        return currency.Value;
    }
}
