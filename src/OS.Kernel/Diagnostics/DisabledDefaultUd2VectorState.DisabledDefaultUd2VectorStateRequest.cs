namespace OS.Kernel.Diagnostics;
/// <summary>
/// Versioned, unmanaged caller input. Readiness fields are caller
/// attestations, not observations of active guest descriptor or serial state.
/// </summary>

public readonly struct DisabledDefaultUd2VectorStateRequest
{
    public const uint StructureSize = 32;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _enabledByDefault;
    private readonly byte _executionRequested;
    private readonly byte _executionAttempted;
    private readonly byte _ownedSerialReady;
    private readonly byte _ownedGdtTssReady;
    private readonly byte _ownedIdtReady;
    private readonly byte _invalidOpcodeHandlerReady;
    private readonly ushort _reserved0;
    private readonly ulong _ud2Address;
    private readonly ulong _reserved1;
    private DisabledDefaultUd2VectorStateRequest(ulong ud2Address, bool enabledByDefault, bool executionRequested, bool executionAttempted, bool ownedSerialReady, bool ownedGdtTssReady, bool ownedIdtReady, bool invalidOpcodeHandlerReady)
    {
        _structureSize = StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _enabledByDefault = enabledByDefault ? (byte)1 : (byte)0;
        _executionRequested = executionRequested ? (byte)1 : (byte)0;
        _executionAttempted = executionAttempted ? (byte)1 : (byte)0;
        _ownedSerialReady = ownedSerialReady ? (byte)1 : (byte)0;
        _ownedGdtTssReady = ownedGdtTssReady ? (byte)1 : (byte)0;
        _ownedIdtReady = ownedIdtReady ? (byte)1 : (byte)0;
        _invalidOpcodeHandlerReady = invalidOpcodeHandlerReady ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _ud2Address = ud2Address;
        _reserved1 = 0;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _enabledByDefault <= 1 && _executionRequested <= 1 && _executionAttempted <= 1 && _ownedSerialReady <= 1 && _ownedGdtTssReady <= 1 && _ownedIdtReady <= 1 && _invalidOpcodeHandlerReady <= 1 && _reserved0 == 0 && _reserved1 == 0;
    public bool EnabledByDefault => _enabledByDefault == 1;
    public bool ExecutionRequested => _executionRequested == 1;
    public bool ExecutionAttempted => _executionAttempted == 1;
    public bool OwnedSerialReady => _ownedSerialReady == 1;
    public bool OwnedGdtTssReady => _ownedGdtTssReady == 1;
    public bool OwnedIdtReady => _ownedIdtReady == 1;
    public bool InvalidOpcodeHandlerReady => _invalidOpcodeHandlerReady == 1;
    public ulong Ud2Address => _ud2Address;

    public static DisabledDefaultUd2VectorStateRequest CreateDisabled(ulong ud2Address, bool callerAttestsOwnedSerialReady, bool callerAttestsOwnedGdtTssReady, bool callerAttestsOwnedIdtReady, bool callerAttestsInvalidOpcodeHandlerReady)
    {
        return new DisabledDefaultUd2VectorStateRequest(ud2Address, DisabledDefaultUd2VectorState.EnabledByDefault, executionRequested: false, executionAttempted: false, callerAttestsOwnedSerialReady, callerAttestsOwnedGdtTssReady, callerAttestsOwnedIdtReady, callerAttestsInvalidOpcodeHandlerReady);
    }

    public static DisabledDefaultUd2VectorStateRequest CreateForValidation(ulong ud2Address, bool enabledByDefault, bool executionRequested, bool executionAttempted, bool callerAttestsOwnedSerialReady, bool callerAttestsOwnedGdtTssReady, bool callerAttestsOwnedIdtReady, bool callerAttestsInvalidOpcodeHandlerReady)
    {
        return new DisabledDefaultUd2VectorStateRequest(ud2Address, enabledByDefault, executionRequested, executionAttempted, callerAttestsOwnedSerialReady, callerAttestsOwnedGdtTssReady, callerAttestsOwnedIdtReady, callerAttestsInvalidOpcodeHandlerReady);
    }
}
