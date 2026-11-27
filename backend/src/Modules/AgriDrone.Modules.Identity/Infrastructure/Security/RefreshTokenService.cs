using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;

namespace AgriDrone.Modules.Identity.Infrastructure.Security;

internal sealed class RefreshTokenService : IRefreshTokenService
{
    public const int TokenByteLength = 32;

    public GeneratedRefreshToken Generate()
    {
        Span<byte> rawBytes = stackalloc byte[TokenByteLength];
        RandomNumberGenerator.Fill(rawBytes);

        var rawToken = Base64Url.EncodeToString(rawBytes);
        var tokenHash = HashUtf8Bytes(rawToken);
        return new GeneratedRefreshToken(rawToken, tokenHash);
    }

    public bool TryHash(string? rawToken, out string tokenHash)
    {
        tokenHash = string.Empty;
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        var trimmed = rawToken.Trim();
        if (trimmed.Contains('=') || trimmed.Contains('+') || trimmed.Contains('/'))
        {
            return false;
        }

        Span<byte> decoded = stackalloc byte[TokenByteLength];
        if (Base64Url.DecodeFromChars(trimmed, decoded, out var charsConsumed, out var bytesWritten) != OperationStatus.Done ||
            charsConsumed != trimmed.Length ||
            bytesWritten != TokenByteLength)
        {
            return false;
        }

        tokenHash = HashUtf8Bytes(trimmed);
        return true;
    }

    private static string HashUtf8Bytes(string normalizedRawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(normalizedRawToken);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
