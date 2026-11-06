using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static class M01015TestData
{
    private const ulong PatFixture = 0x0006_0501_0007_0406;
    internal static X64PageTableEncodingPreconditions Context()
    {
        X64PageTableEntryStatus status = X64PatMappingToken.TryCreate(PatFixture, writeBackIndex: 0, uncachedIndex: 3, writeCombiningIndex: 4, out X64PatMappingToken token);
        if (status != X64PageTableEntryStatus.Success)
        {
            throw new InvalidOperationException("M01-015 PAT fixture invalid: " + status);
        }

        return X64PageTableEncodingPreconditions.CreateObserved(physicalAddressWidth: 48, nxeEnabled: true, writeProtectEnabled: true, fiveLevelPagingEnabled: false, oneGiBPagesSupported: true, token);
    }

    internal static PhysicalAddress Physical(ulong value)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateAlignedFromHardwareRegisterBoundary(value, X64PageMap.PageSize, out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("M01-015 physical fixture invalid: " + status);
        }

        return address;
    }

    internal static ulong EncodeTablePointer(X64PageTableLevel level, PhysicalAddress child, X64PageTableEncodingPreconditions context)
    {
        X64PageTableEntryResult result = X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(level, X64PageTableEntryKind.TablePointer, child, PageProtection.None, PageCachePolicy.WriteBack), context);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("M01-015 table pointer fixture invalid: " + result.Status);
        }

        return result.Entry.RawValue;
    }

    internal static X64PageTableImagePage Page(ulong physical, ulong entryStartIndex)
    {
        X64PageMapStatus status = X64PageTableImagePage.TryCreate(Physical(physical), entryStartIndex, out X64PageTableImagePage page);
        if (status != X64PageMapStatus.Success)
        {
            throw new InvalidOperationException("M01-015 page fixture invalid: " + status);
        }

        return page;
    }
}
