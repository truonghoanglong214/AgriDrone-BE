using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Harvests.Domain.Seasons;

public sealed class Season : AggregateRoot
{
    private Season()
    {
    }

    public Guid FarmId { get; private set; }

    public string Name { get; private set; } = null!;

    public short Year { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public SeasonStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
