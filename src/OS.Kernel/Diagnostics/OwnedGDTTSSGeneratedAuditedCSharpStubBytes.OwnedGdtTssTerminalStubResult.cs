namespace OS.Kernel.Diagnostics;

public readonly struct OwnedGdtTssTerminalStubResult
{
    private const byte PrepareContextOperation = 1;
    private const byte EmitStubOperation = 2;
    private const byte ValidateReadBackOperation = 3;
    private readonly byte _isInitialized;
    private readonly byte _operation;
    private OwnedGdtTssTerminalStubResult(OwnedGdtTssTerminalStubStatus status, byte operation, ulong modeledContextAddress, ulong modeledStubAddress, ulong contextCapacity, ulong stubCapacity, int contextBytesWritten, int contextBytesCompared, int stubBytesWritten, int stubBytesCompared, int firstMismatchOffset, ulong contextFingerprint, ulong stubFingerprint)
    {
        _isInitialized = 1;
        _operation = operation;
        Status = status;
        ModeledContextAddress = modeledContextAddress;
        ModeledStubAddress = modeledStubAddress;
        ContextCapacity = contextCapacity;
        StubCapacity = stubCapacity;
        ContextBytesWritten = contextBytesWritten;
        ContextBytesCompared = contextBytesCompared;
        StubBytesWritten = stubBytesWritten;
        StubBytesCompared = stubBytesCompared;
        FirstMismatchOffset = firstMismatchOffset;
        ContextFingerprint = contextFingerprint;
        StubFingerprint = stubFingerprint;
    }

    public bool IsInitialized => _isInitialized == 1;
    public OwnedGdtTssTerminalStubStatus Status
    {
        get;
    }
    public ulong ModeledContextAddress
    {
        get;
    }
    public ulong ModeledStubAddress
    {
        get;
    }
    public ulong ContextCapacity
    {
        get;
    }
    public ulong StubCapacity
    {
        get;
    }
    public int ContextBytesWritten
    {
        get;
    }
    public int ContextBytesCompared
    {
        get;
    }
    public int StubBytesWritten
    {
        get;
    }
    public int StubBytesCompared
    {
        get;
    }
    public int FirstMismatchOffset
    {
        get;
    }
    public ulong ContextFingerprint
    {
        get;
    }
    public ulong StubFingerprint
    {
        get;
    }
    public ulong ExpectedStubFingerprint => OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubFnv1A64;
    public bool ContextPrepared => IsInitialized && _operation == PrepareContextOperation && Status == OwnedGdtTssTerminalStubStatus.Success && ContextBytesWritten == OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount && ContextBytesCompared == OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount && StubBytesWritten == 0 && StubBytesCompared == 0 && FirstMismatchOffset == -1 && ContextFingerprint != 0;
    public bool StubEmitted => IsInitialized && _operation == EmitStubOperation && Status == OwnedGdtTssTerminalStubStatus.Success && ContextBytesWritten == 0 && ContextBytesCompared == OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount && StubBytesWritten == OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount && StubBytesCompared == 0 && FirstMismatchOffset == -1 && ContextFingerprint != 0 && StubFingerprint == ExpectedStubFingerprint;
    public bool ReadBackValidated => IsInitialized && _operation == ValidateReadBackOperation && Status == OwnedGdtTssTerminalStubStatus.Success && ContextBytesWritten == 0 && ContextBytesCompared == OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount && StubBytesWritten == 0 && StubBytesCompared == OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount && FirstMismatchOffset == -1 && ContextFingerprint != 0 && StubFingerprint == ExpectedStubFingerprint;
    public bool Succeeded => ContextPrepared || StubEmitted || ReadBackValidated;

    internal static OwnedGdtTssTerminalStubResult Prepared(ulong modeledContextAddress, ulong contextCapacity, ulong contextFingerprint)
    {
        return new OwnedGdtTssTerminalStubResult(OwnedGdtTssTerminalStubStatus.Success, PrepareContextOperation, modeledContextAddress, 0, contextCapacity, 0, OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount, OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount, 0, 0, -1, contextFingerprint, 0);
    }

    internal static OwnedGdtTssTerminalStubResult Emitted(ulong modeledContextAddress, ulong modeledStubAddress, ulong contextCapacity, ulong stubCapacity, ulong contextFingerprint, ulong stubFingerprint)
    {
        return new OwnedGdtTssTerminalStubResult(OwnedGdtTssTerminalStubStatus.Success, EmitStubOperation, modeledContextAddress, modeledStubAddress, contextCapacity, stubCapacity, 0, OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount, OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount, 0, -1, contextFingerprint, stubFingerprint);
    }

    internal static OwnedGdtTssTerminalStubResult Validated(ulong modeledContextAddress, ulong modeledStubAddress, ulong contextCapacity, ulong stubCapacity, ulong contextFingerprint, ulong stubFingerprint)
    {
        return new OwnedGdtTssTerminalStubResult(OwnedGdtTssTerminalStubStatus.Success, ValidateReadBackOperation, modeledContextAddress, modeledStubAddress, contextCapacity, stubCapacity, 0, OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount, 0, OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount, -1, contextFingerprint, stubFingerprint);
    }

    internal static OwnedGdtTssTerminalStubResult Fail(OwnedGdtTssTerminalStubStatus status, byte operation, ulong modeledContextAddress, ulong modeledStubAddress, ulong contextCapacity, ulong stubCapacity, int contextBytesCompared = 0, int stubBytesCompared = 0, int firstMismatchOffset = -1, ulong contextFingerprint = 0, ulong stubFingerprint = 0)
    {
        return new OwnedGdtTssTerminalStubResult(status, operation, modeledContextAddress, modeledStubAddress, contextCapacity, stubCapacity, 0, contextBytesCompared, 0, stubBytesCompared, firstMismatchOffset, contextFingerprint, stubFingerprint);
    }

    internal static byte PrepareOperation => PrepareContextOperation;
    internal static byte EmitOperation => EmitStubOperation;
    internal static byte ValidateOperation => ValidateReadBackOperation;
}
#if !NOSTDLIB_EFI
#endif
