namespace AgriDrone.IntegrationContracts.Media;

public static class ChecksumAlgorithms
{
    public const string Md5 = "MD5";

    public const string Sha256 = "SHA256";

    public static bool IsSupported(string? algorithm) =>
        algorithm is Md5 or Sha256;
}