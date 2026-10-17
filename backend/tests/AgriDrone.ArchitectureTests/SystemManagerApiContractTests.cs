using System.Text.Json;
using AgriDrone.Api.Contracts.SystemManagers;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class SystemManagerApiContractTests
{
    [Theory]
    [InlineData(SystemManagerAvailabilityValue.AVAILABLE, "AVAILABLE")]
    [InlineData(SystemManagerAvailabilityValue.UNAVAILABLE, "UNAVAILABLE")]
    public void AvailabilityRequestUsesStableStringToken(
        SystemManagerAvailabilityValue availability,
        string expectedToken)
    {
        var request = new UpdateSystemManagerAvailabilityRequest(
            availability,
            "Operational availability update",
            ExpectedVersion: 1);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));

        Assert.Equal(
            expectedToken,
            json.RootElement.GetProperty("Availability").GetString());
    }

    [Theory]
    [InlineData(FlightQualificationStatusValue.PENDING, "PENDING")]
    [InlineData(FlightQualificationStatusValue.QUALIFIED, "QUALIFIED")]
    [InlineData(FlightQualificationStatusValue.SUSPENDED, "SUSPENDED")]
    [InlineData(FlightQualificationStatusValue.REVOKED, "REVOKED")]
    public void QualificationRequestUsesStableStringToken(
        FlightQualificationStatusValue status,
        string expectedToken)
    {
        var request = new UpdateSystemManagerQualificationRequest(
            status,
            ExpiresAt: null,
            "Qualification status update",
            ExpectedVersion: 1);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));

        Assert.Equal(
            expectedToken,
            json.RootElement.GetProperty("Status").GetString());
    }
}
