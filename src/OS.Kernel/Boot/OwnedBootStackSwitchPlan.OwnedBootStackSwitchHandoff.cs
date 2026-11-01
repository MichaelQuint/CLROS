namespace OS.Boot;
/// <summary>
/// Describes the POD payload that a future static handoff buffer will hold.
/// The x64 stub leaves the handoff address in RDX, publishes the observed
/// continuation-entry RSP in RCX, and jumps to the nonreturning continuation.
/// </summary>

public readonly struct OwnedBootStackSwitchHandoff
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 88;
    public const ulong RequiredStorageAlignment = 8;
    private readonly uint _size;
    private readonly uint _version;
    private readonly VirtualAddress _storageStart;
    private readonly VirtualAddress _storageEndExclusive;
    private readonly ulong _storageByteLength;
    private readonly VirtualAddress _stubEntry;
    private readonly VirtualAddress _usableStackStart;
    private readonly VirtualAddress _usableStackEndExclusive;
    private readonly VirtualAddress _preTransferStackPointer;
    private readonly VirtualAddress _expectedContinuationStackPointer;
    private readonly VirtualAddress _continuation;
    private readonly OwnedBootStackSwitchHandoffProvenance _provenance;
    private readonly OwnedBootStackSwitchContinuationContract _continuationContract;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private OwnedBootStackSwitchHandoff(VirtualAddress storageStart, VirtualAddress storageEndExclusive, ulong storageByteLength, VirtualAddress stubEntry, VirtualAddress usableStackStart, VirtualAddress usableStackEndExclusive, VirtualAddress preTransferStackPointer, VirtualAddress expectedContinuationStackPointer, VirtualAddress continuation, OwnedBootStackSwitchHandoffProvenance provenance, OwnedBootStackSwitchContinuationContract continuationContract)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _storageStart = storageStart;
        _storageEndExclusive = storageEndExclusive;
        _storageByteLength = storageByteLength;
        _stubEntry = stubEntry;
        _usableStackStart = usableStackStart;
        _usableStackEndExclusive = usableStackEndExclusive;
        _preTransferStackPointer = preTransferStackPointer;
        _expectedContinuationStackPointer = expectedContinuationStackPointer;
        _continuation = continuation;
        _provenance = provenance;
        _continuationContract = continuationContract;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
    }

    public uint Size => _size;
    public uint Version => _version;
    public VirtualAddress StorageStart => _storageStart;
    public VirtualAddress StorageEndExclusive => _storageEndExclusive;
    public ulong StorageByteLength => _storageByteLength;
    public VirtualAddress StubEntry => _stubEntry;
    public VirtualAddress UsableStackStart => _usableStackStart;
    public VirtualAddress UsableStackEndExclusive => _usableStackEndExclusive;
    public VirtualAddress PreTransferStackPointer => _preTransferStackPointer;
    public VirtualAddress ExpectedContinuationStackPointer => _expectedContinuationStackPointer;
    public VirtualAddress Continuation => _continuation;
    public OwnedBootStackSwitchHandoffProvenance Provenance => _provenance;
    public OwnedBootStackSwitchContinuationContract ContinuationContract => _continuationContract;
    public bool IsValid => Validate();

    internal static OwnedBootStackSwitchHandoff Create(VirtualAddress storageStart, VirtualAddress storageEndExclusive, ulong storageByteLength, VirtualAddress stubEntry, VirtualAddress usableStackStart, VirtualAddress usableStackEndExclusive, VirtualAddress preTransferStackPointer, VirtualAddress expectedContinuationStackPointer, VirtualAddress continuation, OwnedBootStackSwitchHandoffProvenance provenance, OwnedBootStackSwitchContinuationContract continuationContract)
    {
        return new OwnedBootStackSwitchHandoff(storageStart, storageEndExclusive, storageByteLength, stubEntry, usableStackStart, usableStackEndExclusive, preTransferStackPointer, expectedContinuationStackPointer, continuation, provenance, continuationContract);
    }

    internal bool Validate()
    {
        if (_size != StructureSize || _version != CurrentVersion || !_storageStart.IsValid || !_storageStart.IsAligned(RequiredStorageAlignment) || !_storageEndExclusive.IsValid || _storageByteLength != StructureSize || !_stubEntry.IsValid || !_stubEntry.IsAligned(OwnedBootStackSwitchPlan.StubEntryAlignmentBytes) || !_usableStackStart.IsValid || !_usableStackEndExclusive.IsValid || !_preTransferStackPointer.IsValid || !_expectedContinuationStackPointer.IsValid || !_continuation.IsValid || _provenance != OwnedBootStackSwitchHandoffProvenance.CallerAttestedStaticOwnedData || _continuationContract != OwnedBootStackSwitchContinuationContract.StaticNonReturningRspAndHandoff || _reserved0 != 0 || _reserved1 != 0 || _reserved2 != 0 || _reserved3 != 0 || _reserved4 != 0 || _reserved5 != 0)
        {
            return false;
        }

        if (_storageStart.TryGetEndExclusive(_storageByteLength, out VirtualAddress expectedStorageEnd) != AddressValueStatus.Success || !expectedStorageEnd.Equals(_storageEndExclusive) || !TryGetRaw(_usableStackStart, out ulong usableStart) || !TryGetRaw(_usableStackEndExclusive, out ulong usableEnd) || !TryGetRaw(_preTransferStackPointer, out ulong preTransfer) || !TryGetRaw(_expectedContinuationStackPointer, out ulong continuationRsp) || usableStart >= usableEnd || continuationRsp < usableStart || preTransfer < continuationRsp || preTransfer - continuationRsp != OwnedBootStackSwitchPlan.FakeReturnAddressBytes || usableEnd < preTransfer || usableEnd - preTransfer != OwnedBootStackSwitchPlan.X64ShadowSpaceBytes || (preTransfer & (OwnedBootStackSwitchPlan.X64StackAlignmentBytes - 1)) != 0 || (continuationRsp & (OwnedBootStackSwitchPlan.X64StackAlignmentBytes - 1)) != OwnedBootStackSwitchPlan.ContinuationEntryAlignmentRemainder)
        {
            return false;
        }

        return true;
    }

    private static bool TryGetRaw(VirtualAddress address, out ulong value)
    {
        return address.TryGetRawValueForUefiBoundary(out value) == AddressValueStatus.Success;
    }
}
