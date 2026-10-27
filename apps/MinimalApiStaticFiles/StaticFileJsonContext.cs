using System.Text.Json.Serialization;

/// <summary>Explicit AOT metadata for the selected static-file JSON endpoints.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StaticFileConfigurationResponse))]
[JsonSerializable(typeof(StaticFileStatusResponse))]
[JsonSerializable(typeof(StaticFileItemsResponse))]
public sealed partial class StaticFileJsonContext : JsonSerializerContext;
