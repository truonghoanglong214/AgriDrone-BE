using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AgriDrone.Modules.Missions.Application.Features.Media.UploadMissionMedia;

// Checks the container structure while hashing the exact bytes received from storage.
internal static class UploadedMediaIntegrityVerifier
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = CreateCrcTable();

    public static async Task<string?> VerifyAsync(Stream source, string mimeType,
        long expectedLength, CancellationToken cancellationToken)
    {
        using var hashing = new HashingReadStream(source);
        using var input = new BufferedStream(hashing, 81920);
        var valid = mimeType switch
        {
            "image/png" => await VerifyPngAsync(input, cancellationToken),
            "image/jpeg" => VerifyJpeg(input, cancellationToken),
            "video/mp4" or "video/quicktime" =>
                await VerifyIsoMediaAsync(input, mimeType, expectedLength, cancellationToken),
            _ => false
        };
        if (!valid || input.ReadByte() != -1 || hashing.BytesRead != expectedLength)
            return null;
        return hashing.GetChecksum();
    }

    private static async Task<bool> VerifyPngAsync(Stream input, CancellationToken token)
    {
        var signature = new byte[8];
        if (!await ReadExactlyAsync(input, signature, token) ||
            !signature.AsSpan().SequenceEqual(PngSignature)) return false;

        var first = true;
        var hasImageData = false;
        var chunkHeader = new byte[8];
        var storedCrc = new byte[4];
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (await ReadExactlyAsync(input, chunkHeader, token))
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader);
                var type = System.Text.Encoding.ASCII.GetString(chunkHeader, 4, 4);
                if (length > 50L * 1024 * 1024) return false;
                if (first && (type != "IHDR" || length != 13))
                    return false;
                if (type == "IEND" && (length != 0 || !hasImageData))
                    return false;

                var crc = UpdateCrc(0xffffffffu, chunkHeader.AsSpan(4, 4));
                var remaining = (long)length;
                var firstData = true;
                while (remaining > 0)
                {
                    var count = (int)Math.Min(remaining, buffer.Length);
                    if (!await ReadExactlyAsync(input, buffer.AsMemory(0, count), token))
                        return false;
                    if (first && firstData && length == 13 &&
                        (BinaryPrimitives.ReadUInt32BigEndian(buffer) == 0 ||
                         BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(4)) == 0))
                        return false;
                    crc = UpdateCrc(crc, buffer.AsSpan(0, count));
                    remaining -= count;
                    firstData = false;
                }
                if (!await ReadExactlyAsync(input, storedCrc, token) ||
                    BinaryPrimitives.ReadUInt32BigEndian(storedCrc) != ~crc)
                    return false;
                if (type == "IDAT") hasImageData = true;
                if (type == "IEND") return true;
                first = false;
            }
            return false;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static bool VerifyJpeg(Stream input, CancellationToken token)
    {
        if (input.ReadByte() != 0xff || input.ReadByte() != 0xd8) return false;
        var hasFrame = false;
        var hasScan = false;
        var inScan = false;
        var scanDataBytes = 0;
        var inspected = 0;
        while (true)
        {
            if (++inspected % 65536 == 0) token.ThrowIfCancellationRequested();
            var value = input.ReadByte();
            if (value == -1) return false;
            if (inScan && value != 0xff)
            {
                scanDataBytes++;
                continue;
            }
            if (value != 0xff) return false;
            int marker;
            do { marker = input.ReadByte(); } while (marker == 0xff);
            if (marker == -1) return false;
            if (inScan && (marker == 0x00 || marker is >= 0xd0 and <= 0xd7))
                continue;
            inScan = false;
            if (marker == 0xd9) return hasFrame && hasScan && scanDataBytes > 0;
            if (marker == 0xd8 || marker == 0x00 || marker is >= 0xd0 and <= 0xd7)
                return false;
            if (marker == 0x01) continue;

            var high = input.ReadByte();
            var low = input.ReadByte();
            if (high == -1 || low == -1) return false;
            var length = (high << 8) | low;
            if (length < 2) return false;
            if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc))
            {
                if (length < 8) return false;
                var precision = input.ReadByte();
                var heightHigh = input.ReadByte();
                var heightLow = input.ReadByte();
                var widthHigh = input.ReadByte();
                var widthLow = input.ReadByte();
                var components = input.ReadByte();
                if (precision <= 0 || heightHigh < 0 || heightLow < 0 ||
                    widthHigh < 0 || widthLow < 0 || components <= 0 ||
                    (heightHigh | heightLow) == 0 || (widthHigh | widthLow) == 0 ||
                    length < 8 + 3 * components) return false;
                hasFrame = true;
                if (!Skip(input, length - 8, token)) return false;
                continue;
            }
            if (marker == 0xda)
            {
                if (!hasFrame || length < 6) return false;
                hasScan = true;
                inScan = true;
            }
            if (!Skip(input, length - 2, token)) return false;
        }
    }

    private static async Task<bool> VerifyIsoMediaAsync(Stream input, string mimeType,
        long expectedLength, CancellationToken token)
    {
        var header = new byte[16];
        long position = 0;
        var first = true;
        var hasMovie = false;
        var hasMedia = false;
        while (position < expectedLength)
        {
            if (expectedLength - position < 8 ||
                !await ReadExactlyAsync(input, header.AsMemory(0, 8), token)) return false;
            var boxSize = (long)BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            var headerSize = 8L;
            if (boxSize == 1)
            {
                if (!await ReadExactlyAsync(input, header.AsMemory(8, 8), token)) return false;
                var largeSize = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8));
                if (largeSize > long.MaxValue) return false;
                boxSize = (long)largeSize;
                headerSize = 16;
            }
            if (boxSize == 0) boxSize = expectedLength - position;
            if (boxSize < headerSize || boxSize > expectedLength - position) return false;
            if (first)
            {
                if (type != "ftyp" || boxSize < headerSize + 8 ||
                    !await ReadExactlyAsync(input, header.AsMemory(0, 4), token)) return false;
                var brand = System.Text.Encoding.ASCII.GetString(header, 0, 4);
                var supported = mimeType == "video/quicktime"
                    ? brand == "qt  "
                    : brand is "isom" or "iso2" or "mp41" or "mp42" or "avc1" or "M4V ";
                if (!supported) return false;
                if (!await SkipAsync(input, boxSize - headerSize - 4, token)) return false;
            }
            else
            {
                if (type == "moov" && boxSize > headerSize) hasMovie = true;
                if (type == "mdat" && boxSize > headerSize) hasMedia = true;
                if (!await SkipAsync(input, boxSize - headerSize, token)) return false;
            }
            first = false;
            position += boxSize;
        }
        return hasMovie && hasMedia;
    }

    private static async Task<bool> ReadExactlyAsync(Stream input, Memory<byte> buffer,
        CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await input.ReadAsync(buffer[offset..], token);
            if (count == 0) return false;
            offset += count;
        }
        return true;
    }

    private static bool Skip(Stream input, int count, CancellationToken token)
    {
        for (var i = 0; i < count; i++)
        {
            if (i % 65536 == 0) token.ThrowIfCancellationRequested();
            if (input.ReadByte() == -1) return false;
        }
        return true;
    }

    private static async Task<bool> SkipAsync(Stream input, long count, CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (count > 0)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0,
                    (int)Math.Min(count, buffer.Length)), token);
                if (read == 0) return false;
                count -= read;
            }
            return true;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data) crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var crc = i;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320u;
            table[i] = crc;
        }
        return table;
    }

    private sealed class HashingReadStream(Stream source) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public string GetChecksum() => Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = source.Read(buffer, offset, count);
            hash.AppendData(buffer.AsSpan(offset, read));
            BytesRead += read;
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            hash.AppendData(buffer.Span[..read]);
            BytesRead += read;
            return read;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) hash.Dispose();
            base.Dispose(disposing);
        }
    }
}
