namespace OS.Kernel.Diagnostics;
/// <summary>
/// Allocation-free result for one pre-terminal M02-013 adapter boundary. The
/// caller owns firmware invocation; this result owns the exact accept/reject
/// and cleanup decision shared by the EFI adapter and host fake-firmware tests.
/// </summary>

public readonly struct DisabledDefaultUd2VectorStatePreTerminalResult
{
    private readonly byte _isInitialized;
    private readonly byte _cleanupPlan;
    internal DisabledDefaultUd2VectorStatePreTerminalResult(DisabledDefaultUd2VectorStatePreTerminalStage stage, DisabledDefaultUd2VectorStatePreTerminalStatus status, ulong codeAddress, ulong dataAddress, byte cleanupPlan)
    {
        _isInitialized = 1;
        _cleanupPlan = cleanupPlan;
        Stage = stage;
        Status = status;
        CodeAddress = codeAddress;
        DataAddress = dataAddress;
    }

    public bool IsInitialized => _isInitialized == 1;
    public DisabledDefaultUd2VectorStatePreTerminalStage Stage { get; }
    public DisabledDefaultUd2VectorStatePreTerminalStatus Status { get; }
    public ulong CodeAddress { get; }
    public ulong DataAddress { get; }
    public byte CleanupPlan => IsInitialized ? _cleanupPlan : (byte)0;
    public bool ReleaseData => (CleanupPlan & 1) != 0;
    public bool ReleaseCode => (CleanupPlan & 2) != 0;
    public bool Succeeded => IsInitialized && Status == DisabledDefaultUd2VectorStatePreTerminalStatus.Success;
}
#if !NOSTDLIB_EFI
#endif
