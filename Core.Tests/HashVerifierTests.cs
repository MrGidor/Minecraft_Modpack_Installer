namespace Modpack_Installer.Core.Tests;

public class HashVerifierTests
{
    private const string Sha1 = "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d";

    private const string Sha512 =
        "9b71d224bd62f3785d96d46ad3ea3d73319bfbc2890caadae2dff72519673ca72323c3d99ba5c11d7c7acc6e14b8c5da0c4663475c2e5c3adef46f73bcdec043";

    private static string WriteHello(TempDir temp)
    {
        string path = temp.Combine("hello.bin");
        File.WriteAllText(path, "hello");
        return path;
    }

    [Fact]
    public async Task Verify_AcceptsMatchingHashes()
    {
        using var temp = new TempDir();
        string path = WriteHello(temp);

        await HashVerifier.VerifyAsync(path, new Dictionary<string, string> { ["sha1"] = Sha1, ["sha512"] = Sha512 });
    }

    [Fact]
    public async Task Verify_IsCaseInsensitive()
    {
        using var temp = new TempDir();
        string path = WriteHello(temp);

        await HashVerifier.VerifyAsync(path, new Dictionary<string, string> { ["SHA1"] = Sha1.ToUpperInvariant() });
    }

    [Fact]
    public async Task Verify_RejectsWrongSha1()
    {
        using var temp = new TempDir();
        string path = WriteHello(temp);

        await Assert.ThrowsAsync<HashMismatchException>(() =>
            HashVerifier.VerifyAsync(path, new Dictionary<string, string> { ["sha1"] = new string('0', 40) }));
    }

    [Fact]
    public async Task Verify_RejectsWrongSha512EvenWhenSha1Matches()
    {
        using var temp = new TempDir();
        string path = WriteHello(temp);

        await Assert.ThrowsAsync<HashMismatchException>(() =>
            HashVerifier.VerifyAsync(path, new Dictionary<string, string> { ["sha1"] = Sha1, ["sha512"] = new string('0', 128) }));
    }

    [Fact]
    public async Task Verify_IgnoresUnsupportedAlgorithms()
    {
        using var temp = new TempDir();
        string path = WriteHello(temp);

        await HashVerifier.VerifyAsync(path, new Dictionary<string, string> { ["md5"] = "nope" });
    }

    [Fact]
    public void HasSupportedHash_RequiresSha1OrSha512()
    {
        Assert.True(HashVerifier.HasSupportedHash(new Dictionary<string, string> { ["sha512"] = Sha512 }));
        Assert.False(HashVerifier.HasSupportedHash(new Dictionary<string, string> { ["md5"] = "x" }));
        Assert.False(HashVerifier.HasSupportedHash(null));
    }
}
