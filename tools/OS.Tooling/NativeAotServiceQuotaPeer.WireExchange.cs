using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    /// <summary>One logical HTTP attempt, including unselected polls. ConnectionIds
    /// name streams on which its actual writes were attempted; automatic retries
    /// may use multiple streams. Incomplete/error outcomes never imply HTTP EOF.</summary>

    public sealed record WireExchange(int Id, string Role, string Method, string Path, long StartedTicks, long EndedTicks, string Outcome, int Status, string BodyBase64, IReadOnlyList<int> ConnectionIds);
}
