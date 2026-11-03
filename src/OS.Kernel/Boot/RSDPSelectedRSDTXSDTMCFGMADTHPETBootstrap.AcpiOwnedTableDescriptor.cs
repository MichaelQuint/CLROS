using System.Runtime.InteropServices;
using OS.Std.Binary;

namespace OS.Boot;
// Offsets refer to the retained destination, never to relocated firmware pointers.
public readonly struct AcpiOwnedTableDescriptor
{
    internal AcpiOwnedTableDescriptor(ulong address, int offset, int length, ulong signature, int rootPointerIndex)
    {
        PhysicalAddress = address;
        Offset = offset;
        Length = length;
        Signature = signature;
        RootPointerIndex = rootPointerIndex;
    }

    public ulong PhysicalAddress
    {
        get;
    }
    public int Offset
    {
        get;
    }
    public int Length
    {
        get;
    }
    public ulong Signature
    {
        get;
    }
    public int RootPointerIndex
    {
        get;
    }
}
