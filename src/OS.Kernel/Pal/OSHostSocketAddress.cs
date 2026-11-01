using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketAddress
{
    public readonly ushort Family;
    public readonly ushort PortNetworkOrder;
    public readonly uint FlowInfo;
    public readonly ulong ScopeId;
    public readonly ulong AddressHigh;
    public readonly ulong AddressLow;
    public OSHostSocketAddress(ushort family, ushort portNetworkOrder, uint flowInfo, ulong scopeId, ulong addressHigh, ulong addressLow)
    {
        Family = family;
        PortNetworkOrder = portNetworkOrder;
        FlowInfo = flowInfo;
        ScopeId = scopeId;
        AddressHigh = addressHigh;
        AddressLow = addressLow;
    }
}
