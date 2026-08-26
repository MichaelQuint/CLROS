namespace OS.Kernel.Diagnostics;

public enum OwnedIdtGateTableOperation : byte
{
    None = 0,
    Materialize = 1,
    ValidateReadBack = 2
}

public enum OwnedIdtGateTableStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    TableCapacityTooSmall = 2,
    MissingTable = 3,
    MisalignedTablePointer = 4,
    NonCanonicalTablePointer = 5,
    TablePointerSpanOverflow = 6,
    NonCanonicalTablePointerSpan = 7,
    NullTableAddress = 8,
    MisalignedTableAddress = 9,
    NonCanonicalTableAddress = 10,
    TableAddressSpanOverflow = 11,
    NonCanonicalTableAddressSpan = 12,
    NullStubPageAddress = 13,
    MisalignedStubPageAddress = 14,
    NonCanonicalStubPageAddress = 15,
    StubPageAddressSpanOverflow = 16,
    NonCanonicalStubPageAddressSpan = 17,
    TableOverlapsStubPage = 18,
    TableByteMismatch = 19
}

public readonly struct OwnedIdtGateTableResult
{
    private readonly byte _isInitialized;
    private readonly byte _destinationMutated;
    private readonly byte _readBackPerformed;
    private readonly int _descriptorCount;
    private readonly int _presentDescriptorCount;
    private readonly int _istRoutedDescriptorCount;
    private readonly int _defaultIstDescriptorCount;
    private readonly int _firstMismatchOffset;

    private OwnedIdtGateTableResult(
        OwnedIdtGateTableOperation operation,
        OwnedIdtGateTableStatus status,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress,
        ulong tableCapacity,
        int bytesWritten,
        int bytesCompared,
        int firstMismatchOffset,
        ulong fingerprint,
        ulong normalizedPolicyFingerprint,
        int descriptorCount,
        int presentDescriptorCount,
        int istRoutedDescriptorCount,
        int defaultIstDescriptorCount,
        bool destinationMutated,
        bool readBackPerformed)
    {
        _isInitialized = 1;
        _destinationMutated = destinationMutated ? (byte)1 : (byte)0;
        _readBackPerformed = readBackPerformed ? (byte)1 : (byte)0;
        Operation = operation;
        Status = status;
        ModeledTableAddress = modeledTableAddress;
        OwnedStubPageAddress = ownedStubPageAddress;
        TableCapacity = tableCapacity;
        BytesWritten = bytesWritten;
        BytesCompared = bytesCompared;
        _firstMismatchOffset = firstMismatchOffset;
        Fingerprint = fingerprint;
        NormalizedPolicyFingerprint = normalizedPolicyFingerprint;
        _descriptorCount = descriptorCount;
        _presentDescriptorCount = presentDescriptorCount;
        _istRoutedDescriptorCount = istRoutedDescriptorCount;
        _defaultIstDescriptorCount = defaultIstDescriptorCount;
    }

    public bool IsInitialized => _isInitialized == 1;

    public OwnedIdtGateTableOperation Operation { get; }

    public OwnedIdtGateTableStatus Status { get; }

    public ulong ModeledTableAddress { get; }

    public ulong OwnedStubPageAddress { get; }

    public ulong TableCapacity { get; }

    public int RequiredBytes =>
        PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount;

    public int DescriptorCount => _descriptorCount;

    public int PresentDescriptorCount => _presentDescriptorCount;

    public int IstRoutedDescriptorCount => _istRoutedDescriptorCount;

    public int DefaultIstDescriptorCount => _defaultIstDescriptorCount;

    public int FirmwareGateCopyCount => 0;

    public int BytesWritten { get; }

    public int BytesCompared { get; }

    public int FirstMismatchOffset =>
        IsInitialized ? _firstMismatchOffset : -1;

    public int FirstMismatchVector =>
        FirstMismatchOffset < 0
            ? -1
            : FirstMismatchOffset /
                PopulateAllN256IDTVectorsOwnedGatesNever.DescriptorByteCount;

    public ulong Fingerprint { get; }

    public ulong NormalizedPolicyFingerprint { get; }

    public bool DestinationMutated => _destinationMutated == 1;

    public bool ReadBackPerformed => _readBackPerformed == 1;

    public bool Materialized =>
        IsInitialized &&
        Operation == OwnedIdtGateTableOperation.Materialize &&
        Status == OwnedIdtGateTableStatus.Success &&
        BytesWritten == RequiredBytes &&
        BytesCompared == RequiredBytes &&
        FirstMismatchOffset == -1 &&
        DescriptorCount == 256 &&
        PresentDescriptorCount == 256 &&
        IstRoutedDescriptorCount == 3 &&
        DefaultIstDescriptorCount == 253 &&
        FirmwareGateCopyCount == 0 &&
        NormalizedPolicyFingerprint ==
            PopulateAllN256IDTVectorsOwnedGatesNever
                .NormalizedPolicyFnv1A64 &&
        DestinationMutated &&
        ReadBackPerformed;

    public bool ReadBackValidated =>
        IsInitialized &&
        Operation == OwnedIdtGateTableOperation.ValidateReadBack &&
        Status == OwnedIdtGateTableStatus.Success &&
        BytesWritten == 0 &&
        BytesCompared == RequiredBytes &&
        FirstMismatchOffset == -1 &&
        DescriptorCount == 256 &&
        PresentDescriptorCount == 256 &&
        IstRoutedDescriptorCount == 3 &&
        DefaultIstDescriptorCount == 253 &&
        FirmwareGateCopyCount == 0 &&
        NormalizedPolicyFingerprint ==
            PopulateAllN256IDTVectorsOwnedGatesNever
                .NormalizedPolicyFnv1A64 &&
        !DestinationMutated &&
        ReadBackPerformed;

    public bool Succeeded => Materialized || ReadBackValidated;

    internal static OwnedIdtGateTableResult Pass(
        OwnedIdtGateTableOperation operation,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress,
        ulong tableCapacity,
        int bytesWritten,
        ulong fingerprint,
        bool destinationMutated)
    {
        return new OwnedIdtGateTableResult(
            operation,
            OwnedIdtGateTableStatus.Success,
            modeledTableAddress,
            ownedStubPageAddress,
            tableCapacity,
            bytesWritten,
            PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount,
            -1,
            fingerprint,
            PopulateAllN256IDTVectorsOwnedGatesNever
                .NormalizedPolicyFnv1A64,
            PopulateAllN256IDTVectorsOwnedGatesNever.VectorCount,
            PopulateAllN256IDTVectorsOwnedGatesNever.VectorCount,
            PopulateAllN256IDTVectorsOwnedGatesNever.IstRoutedVectorCount,
            PopulateAllN256IDTVectorsOwnedGatesNever.DefaultIstVectorCount,
            destinationMutated,
            true);
    }

    internal static OwnedIdtGateTableResult Fail(
        OwnedIdtGateTableOperation operation,
        OwnedIdtGateTableStatus status,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress,
        ulong tableCapacity,
        int bytesWritten = 0,
        int bytesCompared = 0,
        int firstMismatchOffset = -1,
        ulong fingerprint = 0,
        bool destinationMutated = false,
        bool readBackPerformed = false)
    {
        return new OwnedIdtGateTableResult(
            operation,
            status,
            modeledTableAddress,
            ownedStubPageAddress,
            tableCapacity,
            bytesWritten,
            bytesCompared,
            firstMismatchOffset,
            fingerprint,
            0,
            0,
            0,
            0,
            0,
            destinationMutated,
            readBackPerformed);
    }
}

/// <summary>
/// Synthesizes one complete x64 IDT page from CLROS-owned constants and the
/// caller-named M02-010 interrupt-entry page. There is deliberately no input
/// for a firmware IDT or firmware gate. This contract writes and validates
/// ordinary caller-owned bytes only; it does not execute a slot, load IDTR,
/// activate IST routing, or trigger a fault.
/// </summary>
public static unsafe class PopulateAllN256IDTVectorsOwnedGatesNever
{
    public const int VectorCount = 256;
    public const int DescriptorByteCount = 16;
    public const int TableByteCount = VectorCount * DescriptorByteCount;
    public const int TableAlignment = TableByteCount;
    public const int StubSlotByteCount = 16;

    public const ushort KernelCodeSelector = 0x0008;
    public const byte DefaultIstIndex = 0;
    public const byte DoubleFaultIstIndex = 1;
    public const byte PageFaultIstIndex = 2;
    public const byte NmiIstIndex = 3;
    public const byte InvalidIstIndex = 0xff;

    public const int NonMaskableInterruptVector = 2;
    public const int DoubleFaultVector = 8;
    public const int PageFaultVector = 14;
    public const int IstRoutedVectorCount = 3;
    public const int DefaultIstVectorCount =
        VectorCount - IstRoutedVectorCount;

    public const byte PresentInterruptGateAttributes = 0x8e;
    public const ulong Fnv1A64OffsetBasis = 14_695_981_039_346_656_037UL;
    public const ulong Fnv1A64Prime = 1_099_511_628_211UL;
    public const ulong NormalizedPolicyFnv1A64 = 0x8514_f42e_bdfc_5d55UL;

#if !NOSTDLIB_EFI
    public static OwnedIdtGateTableResult Materialize(
        Span<byte> destination,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress)
    {
        fixed (byte* destinationPointer = destination)
        {
            return Materialize(
                destinationPointer,
                (ulong)destination.Length,
                modeledTableAddress,
                ownedStubPageAddress);
        }
    }

    public static OwnedIdtGateTableResult ValidateReadBack(
        ReadOnlySpan<byte> source,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress)
    {
        fixed (byte* sourcePointer = source)
        {
            return ValidateReadBack(
                sourcePointer,
                (ulong)source.Length,
                modeledTableAddress,
                ownedStubPageAddress);
        }
    }
#endif

    public static OwnedIdtGateTableResult Materialize(
        byte* destination,
        ulong destinationCapacity,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress)
    {
        OwnedIdtGateTableStatus status = ValidateStorageAndAddresses(
            destination,
            destinationCapacity,
            modeledTableAddress,
            ownedStubPageAddress);
        if (status != OwnedIdtGateTableStatus.Success)
        {
            return OwnedIdtGateTableResult.Fail(
                OwnedIdtGateTableOperation.Materialize,
                status,
                modeledTableAddress,
                ownedStubPageAddress,
                destinationCapacity);
        }

        ulong expectedFingerprint = ComputeExpectedFingerprint(
            ownedStubPageAddress);
        for (int offset = 0; offset < TableByteCount; offset++)
        {
            destination[offset] = ExpectedByteAt(
                offset,
                ownedStubPageAddress);
        }

        ulong observedFingerprint = ComputeFingerprint(
            destination,
            TableByteCount);
        for (int offset = 0; offset < TableByteCount; offset++)
        {
            if (destination[offset] != ExpectedByteAt(
                    offset,
                    ownedStubPageAddress))
            {
                return OwnedIdtGateTableResult.Fail(
                    OwnedIdtGateTableOperation.Materialize,
                    OwnedIdtGateTableStatus.TableByteMismatch,
                    modeledTableAddress,
                    ownedStubPageAddress,
                    destinationCapacity,
                    TableByteCount,
                    offset + 1,
                    offset,
                    observedFingerprint,
                    true,
                    true);
            }
        }

        if (observedFingerprint != expectedFingerprint)
        {
            return OwnedIdtGateTableResult.Fail(
                OwnedIdtGateTableOperation.Materialize,
                OwnedIdtGateTableStatus.TableByteMismatch,
                modeledTableAddress,
                ownedStubPageAddress,
                destinationCapacity,
                TableByteCount,
                TableByteCount,
                -1,
                observedFingerprint,
                true,
                true);
        }

        return OwnedIdtGateTableResult.Pass(
            OwnedIdtGateTableOperation.Materialize,
            modeledTableAddress,
            ownedStubPageAddress,
            destinationCapacity,
            TableByteCount,
            observedFingerprint,
            true);
    }

    public static OwnedIdtGateTableResult ValidateReadBack(
        byte* source,
        ulong sourceCapacity,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress)
    {
        OwnedIdtGateTableStatus status = ValidateStorageAndAddresses(
            source,
            sourceCapacity,
            modeledTableAddress,
            ownedStubPageAddress);
        if (status != OwnedIdtGateTableStatus.Success)
        {
            return OwnedIdtGateTableResult.Fail(
                OwnedIdtGateTableOperation.ValidateReadBack,
                status,
                modeledTableAddress,
                ownedStubPageAddress,
                sourceCapacity);
        }

        ulong observedFingerprint = ComputeFingerprint(source, TableByteCount);
        for (int offset = 0; offset < TableByteCount; offset++)
        {
            if (source[offset] != ExpectedByteAt(
                    offset,
                    ownedStubPageAddress))
            {
                return OwnedIdtGateTableResult.Fail(
                    OwnedIdtGateTableOperation.ValidateReadBack,
                    OwnedIdtGateTableStatus.TableByteMismatch,
                    modeledTableAddress,
                    ownedStubPageAddress,
                    sourceCapacity,
                    0,
                    offset + 1,
                    offset,
                    observedFingerprint,
                    false,
                    true);
            }
        }

        return OwnedIdtGateTableResult.Pass(
            OwnedIdtGateTableOperation.ValidateReadBack,
            modeledTableAddress,
            ownedStubPageAddress,
            sourceCapacity,
            0,
            observedFingerprint,
            false);
    }

    public static byte GetIstIndex(int vector)
    {
        if (vector < 0 || vector >= VectorCount)
        {
            return InvalidIstIndex;
        }

        return vector switch
        {
            DoubleFaultVector => DoubleFaultIstIndex,
            PageFaultVector => PageFaultIstIndex,
            NonMaskableInterruptVector => NmiIstIndex,
            _ => DefaultIstIndex
        };
    }

    public static bool IsCanonicalX64Address(ulong address)
    {
        ulong highBits = address >> 48;
        bool signBitSet = ((address >> 47) & 1UL) != 0;
        return signBitSet
            ? highBits == 0xffffUL
            : highBits == 0;
    }

    private static OwnedIdtGateTableStatus ValidateStorageAndAddresses(
        byte* table,
        ulong tableCapacity,
        ulong modeledTableAddress,
        ulong ownedStubPageAddress)
    {
        if (tableCapacity < TableByteCount)
        {
            return OwnedIdtGateTableStatus.TableCapacityTooSmall;
        }

        if (table == null)
        {
            return OwnedIdtGateTableStatus.MissingTable;
        }

        ulong tablePointerAddress = (ulong)table;
        if (!IsAligned(tablePointerAddress))
        {
            return OwnedIdtGateTableStatus.MisalignedTablePointer;
        }

        if (!IsCanonicalX64Address(tablePointerAddress))
        {
            return OwnedIdtGateTableStatus.NonCanonicalTablePointer;
        }

        if (tablePointerAddress > ~0UL - tableCapacity)
        {
            return OwnedIdtGateTableStatus.TablePointerSpanOverflow;
        }

        if (!IsCanonicalSpan(tablePointerAddress, tableCapacity))
        {
            return OwnedIdtGateTableStatus.NonCanonicalTablePointerSpan;
        }

        if (modeledTableAddress == 0)
        {
            return OwnedIdtGateTableStatus.NullTableAddress;
        }

        if (!IsAligned(modeledTableAddress))
        {
            return OwnedIdtGateTableStatus.MisalignedTableAddress;
        }

        if (!IsCanonicalX64Address(modeledTableAddress))
        {
            return OwnedIdtGateTableStatus.NonCanonicalTableAddress;
        }

        if (modeledTableAddress > ~0UL - TableByteCount)
        {
            return OwnedIdtGateTableStatus.TableAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(modeledTableAddress, TableByteCount))
        {
            return OwnedIdtGateTableStatus.NonCanonicalTableAddressSpan;
        }

        if (ownedStubPageAddress == 0)
        {
            return OwnedIdtGateTableStatus.NullStubPageAddress;
        }

        if (!IsAligned(ownedStubPageAddress))
        {
            return OwnedIdtGateTableStatus.MisalignedStubPageAddress;
        }

        if (!IsCanonicalX64Address(ownedStubPageAddress))
        {
            return OwnedIdtGateTableStatus.NonCanonicalStubPageAddress;
        }

        if (ownedStubPageAddress > ~0UL - TableByteCount)
        {
            return OwnedIdtGateTableStatus.StubPageAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(ownedStubPageAddress, TableByteCount))
        {
            return OwnedIdtGateTableStatus.NonCanonicalStubPageAddressSpan;
        }

        ulong tableEnd = modeledTableAddress + TableByteCount;
        ulong stubPageEnd = ownedStubPageAddress + TableByteCount;
        if (modeledTableAddress < stubPageEnd &&
            ownedStubPageAddress < tableEnd)
        {
            return OwnedIdtGateTableStatus.TableOverlapsStubPage;
        }

        return OwnedIdtGateTableStatus.Success;
    }

    private static bool IsAligned(ulong address)
    {
        return (address & (TableAlignment - 1UL)) == 0;
    }

    private static bool IsCanonicalSpan(ulong start, ulong length)
    {
        ulong endInclusive = start + length - 1;
        if (!IsCanonicalX64Address(endInclusive))
        {
            return false;
        }

        return ((start >> 47) & 1UL) ==
            ((endInclusive >> 47) & 1UL);
    }

    private static byte ExpectedByteAt(
        int tableOffset,
        ulong ownedStubPageAddress)
    {
        int vector = tableOffset / DescriptorByteCount;
        int descriptorOffset =
            tableOffset - (vector * DescriptorByteCount);
        ulong handlerAddress = ownedStubPageAddress +
            ((ulong)vector * StubSlotByteCount);

        return descriptorOffset switch
        {
            0 => (byte)handlerAddress,
            1 => (byte)(handlerAddress >> 8),
            2 => (byte)KernelCodeSelector,
            3 => (byte)(KernelCodeSelector >> 8),
            4 => GetIstIndex(vector),
            5 => PresentInterruptGateAttributes,
            6 => (byte)(handlerAddress >> 16),
            7 => (byte)(handlerAddress >> 24),
            8 => (byte)(handlerAddress >> 32),
            9 => (byte)(handlerAddress >> 40),
            10 => (byte)(handlerAddress >> 48),
            11 => (byte)(handlerAddress >> 56),
            _ => 0
        };
    }

    private static ulong ComputeExpectedFingerprint(
        ulong ownedStubPageAddress)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < TableByteCount; offset++)
        {
            fingerprint ^= ExpectedByteAt(offset, ownedStubPageAddress);
            fingerprint = unchecked(fingerprint * Fnv1A64Prime);
        }

        return fingerprint;
    }

    private static ulong ComputeFingerprint(byte* source, int length)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < length; offset++)
        {
            fingerprint ^= source[offset];
            fingerprint = unchecked(fingerprint * Fnv1A64Prime);
        }

        return fingerprint;
    }
}
