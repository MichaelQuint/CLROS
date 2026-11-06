using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private sealed record RaceHttpSample(string kind, int status, string body);
}
