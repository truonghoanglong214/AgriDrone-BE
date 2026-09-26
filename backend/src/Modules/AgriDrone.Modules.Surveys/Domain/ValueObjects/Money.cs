namespace AgriDrone.Modules.Surveys.Domain;

public readonly record struct Money
{
    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public CurrencyCode Currency { get; }

    public static Money Create(decimal amount, CurrencyCode currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return new Money(Round(amount), currency);
    }

    public static Money CalculateSurveyPrice(decimal confirmedAreaHa, Money pricePerHa)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(confirmedAreaHa);
        return Create(confirmedAreaHa * pricePerHa.Amount, pricePerHa.Currency);
    }

    public static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
