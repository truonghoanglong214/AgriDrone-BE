using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Features.Media.StartMultipartUpload;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class MultipartUploadPolicyTests
{
    [Fact]
    public void ResumeAcceptsEveryExpectedPartInOrder()
    {
        var size = MultipartUploadPolicy.PartSizeBytes * 2 + 37;
        var parts = new[]
        {
            new MultipartUploadedPart(1, MultipartUploadPolicy.PartSizeBytes, "a"),
            new MultipartUploadedPart(2, MultipartUploadPolicy.PartSizeBytes, "b"),
            new MultipartUploadedPart(3, 37, "c")
        };

        Assert.Equal(3, MultipartUploadPolicy.PartCount(size));
        Assert.True(MultipartUploadPolicy.HasExpectedParts(size, parts));
    }

    [Fact]
    public void DuplicateOrMissingPartCannotComplete()
    {
        var size = MultipartUploadPolicy.PartSizeBytes * 2;
        var parts = new[]
        {
            new MultipartUploadedPart(1, MultipartUploadPolicy.PartSizeBytes, "a"),
            new MultipartUploadedPart(1, MultipartUploadPolicy.PartSizeBytes, "duplicate")
        };

        Assert.False(MultipartUploadPolicy.HasExpectedParts(size, parts));
    }

    [Fact]
    public void IncorrectPartSizeCannotComplete()
    {
        var size = MultipartUploadPolicy.PartSizeBytes + 5;
        var parts = new[]
        {
            new MultipartUploadedPart(1, MultipartUploadPolicy.PartSizeBytes - 1, "a"),
            new MultipartUploadedPart(2, 6, "b")
        };

        Assert.False(MultipartUploadPolicy.HasExpectedParts(size, parts));
    }
}
