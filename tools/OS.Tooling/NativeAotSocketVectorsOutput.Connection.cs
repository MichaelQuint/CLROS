using System.Globalization;

namespace OS.Tooling;

public static partial class NativeAotSocketVectorsOutput
{
    public readonly record struct Connection(uint Index, uint ReceiveCalls, uint ReceivePartial, uint ReceiveBlocked, uint SendCalls, uint SendPartial, uint SendBlocked);
}
