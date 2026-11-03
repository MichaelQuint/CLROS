using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private sealed record RaceSampleLink(int sample, int attempt, int related);
}
