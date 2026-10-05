using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModSyncLauncher.Core;

[JsonConverter(typeof(ModTypeJsonConverter))]
public enum ModType
{
    Skin,
    Vehicle,
    Weapon,
    Object,
    Map,
    Audio,
    Script,
    Texture,
    Plugin
}

public class ModTypeJsonConverter : JsonConverter<ModType>
{
    public override ModType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (string.IsNullOrWhiteSpace(str)) return ModType.Object;

        return str.ToLowerInvariant() switch
        {
            "skin" => ModType.Skin,
            "vehicle" => ModType.Vehicle,
            "weapon" => ModType.Weapon,
            "object" => ModType.Object,
            "map" => ModType.Map,
            "audio" => ModType.Audio,
            "script" => ModType.Script,
            "texture" => ModType.Texture,
            "plugin" => ModType.Plugin,
            _ => ModType.Object
        };
    }

    public override void Write(Utf8JsonWriter writer, ModType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
    }
}

public class ModFileItem
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("type")]
    public string FileType { get; set; } = string.Empty;
}

public class ModManifestItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public ModType Type { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("base_model_id")]
    public int? BaseModelId { get; set; }

    [JsonPropertyName("new_model_id")]
    public int? NewModelId { get; set; }

    [JsonPropertyName("is_addition")]
    public bool IsAddition { get; set; } = false;

    [JsonPropertyName("files")]
    public List<ModFileItem> Files { get; set; } = new();

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    // Backward-compatibility properties for flat manifests
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    public IEnumerable<ModFileItem> GetEffectiveFiles()
    {
        if (Files != null && Files.Count > 0)
        {
            return Files;
        }

        if (!string.IsNullOrEmpty(DownloadUrl) || !string.IsNullOrEmpty(Hash))
        {
            return new[]
            {
                new ModFileItem
                {
                    Path = !string.IsNullOrEmpty(RelativePath) ? RelativePath : System.IO.Path.GetFileName(DownloadUrl),
                    Sha256 = Hash,
                    SizeBytes = Size,
                    FileType = System.IO.Path.GetExtension(DownloadUrl).TrimStart('.')
                }
            };
        }

        return Array.Empty<ModFileItem>();
    }
}

public class ModManifest
{
    [JsonPropertyName("server_id")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("server_name")]
    public string ManifestServerName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("serverName")]
    public string ServerName { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : (!string.IsNullOrEmpty(ManifestServerName) ? ManifestServerName : ServerName);

    [JsonIgnore]
    public string EffectiveServerId
    {
        get
        {
            var raw = !string.IsNullOrWhiteSpace(ServerId) ? ServerId : (DisplayName ?? "openmp_server");
            var sb = new System.Text.StringBuilder();
            bool lastWasUnderscore = false;

            foreach (var ch in raw)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-')
                {
                    sb.Append(char.ToLowerInvariant(ch));
                    lastWasUnderscore = false;
                }
                else
                {
                    if (!lastWasUnderscore && sb.Length > 0)
                    {
                        sb.Append('_');
                        lastWasUnderscore = true;
                    }
                }
            }
            var result = sb.ToString().Trim('_', '-');
            return string.IsNullOrEmpty(result) ? "openmp_server" : result;
        }
    }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.3.8-A-R1";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "eLdarqO";

    [JsonPropertyName("credits")]
    public string Credits { get; set; } = "eLdarqO";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("required_launcher_version")]
    public string RequiredLauncherVersion { get; set; } = "0.3.8-A-R1";

    [JsonPropertyName("mods")]
    public List<ModManifestItem> Mods { get; set; } = new();
}

[JsonSerializable(typeof(ModManifest))]
[JsonSerializable(typeof(ModManifestItem))]
[JsonSerializable(typeof(ModFileItem))]
[JsonSerializable(typeof(ModType))]
public partial class ModManifestContext : JsonSerializerContext
{
}
