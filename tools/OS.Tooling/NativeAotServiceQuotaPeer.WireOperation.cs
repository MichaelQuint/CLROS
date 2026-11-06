using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    /// <summary>One actual Stream call. Offset counts only earlier successful
    /// bytes in its direction. Failed/canceled calls report transferred=-1;
    /// write data is attempted bytes and may have been partly transmitted.
    /// Their direction becomes unknown and cannot accept another operation.
    /// A requested0 read is zero, distinct from EOF of a nonempty read.</summary>

    public sealed record WireOperation(int Id, int ConnectionId, string Direction, long Offset, int Requested, int Transferred, long StartedTicks, long EndedTicks, string Outcome, string DataBase64, int ExchangeId);
}
