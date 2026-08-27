using System.Runtime.CompilerServices;

namespace AgriDrone.SharedKernel.Domain;

public static class DomainGuard
{
    public static Guid NotEmpty(
        Guid value,
        [CallerArgumentExpression(nameof(value))]
        string? parameterName = null)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Identifier cannot be empty.",
                parameterName);
        }

        return value;
    }

    public static DateTimeOffset Utc(
        DateTimeOffset value,
        [CallerArgumentExpression(nameof(value))]
        string? parameterName = null)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be a non-default UTC value.",
                parameterName);
        }

        return value;
    }
}
