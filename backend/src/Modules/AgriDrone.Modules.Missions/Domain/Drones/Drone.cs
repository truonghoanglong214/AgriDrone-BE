using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Drones;

public sealed class Drone : AggregateRoot
{
    // Dành cho EF Core.
    private Drone()
    {
    }

    // Constructor đầy đủ dùng trong Domain.
    private Drone(
        Guid id,
        Guid tenantId,
        string code,
        string name,
        string? model,
        string? manufacturer,
        JsonElement specifications,
        string? serialNumber,
        string? registrationNumber,
        DateOnly? registrationDate,
        DateOnly? registrationExpiryDate,
        decimal? weightKg,
        string? notes,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Name = name;
        Model = model;
        Manufacturer = manufacturer;
        Specifications = specifications;
        SerialNumber = serialNumber;
        RegistrationNumber = registrationNumber;
        RegistrationDate = registrationDate;
        RegistrationExpiryDate = registrationExpiryDate;
        WeightKg = weightKg;
        Status = DroneStatus.Available;
        LastMaintenanceAt = null;
        NextMaintenanceAt = null;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        DeletedAt = null;
    }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Model { get; private set; }

    public string? Manufacturer { get; private set; }

    public JsonElement Specifications { get; private set; }

    public string? SerialNumber { get; private set; }

    public string? RegistrationNumber { get; private set; }

    public DateOnly? RegistrationDate { get; private set; }

    public DateOnly? RegistrationExpiryDate { get; private set; }

    public decimal? WeightKg { get; private set; }

    public DroneStatus Status { get; private set; }

    public DateTimeOffset? LastMaintenanceAt { get; private set; }

    public DateTimeOffset? NextMaintenanceAt { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Drone Create(
        Guid tenantId,
        string code,
        string name,
        string? model,
        string? manufacturer,
        JsonElement? specifications,
        string? serialNumber,
        string? registrationNumber,
        DateOnly? registrationDate,
        DateOnly? registrationExpiryDate,
        decimal? weightKg,
        string? notes,
        DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant ID is required.",
                nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Drone code is required.",
                nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Drone name is required.",
                nameof(name));
        }

        if (weightKg.HasValue && weightKg.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weightKg),
                "Drone weight must be greater than zero.");
        }

        if (registrationDate.HasValue &&
            registrationExpiryDate.HasValue &&
            registrationExpiryDate.Value < registrationDate.Value)
        {
            throw new ArgumentException(
                "Registration expiry date cannot be earlier than registration date.",
                nameof(registrationExpiryDate));
        }

        EnsureTimestampIsProvided(
            createdAt,
            nameof(createdAt));

        return new Drone(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            code: code.Trim().ToUpperInvariant(),
            name: name.Trim(),
            model: NormalizeOptional(model),
            manufacturer: NormalizeOptional(manufacturer),
            specifications: CreateSpecificationsSnapshot(specifications),
            serialNumber: NormalizeIdentifier(serialNumber),
            registrationNumber: NormalizeIdentifier(registrationNumber),
            registrationDate: registrationDate,
            registrationExpiryDate: registrationExpiryDate,
            weightKg: weightKg,
            notes: NormalizeOptional(notes),
            createdAt: createdAt);
    }

    public void SendToMaintenance(DateTimeOffset sentAt)
    {
        EnsureTimestampIsProvided(
            sentAt,
            nameof(sentAt));

        if (Status == DroneStatus.Maintenance)
        {
            return;
        }

        if (Status != DroneStatus.Available)
        {
            throw new InvalidOperationException(
                "Only an available drone can be sent to maintenance.");
        }

        Status = DroneStatus.Maintenance;
        UpdatedAt = sentAt;
    }

    public void CompleteMaintenance(
        DateTimeOffset completedAt,
        DateTimeOffset? nextMaintenanceAt)
    {
        EnsureTimestampIsProvided(
            completedAt,
            nameof(completedAt));

        if (Status != DroneStatus.Maintenance)
        {
            throw new InvalidOperationException(
                "Only a drone under maintenance can complete maintenance.");
        }

        if (nextMaintenanceAt.HasValue &&
            nextMaintenanceAt.Value <= completedAt)
        {
            throw new ArgumentException(
                "Next maintenance time must be later than completion time.",
                nameof(nextMaintenanceAt));
        }

        LastMaintenanceAt = completedAt;
        NextMaintenanceAt = nextMaintenanceAt;
        Status = DroneStatus.Available;
        UpdatedAt = completedAt;
    }

    public void Retire(DateTimeOffset retiredAt)
    {
        EnsureTimestampIsProvided(
            retiredAt,
            nameof(retiredAt));

        if (Status == DroneStatus.Retired)
        {
            return;
        }

        if (Status != DroneStatus.Available &&
            Status != DroneStatus.Maintenance)
        {
            throw new InvalidOperationException(
                "Only an available or maintenance drone can be retired.");
        }

        Status = DroneStatus.Retired;
        UpdatedAt = retiredAt;
    }

    private static JsonElement CreateSpecificationsSnapshot(
        JsonElement? specifications)
    {
        if (!specifications.HasValue ||
            specifications.Value.ValueKind is
                JsonValueKind.Null or
                JsonValueKind.Undefined)
        {
            using var emptyDocument = JsonDocument.Parse("{}");

            return emptyDocument.RootElement.Clone();
        }

        return specifications.Value.Clone();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeIdentifier(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static void EnsureTimestampIsProvided(
        DateTimeOffset timestamp,
        string parameterName)
    {
        if (timestamp == default)
        {
            throw new ArgumentException(
                "Timestamp is required.",
                parameterName);
        }
    }
}