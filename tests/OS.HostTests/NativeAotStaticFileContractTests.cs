using System.Text.Json;

internal static class NativeAotStaticFileContractTests
{
    [HostTest("static file profile requires explicit supported selection")]
    internal static void Selection()
    {
        HostCheck.True(!StaticFileApplication.SelectProfile(null));
        HostCheck.True(!StaticFileApplication.SelectProfile(""));
        HostCheck.True(StaticFileApplication.SelectProfile("v1"));
        foreach (string invalid in new[] { "V1", "v2", " v1", "../wwwroot" })
        {
            bool rejected = false;
            try
            {
                StaticFileApplication.SelectProfile(invalid);
            }
            catch (ArgumentException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("static file generated JSON preserves Unicode null and empty objects")]
    internal static void JsonContract()
    {
        using var status = JsonDocument.Parse(JsonSerializer.Serialize(
            new StaticFileStatusResponse("v1", true, "CLROS ready — café"), StaticFileJsonContext.Default.StaticFileStatusResponse));
        HostCheck.Equal(JsonValueKind.Object, status.RootElement.ValueKind);
        HostCheck.True(status.RootElement.GetProperty("ready").GetBoolean());
        HostCheck.Equal("CLROS ready — café", status.RootElement.GetProperty("message").GetString()!);
        using var items = JsonDocument.Parse(JsonSerializer.Serialize(new StaticFileItemsResponse([new(2, "FAT32", null)]),
            StaticFileJsonContext.Default.StaticFileItemsResponse));
        HostCheck.Equal(JsonValueKind.Null, items.RootElement.GetProperty("items")[0].GetProperty("detail").ValueKind);
        using var empty = JsonDocument.Parse(JsonSerializer.Serialize(new StaticFileItemsResponse([]), StaticFileJsonContext.Default.StaticFileItemsResponse));
        HostCheck.Equal(JsonValueKind.Object, empty.RootElement.ValueKind);
        HostCheck.Equal(0, empty.RootElement.GetProperty("items").GetArrayLength());
    }
}
