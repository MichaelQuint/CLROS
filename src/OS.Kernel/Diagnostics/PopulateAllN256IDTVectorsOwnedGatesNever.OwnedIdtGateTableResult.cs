namespace OS.Kernel.Diagnostics;
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
    private OwnedIdtGateTableResult(OwnedIdtGateTableOperation operation, OwnedIdtGateTableStatus status, ulong modeledTableAddress, ulong ownedStubPageAddress, ulong tableCapacity, int bytesWritten, int bytesCompared, int firstMismatchOffset, ulong fingerprint, ulong normalizedPolicyFingerprint, int descriptorCount, int presentDescriptorCount, int istRoutedDescriptorCount, int defaultIstDescriptorCount, bool destinationMutated, bool readBackPerformed)
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
    public int RequiredBytes => PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount;
    public int DescriptorCount => _descriptorCount;
    public int PresentDescriptorCount => _presentDescriptorCount;
    public int IstRoutedDescriptorCount => _istRoutedDescriptorCount;
    public int DefaultIstDescriptorCount => _defaultIstDescriptorCount;
    public int FirmwareGateCopyCount => 0;
    public int BytesWritten { get; }
    public int BytesCompared { get; }
    public int FirstMismatchOffset => IsInitialized ? _firstMismatchOffset : -1;
    public int FirstMismatchVector => FirstMismatchOffset < 0 ? -1 : FirstMismatchOffset / PopulateAllN256IDTVectorsOwnedGatesNever.DescriptorByteCount;
    public ulong Fingerprint { get; }
    public ulong NormalizedPolicyFingerprint { get; }
    public bool DestinationMutated => _destinationMutated == 1;
    public bool ReadBackPerformed => _readBackPerformed == 1;
    public bool Materialized => IsInitialized && Operation == OwnedIdtGateTableOperation.Materialize && Status == OwnedIdtGateTableStatus.Success && BytesWritten == RequiredBytes && BytesCompared == RequiredBytes && FirstMismatchOffset == -1 && DescriptorCount == 256 && PresentDescriptorCount == 256 && IstRoutedDescriptorCount == 3 && DefaultIstDescriptorCount == 253 && FirmwareGateCopyCount == 0 && NormalizedPolicyFingerprint == PopulateAllN256IDTVectorsOwnedGatesNever.NormalizedPolicyFnv1A64 && DestinationMutated && ReadBackPerformed;
    public bool ReadBackValidated => IsInitialized && Operation == OwnedIdtGateTableOperation.ValidateReadBack && Status == OwnedIdtGateTableStatus.Success && BytesWritten == 0 && BytesCompared == RequiredBytes && FirstMismatchOffset == -1 && DescriptorCount == 256 && PresentDescriptorCount == 256 && IstRoutedDescriptorCount == 3 && DefaultIstDescriptorCount == 253 && FirmwareGateCopyCount == 0 && NormalizedPolicyFingerprint == PopulateAllN256IDTVectorsOwnedGatesNever.NormalizedPolicyFnv1A64 && !DestinationMutated && ReadBackPerformed;
    public bool Succeeded => Materialized || ReadBackValidated;

    internal static OwnedIdtGateTableResult Pass(OwnedIdtGateTableOperation operation, ulong modeledTableAddress, ulong ownedStubPageAddress, ulong tableCapacity, int bytesWritten, ulong fingerprint, bool destinationMutated)
    {
        return new OwnedIdtGateTableResult(operation, OwnedIdtGateTableStatus.Success, modeledTableAddress, ownedStubPageAddress, tableCapacity, bytesWritten, PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount, -1, fingerprint, PopulateAllN256IDTVectorsOwnedGatesNever.NormalizedPolicyFnv1A64, PopulateAllN256IDTVectorsOwnedGatesNever.VectorCount, PopulateAllN256IDTVectorsOwnedGatesNever.VectorCount, PopulateAllN256IDTVectorsOwnedGatesNever.IstRoutedVectorCount, PopulateAllN256IDTVectorsOwnedGatesNever.DefaultIstVectorCount, destinationMutated, true);
    }

    internal static OwnedIdtGateTableResult Fail(OwnedIdtGateTableOperation operation, OwnedIdtGateTableStatus status, ulong modeledTableAddress, ulong ownedStubPageAddress, ulong tableCapacity, int bytesWritten = 0, int bytesCompared = 0, int firstMismatchOffset = -1, ulong fingerprint = 0, bool destinationMutated = false, bool readBackPerformed = false)
    {
        return new OwnedIdtGateTableResult(operation, status, modeledTableAddress, ownedStubPageAddress, tableCapacity, bytesWritten, bytesCompared, firstMismatchOffset, fingerprint, 0, 0, 0, 0, 0, destinationMutated, readBackPerformed);
    }
}
#if !NOSTDLIB_EFI
#endif
