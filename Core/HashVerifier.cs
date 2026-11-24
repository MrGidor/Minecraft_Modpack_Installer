using System.Security.Cryptography;

namespace Modpack_Installer.Core;

public static class HashVerifier
{
    private static readonly string[] Supported = { "sha1", "sha512" };

    public static bool HasSupportedHash(IReadOnlyDictionary<string, string>? hashes) =>
        hashes != null && hashes.Keys.Any(k => Supported.Contains(k, StringComparer.OrdinalIgnoreCase));

    public static async Task VerifyAsync(string path, IReadOnlyDictionary<string, string> expected, CancellationToken ct = default)
    {
        foreach (var (algorithm, expectedHex) in expected)
        {
            string name = algorithm.ToLowerInvariant();
            if (!Supported.Contains(name))
                continue;

            await using var stream = File.OpenRead(path);
            byte[] hash = name == "sha1"
                ? await SHA1.HashDataAsync(stream, ct).ConfigureAwait(false)
                : await SHA512.HashDataAsync(stream, ct).ConfigureAwait(false);

            string actualHex = Convert.ToHexString(hash);
            if (!string.Equals(actualHex, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new HashMismatchException(path, name, expectedHex, actualHex.ToLowerInvariant());
        }
    }
}
