using System.Text.Json.Nodes;

namespace OS.Tooling;
/// <summary>Injectable transport for deterministic offline policy tests.</summary>

public delegate Task<JsonNode?> BacklogApi(string method, string path, JsonObject? body, CancellationToken cancellation);
