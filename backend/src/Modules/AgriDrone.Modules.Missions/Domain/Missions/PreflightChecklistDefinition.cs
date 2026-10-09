using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Missions;

public sealed class PreflightChecklistDefinition : AggregateRoot
{
    private PreflightChecklistDefinition() { }

    public string Code { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public PreflightChecklistDefinitionStatus Status { get; private set; }
    public JsonDocument Items { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public ICollection<MissionPreflightChecklist> MissionChecklists { get; private set; } = [];

    public static PreflightChecklistDefinition CreateActive(
        string code,
        int versionNumber,
        JsonDocument items,
        Guid createdBy,
        DateTimeOffset effectiveFrom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        ArgumentNullException.ThrowIfNull(items);
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(effectiveFrom);

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length > 50 ||
            items.RootElement.ValueKind != JsonValueKind.Array ||
            items.RootElement.GetArrayLength() == 0)
        {
            throw new ArgumentException(
                "A checklist requires a code and a non-empty JSON array of items.",
                nameof(items));
        }

        return new PreflightChecklistDefinition
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            VersionNumber = versionNumber,
            Status = PreflightChecklistDefinitionStatus.Active,
            Items = JsonDocument.Parse(items.RootElement.GetRawText()),
            EffectiveFrom = effectiveFrom,
            CreatedBy = createdBy,
            CreatedAt = effectiveFrom
        };
    }

    public void Retire(DateTimeOffset retiredAt)
    {
        DomainGuard.Utc(retiredAt);
        if (Status == PreflightChecklistDefinitionStatus.Retired)
        {
            return;
        }

        Status = PreflightChecklistDefinitionStatus.Retired;
        RetiredAt = retiredAt;
    }
}
