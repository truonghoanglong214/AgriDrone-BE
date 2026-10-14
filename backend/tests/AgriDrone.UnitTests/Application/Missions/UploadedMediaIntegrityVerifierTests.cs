using System.Security.Cryptography;
using AgriDrone.Modules.Missions.Application.Features.Media.UploadMissionMedia;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class UploadedMediaIntegrityVerifierTests
{
    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    [Fact]
    public async Task CompletePngProducesChecksum()
    {
        using var stream = new MemoryStream(ValidPng);
        var actual = await UploadedMediaIntegrityVerifier.VerifyAsync(stream,
            "image/png", ValidPng.Length, CancellationToken.None);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(ValidPng)).ToLowerInvariant(), actual);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(40)]
    public async Task TruncatedPngIsRejected(int length)
    {
        using var stream = new MemoryStream(ValidPng[..length]);
        Assert.Null(await UploadedMediaIntegrityVerifier.VerifyAsync(stream,
            "image/png", length, CancellationToken.None));
    }

    [Fact]
    public async Task PngWithCorruptChunkOrTrailingBytesIsRejected()
    {
        var badCrc = (byte[])ValidPng.Clone();
        badCrc[45] ^= 0x01;
        using var corrupt = new MemoryStream(badCrc);
        Assert.Null(await UploadedMediaIntegrityVerifier.VerifyAsync(corrupt,
            "image/png", badCrc.Length, CancellationToken.None));

        using var trailing = new MemoryStream([.. ValidPng, 0x42]);
        Assert.Null(await UploadedMediaIntegrityVerifier.VerifyAsync(trailing,
            "image/png", ValidPng.Length + 1, CancellationToken.None));
    }

    [Fact]
    public async Task VideoWithoutMovieOrMediaBoxIsRejected()
    {
        byte[] bytes = [0, 0, 0, 16, 102, 116, 121, 112,
            105, 115, 111, 109, 0, 0, 0, 0];
        using var stream = new MemoryStream(bytes);
        Assert.Null(await UploadedMediaIntegrityVerifier.VerifyAsync(stream,
            "video/mp4", bytes.Length, CancellationToken.None));
    }

    [Fact]
    public async Task VideoWithCompleteTopLevelBoxesProducesChecksum()
    {
        byte[] bytes = [
            0, 0, 0, 16, 102, 116, 121, 112, 105, 115, 111, 109, 0, 0, 0, 0,
            0, 0, 0, 9, 109, 111, 111, 118, 1,
            0, 0, 0, 9, 109, 100, 97, 116, 2];
        using var stream = new MemoryStream(bytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            await UploadedMediaIntegrityVerifier.VerifyAsync(stream,
                "video/mp4", bytes.Length, CancellationToken.None));
    }

    [Fact]
    public async Task JpegWithOnlyMarkersAndNoImageScanIsRejected()
    {
        byte[] bytes = [0xff, 0xd8, 0xff, 0xc0, 0, 2,
            0xff, 0xda, 0, 2, 0xff, 0xd9];
        using var stream = new MemoryStream(bytes);
        Assert.Null(await UploadedMediaIntegrityVerifier.VerifyAsync(stream,
            "image/jpeg", bytes.Length, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteJpegProducesChecksum()
    {
        var bytes = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDi6KKK+ZP3E//Z");
        using var stream = new MemoryStream(bytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            await UploadedMediaIntegrityVerifier.VerifyAsync(stream,
                "image/jpeg", bytes.Length, CancellationToken.None));
    }

    [Fact]
    public async Task MultiPictureJpegProducesChecksum()
    {
        var baseJpeg = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDi6KKK+ZP3E//Z");
        byte[] bytes = [.. baseJpeg, .. baseJpeg];
        using var stream = new MemoryStream(bytes);
        var checksum = await UploadedMediaIntegrityVerifier.VerifyAsync(stream,
            "image/jpeg", bytes.Length, CancellationToken.None);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), checksum);
    }

    [Fact]
    public async Task RealDroneImageCanBeVerified()
    {
        var path = @"D:\capstone\dataset\baymap1\13_10_04\G0174984_1791879976229.JPG";
        if (!File.Exists(path)) return;
        using var stream = File.OpenRead(path);
        var checksum = await UploadedMediaIntegrityVerifier.VerifyAsync(stream,
            "image/jpeg", stream.Length, CancellationToken.None);
        Assert.NotNull(checksum);
        Assert.Equal(64, checksum.Length);
    }
}
