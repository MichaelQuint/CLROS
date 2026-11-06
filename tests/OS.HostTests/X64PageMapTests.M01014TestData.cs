using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;

internal static class M01014TestData
{
    internal const ulong PatFixture = 0x0006_0501_0007_0406;
    internal static readonly PageProtection ReadWrite = (PageProtection)((byte)PageProtection.Read | (byte)PageProtection.Write);
    internal static readonly PageProtection ReadExecute = (PageProtection)((byte)PageProtection.Read | (byte)PageProtection.Execute);
    internal static X64PageTableEncodingPreconditions Context()
    {
        X64PageTableEntryStatus status = X64PatMappingToken.TryCreate(PatFixture, writeBackIndex: 0, uncachedIndex: 3, writeCombiningIndex: 4, out X64PatMappingToken token);
        if (status != X64PageTableEntryStatus.Success)
            throw new InvalidOperationException("PAT fixture invalid.");
        return X64PageTableEncodingPreconditions.CreateObserved(48, nxeEnabled: true, writeProtectEnabled: true, fiveLevelPagingEnabled: false, oneGiBPagesSupported: true, token);
    }

    internal static PhysicalAddress Physical(ulong value)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateAlignedFromHardwareRegisterBoundary(value, X64PageMap.PageSize, out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
            throw new InvalidOperationException("Physical fixture invalid: " + status);
        return address;
    }

    internal static PhysicalAddress PhysicalUnaligned(ulong value)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateFromHardwareRegisterBoundary(value, out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
            throw new InvalidOperationException("Physical fixture invalid: " + status);
        return address;
    }

    internal static VirtualAddress Virtual(ulong value)
    {
        AddressValueStatus status = VirtualAddress.TryCreateAlignedFromHardwareRegisterBoundary(value, X64PageMap.PageSize, out VirtualAddress address);
        if (status != AddressValueStatus.Success)
            throw new InvalidOperationException("Virtual fixture invalid: " + status);
        return address;
    }

    internal static VirtualAddress VirtualUnaligned(ulong value)
    {
        AddressValueStatus status = VirtualAddress.TryCreateFromHardwareRegisterBoundary(value, out VirtualAddress address);
        if (status != AddressValueStatus.Success)
            throw new InvalidOperationException("Virtual fixture invalid: " + status);
        return address;
    }

    internal static ulong Raw(PhysicalAddress address)
    {
        if (address.TryGetRawValueForHardwareRegisterBoundary(out ulong value) != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Physical raw conversion failed.");
        }

        return value;
    }

    internal static ulong Raw(VirtualAddress address)
    {
        if (address.TryGetRawValueForHardwareRegisterBoundary(out ulong value) != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Virtual raw conversion failed.");
        }

        return value;
    }

    internal static ulong EncodeTablePointer(X64PageTableLevel level, PhysicalAddress child, X64PageTableEncodingPreconditions context)
    {
        X64PageTableEntryResult result = X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(level, X64PageTableEntryKind.TablePointer, child, PageProtection.None, PageCachePolicy.WriteBack), context);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Table pointer fixture invalid: " + result.Status);
        }

        return result.Entry.RawValue;
    }
}
