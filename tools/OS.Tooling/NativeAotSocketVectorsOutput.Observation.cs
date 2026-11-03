using System.Globalization;

namespace OS.Tooling;
public static partial class NativeAotSocketVectorsOutput
{
    public sealed record Observation(IReadOnlyList<Connection> Connections, bool Policy);
}
