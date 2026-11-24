namespace Modpack_Installer.Core;

public class InvalidModpackException : Exception
{
    public InvalidModpackException(string message) : base(message) { }

    public InvalidModpackException(string message, Exception inner) : base(message, inner) { }
}

public sealed class UnsafePathException : InvalidModpackException
{
    public string OffendingPath { get; }

    public UnsafePathException(string offendingPath, string reason)
        : base($"Unsafe path '{offendingPath}': {reason}")
    {
        OffendingPath = offendingPath;
    }
}

public sealed class HashMismatchException : Exception
{
    public HashMismatchException(string file, string algorithm, string expected, string actual)
        : base($"{algorithm} mismatch for '{Path.GetFileName(file)}': expected {expected}, got {actual}")
    {
    }
}
