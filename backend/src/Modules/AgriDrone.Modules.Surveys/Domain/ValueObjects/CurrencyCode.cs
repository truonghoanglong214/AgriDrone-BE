namespace AgriDrone.Modules.Surveys.Domain;

public readonly record struct CurrencyCode
{
    private CurrencyCode(string value) => Value = value;
    public string Value { get; }
    public static CurrencyCode Vnd { get; } = new("VND");

    public static CurrencyCode Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException(
                "Currency must be a three-letter ISO 4217 code.",
                nameof(value));
        }

        return new CurrencyCode(normalized);
    }

    public override string ToString() => Value;
}
