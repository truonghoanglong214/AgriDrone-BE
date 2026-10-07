namespace AgriDrone.Modules.Surveys.Domain;

public readonly record struct PricePerPole
{
    private PricePerPole(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static PricePerPole Create(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        return new PricePerPole(Money.Round(amount));
    }
}
