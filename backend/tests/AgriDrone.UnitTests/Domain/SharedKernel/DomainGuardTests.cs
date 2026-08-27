using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.SharedKernel;

public sealed class DomainGuardTests
{
    [Fact]
    public void NotEmptyReturnsValidIdentifier()
    {
        var id = Guid.NewGuid();

        var result = DomainGuard.NotEmpty(id);

        Assert.Equal(id, result);
    }

    [Fact]
    public void NotEmptyRejectsEmptyIdentifierAndCapturesParameterName()
    {
        var id = Guid.Empty;

        var exception = Assert.Throws<ArgumentException>(
            () => DomainGuard.NotEmpty(id));

        Assert.Equal(nameof(id), exception.ParamName);
    }

    [Fact]
    public void UtcReturnsValidUtcTimestamp()
    {
        var timestamp =
            new DateTimeOffset(2026, 8, 27, 8, 0, 0, TimeSpan.Zero);

        var result = DomainGuard.Utc(timestamp);

        Assert.Equal(timestamp, result);
    }

    [Fact]
    public void UtcRejectsNonUtcTimestampAndCapturesParameterName()
    {
        var timestamp =
            new DateTimeOffset(2026, 8, 27, 15, 0, 0, TimeSpan.FromHours(7));

        var exception = Assert.Throws<ArgumentException>(
            () => DomainGuard.Utc(timestamp));

        Assert.Equal(nameof(timestamp), exception.ParamName);
    }

    [Fact]
    public void UtcRejectsDefaultTimestamp()
    {
        var timestamp = default(DateTimeOffset);

        Assert.Throws<ArgumentException>(
            () => DomainGuard.Utc(timestamp));
    }
}
