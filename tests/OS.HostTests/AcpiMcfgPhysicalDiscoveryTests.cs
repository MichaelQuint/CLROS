using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class AcpiMcfgPhysicalDiscoveryTests
{
    private const ulong RootAddress = 0x0000000000200000;
    private const ulong HpetAddress = 0x00000000FED00000;
    private const ulong McfgAddress = 0x00000000E0000000;
    private static readonly uint HpetSignature = AcpiTableDirectory.CreateSignature((byte)'H', (byte)'P', (byte)'E', (byte)'T');
    [HostTest("acpi.mcfg.discovery reads xsdt child mcfg from physical memory", HostLane.Fast, 0)]
    private static bool TestReadsXsdtChildMcfgFromPhysicalMemory()
    {
        byte[] xsdt = CreateXsdt(HpetAddress, McfgAddress);
        byte[] hpet = CreateSdt(HpetSignature, payloadLength: 8);
        byte[] mcfg = CreateMcfg(new McfgEntrySpec(0x00000000E0000000, 0, 0, 0x3F));
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(HpetAddress, hpet), new PhysicalMemoryEntry(McfgAddress, mcfg));
        ulong[] pointerScratch = new ulong[2];
        byte[] headerScratch = new byte[AcpiSdtParser.HeaderLength];
        byte[] childTable = new byte[128];
        PciEcamSegment[] segments = new PciEcamSegment[1];
        AcpiMcfgPhysicalDiscoveryResult result = AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot(RootSnapshot(AcpiSdtTableKind.Xsdt, xsdt), xsdt, physicalMemory, pointerScratch, headerScratch, childTable, segments);
        return HostCheck.Result(result.Succeeded && result.Status == AcpiMcfgPhysicalDiscoveryStatus.Success && result.PointerListStatus == AcpiSdtParseStatus.Success && result.McfgParseStatus == AcpiMcfgParseStatus.Success && result.PointerCount == 2 && result.PointerIndex == 1 && result.PhysicalAddress == McfgAddress && result.DeclaredLength == mcfg.Length && result.SegmentCount == 1 && result.RequiredSegmentCapacity == 1 && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.Success && physicalMemory.ReadCount == 3 && physicalMemory.RequestedAddresses[0] == HpetAddress && physicalMemory.RequestedAddresses[1] == McfgAddress && physicalMemory.RequestedAddresses[2] == McfgAddress && segments[0].BasePhysicalAddress == 0x00000000E0000000 && segments[0].SegmentGroup == 0 && segments[0].StartBus == 0 && segments[0].EndBus == 0x3F);
    }

    [HostTest("acpi.mcfg.discovery reads rsdt child mcfg from physical memory", HostLane.Fast, 1)]
    private static bool TestReadsRsdtChildMcfgFromPhysicalMemory()
    {
        byte[] rsdt = CreateRsdt((uint)McfgAddress);
        byte[] mcfg = CreateMcfg(new McfgEntrySpec(0x00000000E0000000, 0, 0, 0x1F), new McfgEntrySpec(0x00000000E2000000, 1, 0x20, 0x3F));
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(McfgAddress, mcfg));
        ulong[] pointerScratch = new ulong[1];
        byte[] headerScratch = new byte[AcpiSdtParser.HeaderLength];
        byte[] childTable = new byte[128];
        PciEcamSegment[] segments = new PciEcamSegment[2];
        AcpiMcfgPhysicalDiscoveryResult result = AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot(RootSnapshot(AcpiSdtTableKind.Rsdt, rsdt), rsdt, physicalMemory, pointerScratch, headerScratch, childTable, segments);
        return HostCheck.Result(result.Succeeded && result.PointerCount == 1 && result.PointerIndex == 0 && result.PhysicalAddress == McfgAddress && result.SegmentCount == 2 && segments[0].SegmentGroup == 0 && segments[0].EndBus == 0x1F && segments[1].SegmentGroup == 1 && segments[1].StartBus == 0x20 && segments[1].EndBus == 0x3F);
    }

    [HostTest("acpi.mcfg.discovery reports physical mcfg missing", HostLane.Fast, 2)]
    private static bool TestReportsPhysicalMcfgMissing()
    {
        byte[] xsdt = CreateXsdt(HpetAddress);
        byte[] hpet = CreateSdt(HpetSignature, payloadLength: 8);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(HpetAddress, hpet));
        ulong[] pointerScratch = new ulong[1];
        byte[] headerScratch = new byte[AcpiSdtParser.HeaderLength];
        byte[] childTable = new byte[64];
        PciEcamSegment[] segments = new PciEcamSegment[1];
        AcpiMcfgPhysicalDiscoveryResult result = AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot(RootSnapshot(AcpiSdtTableKind.Xsdt, xsdt), xsdt, physicalMemory, pointerScratch, headerScratch, childTable, segments);
        return HostCheck.Result(!result.Succeeded && result.Status == AcpiMcfgPhysicalDiscoveryStatus.SignatureNotFound && result.PointerCount == 1 && result.PointerIndex == -1);
    }

    [HostTest("acpi.mcfg.discovery reports child header read failure", HostLane.Fast, 3)]
    private static bool TestReportsChildHeaderReadFailure()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] mcfg = CreateMcfg(new McfgEntrySpec(0x00000000E0000000, 0, 0, 0));
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.FailOnCall(failOnCall: 1, PlatformPhysicalMemoryStatus.SourceFailed, new PhysicalMemoryEntry(McfgAddress, mcfg));
        ulong[] pointerScratch = new ulong[1];
        byte[] headerScratch = new byte[AcpiSdtParser.HeaderLength];
        byte[] childTable = new byte[64];
        PciEcamSegment[] segments = new PciEcamSegment[1];
        AcpiMcfgPhysicalDiscoveryResult result = AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot(RootSnapshot(AcpiSdtTableKind.Xsdt, xsdt), xsdt, physicalMemory, pointerScratch, headerScratch, childTable, segments);
        return HostCheck.Result(!result.Succeeded && result.Status == AcpiMcfgPhysicalDiscoveryStatus.ChildHeaderReadFailed && result.PointerIndex == 0 && result.PhysicalAddress == McfgAddress && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceFailed && result.RequiredBytes == AcpiSdtParser.HeaderLength);
    }

    [HostTest("acpi.mcfg.discovery reports child full read failure", HostLane.Fast, 4)]
    private static bool TestReportsChildFullReadFailure()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] mcfg = CreateMcfg(new McfgEntrySpec(0x00000000E0000000, 0, 0, 0));
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.FailOnCall(failOnCall: 2, PlatformPhysicalMemoryStatus.SourceUnavailable, new PhysicalMemoryEntry(McfgAddress, mcfg));
        ulong[] pointerScratch = new ulong[1];
        byte[] headerScratch = new byte[AcpiSdtParser.HeaderLength];
        byte[] childTable = new byte[64];
        PciEcamSegment[] segments = new PciEcamSegment[1];
        AcpiMcfgPhysicalDiscoveryResult result = AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot(RootSnapshot(AcpiSdtTableKind.Xsdt, xsdt), xsdt, physicalMemory, pointerScratch, headerScratch, childTable, segments);
        return HostCheck.Result(!result.Succeeded && result.Status == AcpiMcfgPhysicalDiscoveryStatus.ChildFullReadFailed && result.PointerIndex == 0 && result.PhysicalAddress == McfgAddress && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceUnavailable && result.DeclaredLength == mcfg.Length);
    }

    [HostTest("acpi.mcfg.discovery rejects invalid physical mcfg checksum", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidPhysicalMcfgChecksum()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] mcfg = CreateMcfg(new McfgEntrySpec(0x00000000E0000000, 0, 0, 0));
        mcfg[AcpiMcfgParser.AllocationEntryOffset]++;
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(McfgAddress, mcfg));
        ulong[] pointerScratch = new ulong[1];
        byte[] headerScratch = new byte[AcpiSdtParser.HeaderLength];
        byte[] childTable = new byte[64];
        PciEcamSegment[] segments = new PciEcamSegment[1];
        AcpiMcfgPhysicalDiscoveryResult result = AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot(RootSnapshot(AcpiSdtTableKind.Xsdt, xsdt), xsdt, physicalMemory, pointerScratch, headerScratch, childTable, segments);
        return HostCheck.Result(!result.Succeeded && result.Status == AcpiMcfgPhysicalDiscoveryStatus.ChildTableChecksumInvalid && result.McfgParseStatus == AcpiMcfgParseStatus.Success && result.PointerIndex == 0 && result.DeclaredLength == mcfg.Length);
    }

    [HostTest("acpi.mcfg.discovery reports physical mcfg parse failure", HostLane.Fast, 6)]
    private static bool TestReportsPhysicalMcfgParseFailure()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] tooShortMcfg = CreateMcfgHeaderOnly();
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(McfgAddress, tooShortMcfg));
        ulong[] pointerScratch = new ulong[1];
        byte[] headerScratch = new byte[AcpiSdtParser.HeaderLength];
        byte[] childTable = new byte[64];
        PciEcamSegment[] segments = new PciEcamSegment[1];
        AcpiMcfgPhysicalDiscoveryResult result = AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot(RootSnapshot(AcpiSdtTableKind.Xsdt, xsdt), xsdt, physicalMemory, pointerScratch, headerScratch, childTable, segments);
        return HostCheck.Result(!result.Succeeded && result.Status == AcpiMcfgPhysicalDiscoveryStatus.McfgParseFailed && result.McfgParseStatus == AcpiMcfgParseStatus.DeclaredLengthTooSmall && result.RequiredBytes == AcpiMcfgParser.MinimumLength && result.PointerIndex == 0);
    }

    [HostTest("acpi.mcfg.discovery reports physical segment destination exhaustion", HostLane.Fast, 7)]
    private static bool TestReportsPhysicalSegmentDestinationExhaustion()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] mcfg = CreateMcfg(new McfgEntrySpec(0x00000000E0000000, 0, 0, 0), new McfgEntrySpec(0x00000000E1000000, 1, 1, 2));
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(McfgAddress, mcfg));
        ulong[] pointerScratch = new ulong[1];
        byte[] headerScratch = new byte[AcpiSdtParser.HeaderLength];
        byte[] childTable = new byte[128];
        PciEcamSegment[] segments = new PciEcamSegment[1];
        AcpiMcfgPhysicalDiscoveryResult result = AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot(RootSnapshot(AcpiSdtTableKind.Xsdt, xsdt), xsdt, physicalMemory, pointerScratch, headerScratch, childTable, segments);
        return HostCheck.Result(!result.Succeeded && result.Status == AcpiMcfgPhysicalDiscoveryStatus.McfgParseFailed && result.McfgParseStatus == AcpiMcfgParseStatus.DestinationTooSmall && result.RequiredSegmentCapacity == 2 && result.SegmentCount == 0);
    }

    [HostTest("acpi.mcfg.discovery physical result contains no managed references", HostLane.Fast, 8)]
    private static bool TestPhysicalResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<AcpiMcfgPhysicalDiscoveryResult>());
    }

    private static AcpiRootSdtSnapshotResult RootSnapshot(AcpiSdtTableKind tableKind, byte[] table)
    {
        int pointerSize = tableKind == AcpiSdtTableKind.Xsdt ? AcpiSdtParser.XsdtPointerSize : AcpiSdtParser.RsdtPointerSize;
        return AcpiRootSdtSnapshotResult.Success(tableKind, RootAddress, table.Length, (uint)table.Length, (table.Length - AcpiSdtParser.HeaderLength) / pointerSize, usedRsdtFallback: false);
    }

    private static byte[] CreateRsdt(params uint[] pointers)
    {
        byte[] table = new byte[AcpiSdtParser.HeaderLength + (pointers.Length * AcpiSdtParser.RsdtPointerSize)];
        WriteSdtHeader(table, AcpiSdtParser.RsdtSignature);
        for (int index = 0; index < pointers.Length; index++)
        {
            EndianPrimitives.WriteUInt32LittleEndian(table.AsSpan(AcpiSdtParser.HeaderLength + (index * AcpiSdtParser.RsdtPointerSize), sizeof(uint)), pointers[index]);
        }

        SetChecksum(table);
        return table;
    }

    private static byte[] CreateXsdt(params ulong[] pointers)
    {
        byte[] table = new byte[AcpiSdtParser.HeaderLength + (pointers.Length * AcpiSdtParser.XsdtPointerSize)];
        WriteSdtHeader(table, AcpiSdtParser.XsdtSignature);
        for (int index = 0; index < pointers.Length; index++)
        {
            EndianPrimitives.WriteUInt64LittleEndian(table.AsSpan(AcpiSdtParser.HeaderLength + (index * AcpiSdtParser.XsdtPointerSize), sizeof(ulong)), pointers[index]);
        }

        SetChecksum(table);
        return table;
    }

    private static byte[] CreateSdt(uint signature, int payloadLength)
    {
        byte[] table = new byte[AcpiSdtParser.HeaderLength + payloadLength];
        WriteSdtHeader(table, signature);
        for (int index = 0; index < payloadLength; index++)
        {
            table[AcpiSdtParser.HeaderLength + index] = (byte)(index + 1);
        }

        SetChecksum(table);
        return table;
    }

    private static byte[] CreateMcfgHeaderOnly()
    {
        byte[] mcfg = new byte[AcpiMcfgParser.AllocationEntryOffset];
        WriteSdtHeader(mcfg, AcpiTableDirectory.McfgSignature);
        SetChecksum(mcfg);
        return mcfg;
    }

    private static byte[] CreateMcfg(params McfgEntrySpec[] entries)
    {
        byte[] mcfg = new byte[AcpiMcfgParser.AllocationEntryOffset + (entries.Length * AcpiMcfgParser.AllocationEntryLength)];
        WriteSdtHeader(mcfg, AcpiTableDirectory.McfgSignature);
        for (int index = 0; index < entries.Length; index++)
        {
            WriteEntry(mcfg.AsSpan(AcpiMcfgParser.AllocationEntryOffset + (index * AcpiMcfgParser.AllocationEntryLength), AcpiMcfgParser.AllocationEntryLength), entries[index]);
        }

        SetChecksum(mcfg);
        return mcfg;
    }

    private static void WriteEntry(Span<byte> destination, McfgEntrySpec entry)
    {
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(AcpiMcfgParser.AllocationBaseAddressOffset, sizeof(ulong)), entry.BasePhysicalAddress);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(AcpiMcfgParser.AllocationSegmentGroupOffset, sizeof(ushort)), entry.SegmentGroup);
        destination[AcpiMcfgParser.AllocationStartBusOffset] = entry.StartBus;
        destination[AcpiMcfgParser.AllocationEndBusOffset] = entry.EndBus;
    }

    private static void WriteSdtHeader(byte[] table, uint signature)
    {
        EndianPrimitives.WriteUInt32LittleEndian(table.AsSpan(AcpiSdtParser.SignatureOffset, sizeof(uint)), signature);
        EndianPrimitives.WriteUInt32LittleEndian(table.AsSpan(AcpiSdtParser.LengthOffset, sizeof(uint)), (uint)table.Length);
        table[AcpiSdtParser.RevisionOffset] = 1;
        WriteAscii(table, AcpiSdtParser.OemIdOffset, "OSTEST");
        WriteAscii(table, AcpiSdtParser.OemTableIdOffset, "PHYSMCFG");
        EndianPrimitives.WriteUInt32LittleEndian(table.AsSpan(AcpiSdtParser.OemRevisionOffset, sizeof(uint)), 1);
        WriteAscii(table, AcpiSdtParser.CreatorIdOffset, "OS  ");
        EndianPrimitives.WriteUInt32LittleEndian(table.AsSpan(AcpiSdtParser.CreatorRevisionOffset, sizeof(uint)), 1);
    }

    private static void WriteAscii(byte[] destination, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            destination[offset + index] = (byte)value[index];
        }
    }

    private static void SetChecksum(byte[] table)
    {
        table[AcpiSdtParser.ChecksumOffset] = 0;
        uint sum = 0;
        for (int index = 0; index < table.Length; index++)
        {
            sum += table[index];
        }

        table[AcpiSdtParser.ChecksumOffset] = unchecked((byte)(0 - sum));
    }
}
