using System.Text.Json.Serialization;

namespace Modpack_Installer.Core;

public sealed class MrpackIndex
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; }

    [JsonPropertyName("game")]
    public string? Game { get; set; }

    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("files")]
    public List<MrpackFile>? Files { get; set; }

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; set; }
}

public sealed class MrpackFile
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("hashes")]
    public Dictionary<string, string>? Hashes { get; set; }

    [JsonPropertyName("env")]
    public MrpackEnv? Env { get; set; }

    [JsonPropertyName("downloads")]
    public List<string>? Downloads { get; set; }

    [JsonPropertyName("fileSize")]
    public long? FileSize { get; set; }

    [JsonIgnore]
    public bool IsClientUnsupported =>
        string.Equals(Env?.Client, "unsupported", StringComparison.OrdinalIgnoreCase);
}

public sealed class MrpackEnv
{
    [JsonPropertyName("client")]
    public string? Client { get; set; }

    [JsonPropertyName("server")]
    public string? Server { get; set; }
}
