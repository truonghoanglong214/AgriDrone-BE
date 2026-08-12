using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Diseases;

public sealed class Disease : Entity
{
    private Disease()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? ScientificName { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
