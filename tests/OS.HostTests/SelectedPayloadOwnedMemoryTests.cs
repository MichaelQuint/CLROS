using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using OS.Manifest;
using OS.Storage;

internal static unsafe class SelectedPayloadOwnedMemoryTests
{
    private const int PathOffset = 64;
    private const int PayloadOffset = 75;
    private const int PayloadLength = 40;
    // Independent zlib/IEEE CRC32 values for the exact byte fixture below.
    private const uint PackageCrc32 = 0x7C9BDCCB;
    private const uint PayloadCrc32 = 0x3414BC50;

    [HostTest("storage.payload-owned copies complete v0 payload", HostLane.Fast, 0)]
    private static bool CopiesCompletePayload()
    {
        byte[] package = CreatePackage();
        byte[] original = (byte[])package.Clone();
        byte[] destination = FilledDestination(PayloadLength + 6);
        SelectedPayloadOwnedMemoryResult result = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), PackageCrc32, destination.AsSpan(2, PayloadLength + 2));

        return HostCheck.Result(result.Succeeded
            && result.Status == SelectedPayloadOwnedMemoryStatus.Success
            && result.PackageStatus == LivePackageStatus.Success
            && result.BytesWritten == PayloadLength
            && result.EntryOffset == 36
            && result.PayloadCrc32 == PayloadCrc32
            && destination.AsSpan(2, PayloadLength).SequenceEqual(package.AsSpan(PayloadOffset))
            && package.AsSpan().SequenceEqual(original)
            && AllBytesEqual(destination.AsSpan(0, 2), 0xEE)
            && AllBytesEqual(destination.AsSpan(PayloadLength + 2), 0xEE));
    }

    [HostTest("storage.payload-owned revalidates package checksum", HostLane.Fast, 1)]
    private static bool RevalidatesPackageChecksum()
    {
        byte[] package = CreatePackage();
        byte[] destination = FilledDestination(PayloadLength);
        LivePackageResult earlierReceipt = APPPKGDisk.Validate(package, SelectedPath(), PackageCrc32);
        package[^1] ^= 1;
        SelectedPayloadOwnedMemoryResult result = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), PackageCrc32, destination);

        return HostCheck.Result(earlierReceipt.Succeeded
            && Failed(result, SelectedPayloadOwnedMemoryStatus.PackageValidationFailed,
                LivePackageStatus.ChecksumMismatch)
            && AllBytesEqual(destination, 0xEE));
    }

    [HostTest("storage.payload-owned rejects invalid selected range", HostLane.Fast, 2)]
    private static bool RejectsInvalidSelectedRange()
    {
        byte[] package = CreatePackage();
        byte[] destination = FilledDestination(PayloadLength);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(48), ulong.MaxValue);
        SelectedPayloadOwnedMemoryResult result = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), ReferenceCrc32(package), destination);

        return HostCheck.Result(Failed(result, SelectedPayloadOwnedMemoryStatus.PackageValidationFailed,
                LivePackageStatus.InvalidEntry)
            && AllBytesEqual(destination, 0xEE));
    }

    [HostTest("storage.payload-owned rejects missing selected file", HostLane.Fast, 3)]
    private static bool RejectsMissingSelectedFile()
    {
        byte[] destination = FilledDestination(PayloadLength);
        SelectedPayloadOwnedMemoryResult result = SelectedPayloadOwnedMemory.Stage(
            CreatePackage(), "bin/missing.bin"u8.ToArray(), PackageCrc32, destination);

        return HostCheck.Result(Failed(result, SelectedPayloadOwnedMemoryStatus.PackageValidationFailed,
                LivePackageStatus.MissingFile)
            && AllBytesEqual(destination, 0xEE));
    }

    [HostTest("storage.payload-owned rejects payload magic", HostLane.Fast, 4)]
    private static bool RejectsPayloadMagic() => HostCheck.Result(RejectHeader(
        static package => package[PayloadOffset] = (byte)'X', NativeAppPayloadHeaderStatus.InvalidMagic));

    [HostTest("storage.payload-owned rejects payload version", HostLane.Fast, 5)]
    private static bool RejectsPayloadVersion() => HostCheck.Result(RejectHeader(
        static package => BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(PayloadOffset + 8), 1),
        NativeAppPayloadHeaderStatus.UnsupportedVersion));

    [HostTest("storage.payload-owned rejects payload header size", HostLane.Fast, 6)]
    private static bool RejectsPayloadHeaderSize() => HostCheck.Result(RejectHeader(
        static package => BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(PayloadOffset + 10), 31),
        NativeAppPayloadHeaderStatus.InvalidHeaderSize));

    [HostTest("storage.payload-owned rejects payload flags", HostLane.Fast, 7)]
    private static bool RejectsPayloadFlags() => HostCheck.Result(RejectHeader(
        static package => BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(PayloadOffset + 12), 1),
        NativeAppPayloadHeaderStatus.UnsupportedFlags));

    [HostTest("storage.payload-owned rejects payload reserved bytes", HostLane.Fast, 8)]
    private static bool RejectsPayloadReservedBytes() => HostCheck.Result(RejectHeader(
        static package => BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(PayloadOffset + 24), 1),
        NativeAppPayloadHeaderStatus.InvalidReserved));

    [HostTest("storage.payload-owned checks entry offset boundaries", HostLane.Fast, 9)]
    private static bool ChecksEntryOffsetBoundaries()
    {
        bool valid = true;
        foreach (ulong invalid in new ulong[] { 31, PayloadLength, ulong.MaxValue })
            valid &= RejectHeader(
                package => BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(PayloadOffset + 16), invalid),
                NativeAppPayloadHeaderStatus.InvalidEntryOffset);

        foreach (uint entry in new uint[] { 32, PayloadLength - 1 })
        {
            byte[] package = CreatePackage();
            BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(PayloadOffset + 16), entry);
            byte[] destination = FilledDestination(PayloadLength);
            SelectedPayloadOwnedMemoryResult result = SelectedPayloadOwnedMemory.Stage(
                package, SelectedPath(), ReferenceCrc32(package), destination);
            valid &= result.Succeeded && result.BytesWritten == PayloadLength && result.EntryOffset == entry
                && NativeAppPayloadHeader.Parse(package.AsSpan(PayloadOffset)).Succeeded
                && destination.AsSpan().SequenceEqual(package.AsSpan(PayloadOffset));
        }

        return HostCheck.Result(valid);
    }

    [HostTest("storage.payload-owned rejects truncated payload header", HostLane.Fast, 10)]
    private static bool RejectsTruncatedPayloadHeader()
    {
        byte[] package = CreatePackage()[..(PayloadOffset + 31)];
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(48), 31);
        byte[] destination = FilledDestination(PayloadLength);
        SelectedPayloadOwnedMemoryResult result = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), ReferenceCrc32(package), destination);

        return HostCheck.Result(NativeAppPayloadHeader.Parse(package.AsSpan(PayloadOffset)).Status
                == NativeAppPayloadHeaderStatus.PayloadTooSmall
            && Failed(result, SelectedPayloadOwnedMemoryStatus.InvalidPayloadHeader, LivePackageStatus.Success)
            && AllBytesEqual(destination, 0xEE));
    }

    [HostTest("storage.payload-owned checks destination capacity", HostLane.Fast, 11)]
    private static bool ChecksDestinationCapacity()
    {
        byte[] package = CreatePackage();
        byte[] shortDestination = FilledDestination(PayloadLength - 1);
        byte[] exactDestination = FilledDestination(PayloadLength);
        SelectedPayloadOwnedMemoryResult shortResult = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), PackageCrc32, shortDestination);
        SelectedPayloadOwnedMemoryResult emptyResult = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), PackageCrc32, Span<byte>.Empty);
        SelectedPayloadOwnedMemoryResult exactResult = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), PackageCrc32, exactDestination);

        return HostCheck.Result(Failed(shortResult, SelectedPayloadOwnedMemoryStatus.DestinationTooSmall,
                LivePackageStatus.Success)
            && Failed(emptyResult, SelectedPayloadOwnedMemoryStatus.DestinationTooSmall, LivePackageStatus.Success)
            && AllBytesEqual(shortDestination, 0xEE)
            && exactResult.Succeeded && exactResult.BytesWritten == PayloadLength
            && exactDestination.AsSpan().SequenceEqual(package.AsSpan(PayloadOffset)));
    }

    [HostTest("storage.payload-owned rejects every buffer alias", HostLane.Fast, 12)]
    private static bool RejectsEveryBufferAlias()
    {
        byte[] package = CreatePackage();
        byte[] original = (byte[])package.Clone();
        byte[] destination = FilledDestination(PayloadLength);
        SelectedPayloadOwnedMemoryResult payloadAlias = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), PackageCrc32, package.AsSpan(PayloadOffset));
        SelectedPayloadOwnedMemoryResult metadataAlias = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), PackageCrc32, package.AsSpan(0, PayloadLength));
        SelectedPayloadOwnedMemoryResult inputAlias = SelectedPayloadOwnedMemory.Stage(
            package, package.AsSpan(PathOffset, 11), PackageCrc32, destination);
        byte[] pathBacking = FilledDestination(PayloadLength);
        SelectedPath().CopyTo(pathBacking, 0);
        byte[] pathOriginal = (byte[])pathBacking.Clone();
        SelectedPayloadOwnedMemoryResult pathAlias = SelectedPayloadOwnedMemory.Stage(
            package, pathBacking.AsSpan(0, 11), PackageCrc32, pathBacking);

        return HostCheck.Result(Failed(payloadAlias, SelectedPayloadOwnedMemoryStatus.OverlappingBuffers,
                LivePackageStatus.Success)
            && Failed(metadataAlias, SelectedPayloadOwnedMemoryStatus.OverlappingBuffers, LivePackageStatus.Success)
            && Failed(inputAlias, SelectedPayloadOwnedMemoryStatus.OverlappingBuffers, LivePackageStatus.Success)
            && Failed(pathAlias, SelectedPayloadOwnedMemoryStatus.OverlappingBuffers, LivePackageStatus.Success)
            && package.AsSpan().SequenceEqual(original)
            && pathBacking.AsSpan().SequenceEqual(pathOriginal)
            && AllBytesEqual(destination, 0xEE));
    }

    [HostTest("storage.payload-owned accepts adjacent buffers", HostLane.Fast, 13)]
    private static bool AcceptsAdjacentBuffers()
    {
        byte[] package = CreatePackage();
        byte[] backing = FilledDestination(package.Length + PayloadLength);
        package.CopyTo(backing, 0);
        SelectedPayloadOwnedMemoryResult result = SelectedPayloadOwnedMemory.Stage(
            backing.AsSpan(0, package.Length), SelectedPath(), PackageCrc32, backing.AsSpan(package.Length));

        return HostCheck.Result(result.Succeeded
            && backing.AsSpan(0, package.Length).SequenceEqual(package)
            && backing.AsSpan(package.Length).SequenceEqual(package.AsSpan(PayloadOffset)));
    }

    [HostTest("storage.payload-owned rejects null and wrapping pointers", HostLane.Fast, 14)]
    private static bool RejectsNullAndWrappingPointers()
    {
        byte[] package = CreatePackage();
        byte[] path = SelectedPath();
        byte[] destination = FilledDestination(PayloadLength);
        Span<byte> nullPackage = new((void*)0, 32);
        Span<byte> wrapping = new((void*)unchecked((nuint)(ulong.MaxValue - 8)), 32);
        Span<byte> nullPath = new((void*)0, 11);
        Span<byte> nullDestination = new((void*)0, PayloadLength);
        SelectedPayloadOwnedMemoryResult[] results =
        [
            SelectedPayloadOwnedMemory.Stage(nullPackage, path, PackageCrc32, destination),
            SelectedPayloadOwnedMemory.Stage(wrapping, path, PackageCrc32, destination),
            SelectedPayloadOwnedMemory.Stage(package, nullPath, PackageCrc32, destination),
            SelectedPayloadOwnedMemory.Stage(package, wrapping, PackageCrc32, destination),
            SelectedPayloadOwnedMemory.Stage(package, path, PackageCrc32, nullDestination),
            SelectedPayloadOwnedMemory.Stage(package, path, PackageCrc32, wrapping)
        ];
        bool valid = AllBytesEqual(destination, 0xEE);
        foreach (SelectedPayloadOwnedMemoryResult result in results)
            valid &= Failed(result, SelectedPayloadOwnedMemoryStatus.InvalidRange, LivePackageStatus.Uninitialized);
        return HostCheck.Result(valid);
    }

    [HostTest("storage.payload-owned default result is failure", HostLane.Fast, 15)]
    private static bool DefaultResultIsFailure()
    {
        SelectedPayloadOwnedMemoryResult result = default;
        return HostCheck.Result(Failed(result, SelectedPayloadOwnedMemoryStatus.Uninitialized,
                LivePackageStatus.Uninitialized)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<SelectedPayloadOwnedMemoryResult>());
    }

    [HostTest("storage.payload-owned staging allocates nothing", HostLane.Fast, 16)]
    private static bool StagingAllocatesNothing()
    {
        byte[] package = CreatePackage();
        byte[] path = SelectedPath();
        byte[] destination = FilledDestination(PayloadLength);
        bool valid = SelectedPayloadOwnedMemory.Stage(package, path, PackageCrc32, destination).Succeeded;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 64; index++)
            valid &= SelectedPayloadOwnedMemory.Stage(package, path, PackageCrc32, destination).Succeeded;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return HostCheck.Result(valid && allocated == 0);
    }

    [HostTest("storage.payload-owned byte fixture has independent crc32", HostLane.Fast, 17)]
    private static bool ByteFixtureHasIndependentCrc32() => HostCheck.Result(
        ReferenceCrc32("123456789"u8) == 0xCBF43926
        && ReferenceCrc32(CreatePackage()) == PackageCrc32
        && ReferenceCrc32(CreatePackage().AsSpan(PayloadOffset)) == PayloadCrc32);

    private static bool RejectHeader(Action<byte[]> mutate, NativeAppPayloadHeaderStatus expected)
    {
        byte[] package = CreatePackage();
        byte[] destination = FilledDestination(PayloadLength);
        mutate(package);
        NativeAppPayloadHeaderResult oracle = NativeAppPayloadHeader.Parse(package.AsSpan(PayloadOffset));
        SelectedPayloadOwnedMemoryResult result = SelectedPayloadOwnedMemory.Stage(
            package, SelectedPath(), ReferenceCrc32(package), destination);
        return !oracle.Succeeded && oracle.Status == expected
            && Failed(result, SelectedPayloadOwnedMemoryStatus.InvalidPayloadHeader, LivePackageStatus.Success)
            && AllBytesEqual(destination, 0xEE);
    }

    private static bool Failed(SelectedPayloadOwnedMemoryResult result,
        SelectedPayloadOwnedMemoryStatus status, LivePackageStatus packageStatus) =>
        !result.Succeeded && result.Status == status && result.PackageStatus == packageStatus
        && result.BytesWritten == 0 && result.EntryOffset == 0 && result.PayloadCrc32 == 0;

    private static byte[] SelectedPath() => "bin/app.bin"u8.ToArray();

    private static byte[] CreatePackage()
    {
        // Canonical OSPKGv0 + one 32-byte entry + path + OSAPPv0 payload.
        // BCL endian writers and literal byte values keep this independent of
        // APPPKGDisk and staging implementation constants.
        byte[] package = new byte[115];
        "OSPKGv0\0"u8.CopyTo(package);
        BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(10), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(14), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(32), PathOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(36), 11);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(40), PayloadOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(48), PayloadLength);
        "bin/app.bin"u8.CopyTo(package.AsSpan(PathOffset));
        "OSAPPv0\0"u8.CopyTo(package.AsSpan(PayloadOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(PayloadOffset + 10), 32);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(PayloadOffset + 16), 36);
        for (int index = 0; index < 8; index++)
            package[PayloadOffset + 32 + index] = (byte)(0xA0 + index);
        package[PayloadOffset + 36] = 0xC3;
        return package;
    }

    private static byte[] FilledDestination(int length)
    {
        byte[] destination = new byte[length];
        Array.Fill(destination, (byte)0xEE);
        return destination;
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> bytes, byte expected)
    {
        foreach (byte value in bytes)
            if (value != expected)
                return false;
        return true;
    }

    // Normal (MSB-first) polynomial with explicit bit reflection, independent of
    // the production reflected UpdateByte path. The standard check vector above
    // verifies the convention as well as the independently recorded fixture CRCs.
    private static uint ReferenceCrc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            uint reflected = 0;
            for (int bit = 0; bit < 8; bit++)
                reflected = (reflected << 1) | (uint)((value >> bit) & 1);
            crc ^= reflected << 24;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc << 1) ^ ((crc & 0x80000000) != 0 ? 0x04C11DB7u : 0u);
        }
        uint reversed = 0;
        for (int bit = 0; bit < 32; bit++)
            reversed = (reversed << 1) | ((crc >> bit) & 1);
        return reversed ^ uint.MaxValue;
    }
}
