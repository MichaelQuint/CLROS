using System.Runtime.InteropServices;
using OS.Std.Binary;

namespace OS.Boot;
/// <summary>
/// Copies five validated snapshots into one caller-retained buffer. Inputs and
/// their metadata must remain stable throughout this call. Physical addresses
/// describe the complete supplied snapshot spans; this routine never reads them.
/// Destination and pointer scratch must not alias any supplied source or each
/// other, including unused capacity. Source physical spans must be disjoint;
/// duplicate source addresses and duplicate root pointers are rejected.
/// Scratch may change on failure, but the destination and published descriptors
/// do not. Embedded physical pointers are preserved, not made safe to follow.
/// </summary>

public static class RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap
{
    public const ulong RsdpSignature = 0x2052545020445352;
    public const uint MadtSignature = 0x43495041; // The MADT signature is APIC.
    public const uint HpetSignature = 0x54455048;
    public static AcpiBootstrapCopyResult Copy(ReadOnlySpan<byte> rsdp, ulong rsdpAddress, ReadOnlySpan<byte> root, ulong rootAddress, Span<ulong> pointerScratch, ReadOnlySpan<AcpiTableSource> sources, Span<byte> destination)
    {
        if (pointerScratch.Length > int.MaxValue / sizeof(ulong))
            return Fail(AcpiBootstrapCopyStatus.InvalidScratchLength);
        Span<byte> scratchBytes = MemoryMarshal.AsBytes(pointerScratch);
        if (destination.Overlaps(scratchBytes) || rsdp.Overlaps(destination) || rsdp.Overlaps(scratchBytes) || root.Overlaps(destination) || root.Overlaps(scratchBytes))
            return Fail(AcpiBootstrapCopyStatus.AliasedBuffers);
        // Complete alias admission precedes the first pointer-parser write.
        for (int i = 0; i < sources.Length; i++)
        {
            ReadOnlySpan<byte> bytes = sources[i].TableBytes.Span;
            if (bytes.Overlaps(destination) || bytes.Overlaps(scratchBytes))
                return Fail(AcpiBootstrapCopyStatus.AliasedBuffers);
        }

        if (!ValidRange(rsdpAddress, rsdp.Length) || !ValidRange(rootAddress, root.Length))
            return Fail(AcpiBootstrapCopyStatus.InvalidSourceRange);
        if (Overlap(rsdpAddress, rsdp.Length, rootAddress, root.Length))
            return Fail(AcpiBootstrapCopyStatus.OverlappingSourceRanges);
        for (int i = 0; i < sources.Length; i++)
        {
            ulong address = sources[i].PhysicalAddress;
            int length = sources[i].TableBytes.Length;
            if (!ValidRange(address, length))
                return Fail(AcpiBootstrapCopyStatus.InvalidSourceRange);
            if (Overlap(address, length, rsdpAddress, rsdp.Length) || Overlap(address, length, rootAddress, root.Length))
                return Fail(AcpiBootstrapCopyStatus.OverlappingSourceRanges);
            for (int j = 0; j < i; j++)
                if (Overlap(address, length, sources[j].PhysicalAddress, sources[j].TableBytes.Length))
                    return Fail(AcpiBootstrapCopyStatus.OverlappingSourceRanges);
        }

        AcpiRsdpValidationResult rsdpResult = AcpiRsdpValidator.Validate(rsdp);
        if (!rsdpResult.Succeeded)
            return Fail(AcpiBootstrapCopyStatus.InvalidRsdp);
        // Same selection as AcpiRootSdtSnapshotReader: use XSDT when nonzero;
        // RSDT fallback is permitted only when the validated XSDT address is zero.
        ulong selectedAddress = rsdpResult.RevisionKind == AcpiRsdpRevisionKind.Acpi20OrGreater ? EndianPrimitives.ReadUInt64LittleEndian(rsdp.Slice(AcpiRsdpValidator.XsdtAddressOffset, 8)) : 0;
        bool xsdt = selectedAddress != 0;
        if (!xsdt)
            selectedAddress = EndianPrimitives.ReadUInt32LittleEndian(rsdp.Slice(AcpiRsdpValidator.RsdtAddressOffset, 4));
        if (selectedAddress == 0 || selectedAddress != rootAddress)
            return Fail(AcpiBootstrapCopyStatus.RootAddressMismatch);
        AcpiSdtParseResult rootResult = AcpiSdtParser.TryParsePointerList(root, pointerScratch);
        if (!rootResult.Succeeded || rootResult.TableKind != (xsdt ? AcpiSdtTableKind.Xsdt : AcpiSdtTableKind.Rsdt))
            return Fail(AcpiBootstrapCopyStatus.InvalidRoot);
        ReadOnlySpan<ulong> pointers = pointerScratch.Slice(0, rootResult.PointerCount);
        for (int i = 0; i < pointers.Length; i++)
            for (int j = 0; j < i; j++)
                if (pointers[i] == pointers[j])
                    return Fail(AcpiBootstrapCopyStatus.DuplicateRootPointer);
        Span<AcpiTableSnapshot> selected = stackalloc AcpiTableSnapshot[3];
        for (int i = 0; i < 3; i++)
        {
            uint signature = i == 0 ? AcpiTableDirectory.McfgSignature : i == 1 ? MadtSignature : HpetSignature;
            AcpiTableDirectoryResult selection = AcpiTableDirectory.TrySelect(pointers, sources, signature, selected.Slice(i, 1));
            if (!selection.Succeeded)
            {
                AcpiBootstrapCopyStatus status = selection.Status != AcpiTableDirectoryStatus.SignatureNotFound ? AcpiBootstrapCopyStatus.InvalidChild : i == 0 ? AcpiBootstrapCopyStatus.MissingMcfg : i == 1 ? AcpiBootstrapCopyStatus.MissingMadt : AcpiBootstrapCopyStatus.MissingHpet;
                return new AcpiBootstrapCopyResult(status, directoryStatus: selection.Status);
            }
        }

        // Each declared length is bounded by an actual int-sized source span.
        // Sum in UInt64 before narrowing or constructing any destination slice.
        ulong total = (ulong)rsdpResult.Length + rootResult.DeclaredLength + selected[0].Length + selected[1].Length + selected[2].Length;
        if (total > (ulong)destination.Length)
            return new AcpiBootstrapCopyResult(AcpiBootstrapCopyStatus.DestinationTooSmall, total);
        int offset = 0;
        AcpiOwnedTableDescriptor ownedRsdp = CopyOne(rsdp, rsdpAddress, (int)rsdpResult.Length, RsdpSignature, -1, destination, ref offset);
        AcpiOwnedTableDescriptor ownedRoot = CopyOne(root, rootAddress, (int)rootResult.DeclaredLength, rootResult.Header.Signature, -1, destination, ref offset);
        AcpiOwnedTableDescriptor ownedMcfg = CopySelected(selected[0], sources, destination, ref offset);
        AcpiOwnedTableDescriptor ownedMadt = CopySelected(selected[1], sources, destination, ref offset);
        AcpiOwnedTableDescriptor ownedHpet = CopySelected(selected[2], sources, destination, ref offset);
        return new AcpiBootstrapCopyResult(AcpiBootstrapCopyStatus.Success, total, rsdp: ownedRsdp, root: ownedRoot, mcfg: ownedMcfg, madt: ownedMadt, hpet: ownedHpet);
    }

    private static AcpiOwnedTableDescriptor CopySelected(AcpiTableSnapshot selected, ReadOnlySpan<AcpiTableSource> sources, Span<byte> destination, ref int offset) => CopyOne(sources[selected.SourceIndex].TableBytes.Span, selected.PhysicalAddress, (int)selected.Length, selected.Signature, selected.PointerIndex, destination, ref offset);
    private static AcpiOwnedTableDescriptor CopyOne(ReadOnlySpan<byte> source, ulong address, int length, ulong signature, int pointerIndex, Span<byte> destination, ref int offset)
    {
        AcpiOwnedTableDescriptor descriptor = new(address, offset, length, signature, pointerIndex);
        source.Slice(0, length).CopyTo(destination.Slice(offset, length));
        offset += length;
        return descriptor;
    }

    private static bool ValidRange(ulong address, int length) => address != 0 && length > 0 && (ulong)length <= ulong.MaxValue - address;
    private static bool Overlap(ulong a, int aLength, ulong b, int bLength) => a < b + (ulong)bLength && b < a + (ulong)aLength;
    private static AcpiBootstrapCopyResult Fail(AcpiBootstrapCopyStatus status) => new(status);
}
