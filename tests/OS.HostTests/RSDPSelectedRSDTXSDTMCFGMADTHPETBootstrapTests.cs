using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Std.Binary;
using Bootstrap = OS.Boot.RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap;

internal static partial class RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrapTests
{
    private static void Check(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }

    [HostTest("m03.002", HostLane.Fast, 0)]
    private static void TestFiveCopies()
    {
        Fixture f = new();
        AcpiBootstrapCopyResult r = f.Copy();
        Check(r.Succeeded && r.Status == AcpiBootstrapCopyStatus.Success && r.CopiedBytes == f.Total && r.RequiredBytes == (ulong)f.Total, "aggregate");
        Descriptor(r.Rsdp, f.RsdpAddress, 0, f.Rsdp, Bootstrap.RsdpSignature, -1, f.Destination);
        Descriptor(r.Root, f.RootAddress, f.Rsdp.Length, f.Root, AcpiSdtParser.XsdtSignature, -1, f.Destination);
        Descriptor(r.Mcfg, 0x3000, f.Rsdp.Length + f.Root.Length, f.Mcfg, AcpiTableDirectory.McfgSignature, 0, f.Destination);
        Descriptor(r.Madt, 0x4000, r.Mcfg.Offset + r.Mcfg.Length, f.Madt, Bootstrap.MadtSignature, 1, f.Destination);
        Descriptor(r.Hpet, 0x5000, r.Madt.Offset + r.Madt.Length, f.Hpet, Bootstrap.HpetSignature, 2, f.Destination);
        Check(f.Destination.AsSpan(f.Total).IndexOfAnyExcept((byte)0xAD) < 0, "tail");
        Check(f.Scratch.AsSpan(3).IndexOfAnyExcept(0xF0F0F0F0F0F0F0F0UL) < 0, "scratch tail");
    }

    private static void Descriptor(AcpiOwnedTableDescriptor d, ulong address, int offset, byte[] bytes, ulong signature, int index, byte[] destination)
    {
        Check(d.PhysicalAddress == address && d.Offset == offset && d.Length == bytes.Length && d.Signature == signature && d.RootPointerIndex == index, "descriptor provenance");
        Check(destination.AsSpan(offset, bytes.Length).SequenceEqual(bytes), "full owned bytes");
    }

    [HostTest("m03.002 ACPI1 and XSDT fallback", HostLane.Fast, 1)]
    private static void TestRootSelection()
    {
        foreach (Fixture f in new[]
        {
            new Fixture(false),
            new Fixture(true, true)
        }

        )
        {
            AcpiBootstrapCopyResult r = f.Copy();
            Check(r.Succeeded && r.Root.Signature == AcpiSdtParser.RsdtSignature, "RSDT selection");
            Check(f.Destination.AsSpan(r.Rsdp.Offset, r.Rsdp.Length).SequenceEqual(f.Rsdp), "RSDP full bytes");
        }

        Fixture preferred = new();
        preferred.Root = RootTable(false, 0x3000, 0x4000, 0x5000);
        Reject(preferred, AcpiBootstrapCopyStatus.InvalidRoot);
        Fixture noFallbackOnFailure = new();
        noFallbackOnFailure.Root[9]++;
        Reject(noFallbackOnFailure, AcpiBootstrapCopyStatus.InvalidRoot);
    }

    [HostTest("m03.002 first matching root order", HostLane.Fast, 2)]
    private static void TestFirstMatch()
    {
        Fixture f = new();
        byte[] first = Sdt(AcpiTableDirectory.McfgSignature, 47, 0xE1);
        f.Root = RootTable(true, 0x6000, 0x5000, 0x4000, 0x3000);
        f.Sources = [new(0x3000, f.Mcfg), new(0x4000, f.Madt), new(0x5000, f.Hpet), new(0x6000, first)];
        AcpiBootstrapCopyResult r = f.Copy();
        Check(r.Succeeded && r.Mcfg.PhysicalAddress == 0x6000 && r.Mcfg.RootPointerIndex == 0 && r.Madt.RootPointerIndex == 2 && r.Hpet.RootPointerIndex == 1, "root order, not source order");
        Check(f.Destination.AsSpan(r.Mcfg.Offset, r.Mcfg.Length).SequenceEqual(first), "first payload");
        // Invalid earlier supplied tables must not be skipped during any selection.
        first[9]++;
        Reject(f, AcpiBootstrapCopyStatus.InvalidChild);
        // Existing directory selection stops after its first match; later unknown
        // payloads are not newly subjected to device/envelope validation here.
        Fixture later = new();
        later.Root = RootTable(true, 0x3000, 0x4000, 0x5000, 0x6000);
        later.Sources = [.. later.Sources, new(0x6000, new byte[36])];
        Check(later.Copy().Succeeded, "later unselected envelope policy");
    }

    [HostTest("m03.002 independent retained bytes", HostLane.Fast, 3)]
    private static void TestRetention()
    {
        Fixture f = new();
        AcpiBootstrapCopyResult r = f.Copy();
        Check(r.Succeeded, "copy");
        byte[] expected = (byte[])f.Destination.Clone();
        foreach (byte[] source in new[]
        {
            f.Rsdp,
            f.Root,
            f.Mcfg,
            f.Madt,
            f.Hpet
        }

        )
            source.AsSpan().Fill(0xEE);
        f.Scratch.AsSpan().Clear();
        f.Sources.AsSpan().Clear();
        Check(f.Destination.AsSpan().SequenceEqual(expected), "five copies survive source mutation");
        Check(EndianPrimitives.ReadUInt64LittleEndian(f.Destination.AsSpan(r.Rsdp.Offset + 24, 8)) == 0x2000 && EndianPrimitives.ReadUInt64LittleEndian(f.Destination.AsSpan(r.Root.Offset + 36, 8)) == 0x3000, "embedded original pointers retained");
    }

    [HostTest("m03.002 rejects malformed envelopes", HostLane.Fast, 4)]
    private static void TestEnvelopes()
    {
        foreach (int index in new[]
        {
            0,
            8,
            32
        }

        )
        {
            Fixture f = new();
            f.Rsdp[index]++;
            Reject(f, AcpiBootstrapCopyStatus.InvalidRsdp);
        }

        foreach (uint length in new uint[]
        {
            0,
            20,
            35,
            41,
            uint.MaxValue
        }

        )
        {
            Fixture f = new();
            U32(f.Rsdp, 20, length);
            RsdpChecksum(f.Rsdp);
            Reject(f, AcpiBootstrapCopyStatus.InvalidRsdp);
        }

        foreach (int source in new[]
        {
            0,
            1,
            2,
            3
        }

        )
        {
            foreach (int alteration in new[]
            {
                0,
                1,
                2,
                3,
                4
            }

            )
            {
                Fixture f = new();
                byte[] table = source == 0 ? f.Root : source == 1 ? f.Mcfg : source == 2 ? f.Madt : f.Hpet;
                if (alteration == 0)
                    table[9]++;
                else if (alteration == 1)
                {
                    table[0] = (byte)'!';
                    Checksum(table, 9);
                }
                else
                {
                    U32(table, 4, alteration == 2 ? 35u : alteration == 3 ? (uint)table.Length + 1 : uint.MaxValue);
                    Checksum(table, 9);
                }

                AcpiBootstrapCopyStatus expected = source == 0 ? AcpiBootstrapCopyStatus.InvalidRoot : alteration != 1 ? AcpiBootstrapCopyStatus.InvalidChild : source == 1 ? AcpiBootstrapCopyStatus.MissingMcfg : source == 2 ? AcpiBootstrapCopyStatus.MissingMadt : AcpiBootstrapCopyStatus.MissingHpet;
                Reject(f, expected);
            }
        }

        Fixture shortRoot = new();
        shortRoot.Root = shortRoot.Root[..35];
        Reject(shortRoot, AcpiBootstrapCopyStatus.InvalidRoot);
        Fixture shortChild = new();
        shortChild.Sources[1] = new(0x4000, new byte[35]);
        Reject(shortChild, AcpiBootstrapCopyStatus.InvalidChild);
        Fixture oddRoot = new();
        oddRoot.Root = Sdt(AcpiSdtParser.XsdtSignature, 61, 1);
        Reject(oddRoot, AcpiBootstrapCopyStatus.InvalidRoot);
    }

    [HostTest("m03.002 rejects address and duplicate metadata", HostLane.Fast, 5)]
    private static void TestAddresses()
    {
        foreach (ulong address in new[]
        {
            0UL,
            ulong.MaxValue,
            ulong.MaxValue - 38
        }

        )
        {
            Fixture f = new();
            f.RsdpAddress = address;
            Reject(f, AcpiBootstrapCopyStatus.InvalidSourceRange);
            f = new();
            f.RootAddress = address;
            Reject(f, AcpiBootstrapCopyStatus.InvalidSourceRange);
            f = new();
            f.Sources[0] = new(address, f.Mcfg);
            Reject(f, AcpiBootstrapCopyStatus.InvalidSourceRange);
        }

        Fixture edge = new();
        edge.RsdpAddress = ulong.MaxValue - (ulong)edge.Rsdp.Length;
        Check(edge.Copy().Succeeded, "nonwrapping upper boundary");
        Fixture mismatch = new();
        mismatch.RootAddress = 0x8000;
        Reject(mismatch, AcpiBootstrapCopyStatus.RootAddressMismatch);
        Fixture zeroRoot = new();
        U64(zeroRoot.Rsdp, 24, 0);
        U32(zeroRoot.Rsdp, 16, 0);
        RsdpChecksum(zeroRoot.Rsdp);
        Reject(zeroRoot, AcpiBootstrapCopyStatus.RootAddressMismatch);
        Fixture duplicateSource = new();
        duplicateSource.Sources = [.. duplicateSource.Sources, new(0x3000, duplicateSource.Mcfg)];
        Reject(duplicateSource, AcpiBootstrapCopyStatus.OverlappingSourceRanges);
        foreach (ulong address in new[]
        {
            0x1000UL,
            0x2001UL,
            0x3020UL
        }

        )
        {
            Fixture f = new();
            f.Sources[1] = new(address, f.Madt);
            Reject(f, AcpiBootstrapCopyStatus.OverlappingSourceRanges);
        }

        Fixture sameRoot = new();
        sameRoot.RootAddress = sameRoot.RsdpAddress;
        Reject(sameRoot, AcpiBootstrapCopyStatus.OverlappingSourceRanges);
        Fixture duplicatePointer = new();
        duplicatePointer.Root = RootTable(true, 0x3000, 0x4000, 0x5000, 0x3000);
        Reject(duplicatePointer, AcpiBootstrapCopyStatus.DuplicateRootPointer);
        Fixture zeroPointer = new();
        zeroPointer.Root = RootTable(true, 0x3000, 0, 0x5000);
        Reject(zeroPointer, AcpiBootstrapCopyStatus.InvalidRoot);
        Fixture unmatched = new();
        unmatched.Sources[0] = new(0x6000, unmatched.Mcfg);
        Reject(unmatched, AcpiBootstrapCopyStatus.InvalidChild);
    }

    [HostTest("m03.002 requires all three selected children", HostLane.Fast, 6)]
    private static void TestMissing()
    {
        for (int i = 0; i < 3; i++)
        {
            Fixture f = new();
            byte[] child = i == 0 ? f.Mcfg : i == 1 ? f.Madt : f.Hpet;
            U32(child, 0, 0x54534554);
            Checksum(child, 9);
            Reject(f, i == 0 ? AcpiBootstrapCopyStatus.MissingMcfg : i == 1 ? AcpiBootstrapCopyStatus.MissingMadt : AcpiBootstrapCopyStatus.MissingHpet);
        }

        Fixture absent = new();
        absent.Sources = [];
        Reject(absent, AcpiBootstrapCopyStatus.InvalidChild);
    }

    [HostTest("m03.002 exact capacities and untouched tails", HostLane.Fast, 7)]
    private static void TestCapacity()
    {
        Fixture f = new();
        int required = f.Total;
        f.Destination = new byte[required];
        Check(f.Copy().Succeeded, "exact destination");
        f.Destination = new byte[required - 1];
        AcpiBootstrapCopyResult shortResult = Reject(f, AcpiBootstrapCopyStatus.DestinationTooSmall);
        Check(shortResult.RequiredBytes == (ulong)required, "required sum");
        f.Destination = [];
        Reject(f, AcpiBootstrapCopyStatus.DestinationTooSmall);
        f = new();
        f.Scratch = new ulong[2];
        Reject(f, AcpiBootstrapCopyStatus.InvalidRoot);
        f = new();
        f.Scratch = new ulong[3];
        Check(f.Copy().Succeeded, "exact pointer scratch");
        f = new();
        byte[] larger = new byte[f.Mcfg.Length + 19];
        f.Mcfg.CopyTo(larger, 0);
        larger.AsSpan(f.Mcfg.Length).Fill(0xFC);
        f.Sources[0] = new(0x3000, larger);
        AcpiBootstrapCopyResult r = f.Copy();
        Check(r.Succeeded && r.Mcfg.Length == f.Mcfg.Length && r.CopiedBytes == f.Total, "copy declared length only");
        Check(f.Destination.AsSpan(f.Total).IndexOfAnyExcept((byte)0xAD) < 0, "all tail preserved");
    }

    [HostTest("m03.002 rejects aliases before scratch writes", HostLane.Fast, 8)]
    private static void TestAliases()
    {
        for (int i = 0; i < 5; i++)
        {
            Fixture f = new();
            byte[] bytes = i == 0 ? f.Rsdp : i == 1 ? f.Root : i == 2 ? f.Mcfg : i == 3 ? f.Madt : f.Hpet;
            byte[] before = (byte[])bytes.Clone();
            ulong[] scratchBefore = (ulong[])f.Scratch.Clone();
            AcpiBootstrapCopyResult r = Bootstrap.Copy(f.Rsdp, f.RsdpAddress, f.Root, f.RootAddress, f.Scratch, f.Sources, bytes.AsSpan(1));
            Check(r.Status == AcpiBootstrapCopyStatus.AliasedBuffers && bytes.AsSpan().SequenceEqual(before) && f.Scratch.AsSpan().SequenceEqual(scratchBefore), "destination aliases full source");
            r = Bootstrap.Copy(f.Rsdp, f.RsdpAddress, f.Root, f.RootAddress, MemoryMarshal.Cast<byte, ulong>(bytes.AsSpan()), f.Sources, f.Destination);
            Check(r.Status == AcpiBootstrapCopyStatus.AliasedBuffers && bytes.AsSpan().SequenceEqual(before), "scratch aliases source");
            Check(f.Destination.AsSpan().IndexOfAnyExcept((byte)0xAD) < 0, "alias failure destination atomic");
        }

        Fixture shared = new();
        byte[] backing = new byte[2048];
        backing.AsSpan().Fill(0xD7);
        AcpiBootstrapCopyResult overlap = Bootstrap.Copy(shared.Rsdp, shared.RsdpAddress, shared.Root, shared.RootAddress, MemoryMarshal.Cast<byte, ulong>(backing.AsSpan(1024, 64)), shared.Sources, backing.AsSpan(0, 1500));
        Check(overlap.Status == AcpiBootstrapCopyStatus.AliasedBuffers && backing.AsSpan().IndexOfAnyExcept((byte)0xD7) < 0, "unused destination capacity aliases scratch");
        shared.Mcfg.CopyTo(backing, 1400);
        byte[] original = (byte[])backing.Clone();
        shared.Sources[0] = new(0x3000, backing.AsMemory(1400, shared.Mcfg.Length));
        overlap = Bootstrap.Copy(shared.Rsdp, shared.RsdpAddress, shared.Root, shared.RootAddress, shared.Scratch, shared.Sources, backing);
        Check(overlap.Status == AcpiBootstrapCopyStatus.AliasedBuffers && backing.AsSpan().SequenceEqual(original), "unused destination capacity aliases source");
    }

    [HostTest("m03.002 ignores forged success metadata", HostLane.Fast, 9)]
    private static void TestDefaultAndForged()
    {
        Check(!default(AcpiBootstrapCopyResult).Succeeded && default(AcpiBootstrapCopyResult).CopiedBytes == 0, "default failure");
        Check(!RuntimeHelpers.IsReferenceOrContainsReferences<AcpiBootstrapCopyResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<AcpiOwnedTableDescriptor>(), "POD output");
        Check(default(AcpiTableDirectoryResult).Succeeded, "existing zero-default metadata is not our authority");
        AcpiRsdpValidationResult forged = AcpiRsdpValidationResult.Success(AcpiRsdpRevisionKind.Acpi20OrGreater, 2, 40);
        Check(forged.Succeeded, "forged existing metadata fixture");
        Fixture f = new();
        f.Rsdp = new byte[40];
        Reject(f, AcpiBootstrapCopyStatus.InvalidRsdp);
        f = new();
        f.Sources[0] = default;
        Reject(f, AcpiBootstrapCopyStatus.InvalidSourceRange);
    }

    [HostTest("m03.002 warmed zero allocation", HostLane.Fast, 10)]
    private static void TestNoAllocation()
    {
        Fixture f = new();
        for (int i = 0; i < 128; i++)
            Check(f.Copy().Succeeded, "warm copy");
        long before = GC.GetAllocatedBytesForCurrentThread();
        int copied = 0;
        for (int i = 0; i < 512; i++)
            copied += f.Copy().CopiedBytes;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0 && copied == 512 * f.Total, "warmed copy allocation");
        f.Hpet[9]++;
        before = GC.GetAllocatedBytesForCurrentThread();
        int rejected = 0;
        for (int i = 0; i < 512; i++)
            if (!f.Copy().Succeeded)
                rejected++;
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0 && rejected == 512, "rejection allocation");
    }

    private static AcpiBootstrapCopyResult Reject(Fixture f, AcpiBootstrapCopyStatus status)
    {
        byte[] before = (byte[])f.Destination.Clone();
        AcpiBootstrapCopyResult r = f.Copy();
        Check(!r.Succeeded && r.Status == status, "rejection expected=" + status + " actual=" + r.Status);
        Check(r.CopiedBytes == 0 && r.Rsdp.Length == 0 && r.Root.Length == 0 && r.Mcfg.Length == 0 && r.Madt.Length == 0 && r.Hpet.Length == 0, "no partial publication");
        Check(f.Destination.AsSpan().SequenceEqual(before), "whole destination unchanged");
        return r;
    }

    private static byte[] RootTable(bool xsdt, params ulong[] pointers)
    {
        int width = xsdt ? 8 : 4;
        byte[] table = Sdt(xsdt ? AcpiSdtParser.XsdtSignature : AcpiSdtParser.RsdtSignature, 36 + width * pointers.Length, 1);
        for (int i = 0; i < pointers.Length; i++)
            if (xsdt)
                U64(table, 36 + i * width, pointers[i]);
            else
                U32(table, 36 + i * width, (uint)pointers[i]);
        Checksum(table, 9);
        return table;
    }

    private static byte[] Sdt(uint signature, int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(seed + i * 13);
        U32(bytes, 0, signature);
        U32(bytes, 4, (uint)length);
        bytes[8] = 1;
        Checksum(bytes, 9);
        return bytes;
    }

    private static byte[] RsdpTable(bool extended, uint rsdt, ulong xsdt)
    {
        byte[] bytes = new byte[extended ? 40 : 20];
        "RSD PTR "u8.CopyTo(bytes);
        "CLROS!"u8.CopyTo(bytes.AsSpan(9));
        bytes[15] = extended ? (byte)2 : (byte)0;
        U32(bytes, 16, rsdt);
        if (extended)
        {
            U32(bytes, 20, (uint)bytes.Length);
            U64(bytes, 24, xsdt);
            bytes[39] = 0x7B;
        }

        RsdpChecksum(bytes);
        return bytes;
    }

    private static void RsdpChecksum(byte[] bytes)
    {
        Checksum(bytes.AsSpan(0, 20), 8);
        if (bytes.Length >= 36)
            Checksum(bytes, 32);
    }

    private static void Checksum(Span<byte> bytes, int index)
    {
        bytes[index] = 0;
        int sum = 0;
        foreach (byte value in bytes)
            sum += value;
        bytes[index] = unchecked((byte)-sum);
    }

    private static void U32(byte[] bytes, int offset, uint value) => EndianPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void U64(byte[] bytes, int offset, ulong value) => EndianPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);
}
