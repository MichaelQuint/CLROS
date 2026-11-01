namespace OS.Kernel.Diagnostics;
/// <summary>
/// Versioned, unmanaged caller input for planning only. Diagnostics fields are
/// caller attestations; they are not observations of loaded guest state.
/// </summary>

public readonly struct NullPageFaultProbeRequest
{
    public const ulong StructureSize = 144;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _enabledByDefault;
    private readonly byte _ownedDiagnosticsReady;
    private readonly byte _pageFaultHandlerReady;
    private readonly byte _executionRequested;
    private readonly byte _executionAttempted;
    private readonly uint _reserved;
    private readonly OS.Kernel.Memory.PageZeroMappingPolicyResult _pageZeroPolicy;
    private NullPageFaultProbeRequest(OS.Kernel.Memory.PageZeroMappingPolicyResult pageZeroPolicy, bool enabledByDefault, bool ownedDiagnosticsReady, bool pageFaultHandlerReady, bool executionRequested, bool executionAttempted)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _enabledByDefault = enabledByDefault ? (byte)1 : (byte)0;
        _ownedDiagnosticsReady = ownedDiagnosticsReady ? (byte)1 : (byte)0;
        _pageFaultHandlerReady = pageFaultHandlerReady ? (byte)1 : (byte)0;
        _executionRequested = executionRequested ? (byte)1 : (byte)0;
        _executionAttempted = executionAttempted ? (byte)1 : (byte)0;
        _reserved = 0;
        _pageZeroPolicy = pageZeroPolicy;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _enabledByDefault <= 1 && _ownedDiagnosticsReady <= 1 && _pageFaultHandlerReady <= 1 && _executionRequested <= 1 && _executionAttempted <= 1 && _reserved == 0;
    public bool EnabledByDefault => _enabledByDefault == 1;
    public bool OwnedDiagnosticsReady => _ownedDiagnosticsReady == 1;
    public bool PageFaultHandlerReady => _pageFaultHandlerReady == 1;
    public bool ExecutionRequested => _executionRequested == 1;
    public bool ExecutionAttempted => _executionAttempted == 1;
    public OS.Kernel.Memory.PageZeroMappingPolicyResult PageZeroPolicy => _pageZeroPolicy;

    public static NullPageFaultProbeRequest CreateDisabled(OS.Kernel.Memory.PageZeroMappingPolicyResult pageZeroPolicy, bool callerAttestsOwnedDiagnosticsReady, bool callerAttestsPageFaultHandlerReady)
    {
        return new NullPageFaultProbeRequest(pageZeroPolicy, enabledByDefault: false, callerAttestsOwnedDiagnosticsReady, callerAttestsPageFaultHandlerReady, executionRequested: false, executionAttempted: false);
    }

    public static NullPageFaultProbeRequest CreateForValidation(OS.Kernel.Memory.PageZeroMappingPolicyResult pageZeroPolicy, bool enabledByDefault, bool callerAttestsOwnedDiagnosticsReady, bool callerAttestsPageFaultHandlerReady, bool executionRequested, bool executionAttempted)
    {
        return new NullPageFaultProbeRequest(pageZeroPolicy, enabledByDefault, callerAttestsOwnedDiagnosticsReady, callerAttestsPageFaultHandlerReady, executionRequested, executionAttempted);
    }
}
