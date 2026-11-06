using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostIpv4Endpoint
{
    public readonly ushort Family;
    public readonly ushort PortNetworkOrder;
    public readonly byte Address0;
    public readonly byte Address1;
    public readonly byte Address2;
    public readonly byte Address3;
    public OSHostIpv4Endpoint(ushort family, ushort portNetworkOrder, byte address0, byte address1, byte address2, byte address3)
    {
        Family = family;
        PortNetworkOrder = portNetworkOrder;
        Address0 = address0;
        Address1 = address1;
        Address2 = address2;
        Address3 = address3;
    }

    public ushort PortHostOrder => OSHostSocketNumbers.NetworkToHost16(PortNetworkOrder);
    public bool IsValid => Validate(this) == OSHostStatus.Ok;

    public OSHostSocketAddress AsSocketAddress()
    {
        return new OSHostSocketAddress(Family, PortNetworkOrder, flowInfo: 0, scopeId: 0, addressHigh: 0, addressLow: ToAddressNetworkOrder());
    }

    public uint ToAddressNetworkOrder()
    {
        return ((uint)Address0 << 24) | ((uint)Address1 << 16) | ((uint)Address2 << 8) | Address3;
    }

    public static OSHostStatus TryCreate(byte address0, byte address1, byte address2, byte address3, ushort portHostOrder, out OSHostIpv4Endpoint endpoint)
    {
        if (portHostOrder == 0)
        {
            endpoint = default;
            return OSHostStatus.InvalidArgument;
        }

        endpoint = new OSHostIpv4Endpoint(OSHostSocketConstants.AddressFamilyIpv4, OSHostSocketNumbers.HostToNetwork16(portHostOrder), address0, address1, address2, address3);
        return OSHostStatus.Ok;
    }

    public static OSHostStatus Validate(OSHostIpv4Endpoint endpoint)
    {
        if (endpoint.Family != OSHostSocketConstants.AddressFamilyIpv4)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (endpoint.PortNetworkOrder == 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        return OSHostStatus.Ok;
    }
}
