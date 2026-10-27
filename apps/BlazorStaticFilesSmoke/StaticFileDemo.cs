using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

/// <summary>A real browser WebAssembly component. Readiness requires JSON from
/// Kestrel and the variant nonce fetched separately from the physical webroot.</summary>
public sealed class StaticFileDemo : ComponentBase
{
    [Inject] public HttpClient Http { get; set; } = null!;
    private string message = "Loading CLROS…", nonce = "", state = "loading";
    private int count;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            using var status = JsonDocument.Parse(await Http.GetStringAsync("api/status"));
            using var items = JsonDocument.Parse(await Http.GetStringAsync("api/items"));
            nonce = (await Http.GetStringAsync("fixture-nonce.txt")).TrimEnd('\r', '\n');
            if (status.RootElement.GetProperty("profile").GetString() != "v1" ||
                !status.RootElement.GetProperty("ready").GetBoolean() || nonce.Length is 0 or > 128)
                throw new InvalidDataException("Unexpected fixture response.");
            message = status.RootElement.GetProperty("message").GetString()!;
            count = items.RootElement.GetProperty("items").GetArrayLength();
            state = "ready";
        }
        catch
        {
            state = "failed";
            message = "CLROS browser fixture failed to load its API or disk assets.";
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "main");
        builder.AddAttribute(1, "data-clros-state", state);
        builder.OpenElement(2, "h1"); builder.AddContent(3, "CLROS disk-hosted Blazor"); builder.CloseElement();
        builder.OpenElement(4, "p"); builder.AddAttribute(5, "id", "api-status"); builder.AddContent(6, message); builder.CloseElement();
        builder.OpenElement(7, "p"); builder.AddAttribute(8, "id", "api-items"); builder.AddContent(9, $"API items: {count}"); builder.CloseElement();
        builder.OpenElement(10, "p"); builder.AddAttribute(11, "id", "disk-nonce"); builder.AddContent(12, nonce); builder.CloseElement();
        builder.CloseElement();
    }
}
