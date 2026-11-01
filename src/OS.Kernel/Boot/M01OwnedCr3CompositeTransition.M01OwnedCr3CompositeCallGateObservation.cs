namespace OS.Boot;
#if !NOSTDLIB_EFI
#endif
/// <summary>
/// Caller-owned input/output POD used by the emitted Windows-x64 call gate.
/// The fixed canary is outside the composite runtime-state mutation.
/// </summary>

public struct M01OwnedCr3CompositeCallGateObservation
{
    public const uint StructureSize = 160;
    public const ushort StructureVersion = 1;
    public const uint CompletionSignature = 0x4b47_534f;
    internal uint HeaderSize;
    internal ushort HeaderVersion;
    internal byte IsInitializedValue;
    internal M01OwnedCr3CompositeCallGateObservationLifecycle LifecycleValue;
    internal ulong ExpectedSelfAddressValue;
    internal ulong ExpectedCodeBaseValue;
    internal ulong ExpectedCompositeEntryAddressValue;
    internal ulong ExpectedGateEntryAddressValue;
    internal ulong ExpectedReturnSiteAddressValue;
    internal ulong ExpectedOriginalRflagsValue;
    internal ulong ExpectedFirmwareCr3RawValue;
    internal ulong ExpectedFirmwareCr4RawValue;
    internal ulong ExpectedBoundaryCanaryValue;
    internal uint ExpectedCompositeReturnValueValue;
    internal byte ExpectedInterruptEnabledValue;
    internal byte ExpectedPgeWasSetValue;
    internal M01OwnedCr3CompositeCallGateInterruptPolicy InterruptPolicyValue;
    internal M01OwnedCr3CompositeCallGatePgePolicy PgePolicyValue;
    internal ulong BoundaryCanaryValue;
    internal ulong CanaryBeforeValue;
    internal ulong PostReturnRflagsValue;
    internal ulong PostReturnCr3RawValue;
    internal ulong PostReturnCr4RawValue;
    internal ulong CanaryAfterValue;
    internal uint CompositeReturnValueValue;
    internal byte CallCountValue;
    internal byte PostReturnObservationCountValue;
    internal ushort ReservedOutput0;
    internal uint CompletionSignatureValue;
    internal uint ReservedOutput1;
    internal ulong ReservedOutput2;
    public uint Size => HeaderSize;
    public ushort Version => HeaderVersion;
    public bool IsInitialized => IsInitializedValue == 1;
    public M01OwnedCr3CompositeCallGateObservationLifecycle Lifecycle => LifecycleValue;
    public bool IsArmed => HasCanonicalHeader && LifecycleValue == M01OwnedCr3CompositeCallGateObservationLifecycle.Armed && OutputsAreZero;
    public bool IsCompleted => HasCanonicalHeader && LifecycleValue == M01OwnedCr3CompositeCallGateObservationLifecycle.Completed && CompletionSignatureValue == CompletionSignature;
    public ulong ExpectedSelfAddress => ExpectedSelfAddressValue;
    public ulong ExpectedCodeBase => ExpectedCodeBaseValue;
    public ulong ExpectedCompositeEntryAddress => ExpectedCompositeEntryAddressValue;
    public ulong ExpectedGateEntryAddress => ExpectedGateEntryAddressValue;
    public ulong ExpectedReturnSiteAddress => ExpectedReturnSiteAddressValue;
    public ulong ExpectedOriginalRflags => ExpectedOriginalRflagsValue;
    public ulong ExpectedFirmwareCr3Raw => ExpectedFirmwareCr3RawValue;
    public ulong ExpectedFirmwareCr4Raw => ExpectedFirmwareCr4RawValue;
    public ulong ExpectedBoundaryCanary => ExpectedBoundaryCanaryValue;
    public uint ExpectedCompositeReturnValue => ExpectedCompositeReturnValueValue;
    public bool ExpectedInterruptEnabled => ExpectedInterruptEnabledValue == 1;
    public bool ExpectedPgeWasSet => ExpectedPgeWasSetValue == 1;
    public M01OwnedCr3CompositeCallGateInterruptPolicy InterruptPolicy => InterruptPolicyValue;
    public M01OwnedCr3CompositeCallGatePgePolicy PgePolicy => PgePolicyValue;
    public ulong BoundaryCanary => BoundaryCanaryValue;
    public ulong CanaryBefore => CanaryBeforeValue;
    public ulong PostReturnRflags => PostReturnRflagsValue;
    public ulong PostReturnCr3Raw => PostReturnCr3RawValue;
    public ulong PostReturnCr4Raw => PostReturnCr4RawValue;
    public ulong CanaryAfter => CanaryAfterValue;
    public uint CompositeReturnValue => CompositeReturnValueValue;
    public uint CallCount => CallCountValue;
    public uint PostReturnObservationCount => PostReturnObservationCountValue;
    public uint ObservedCompletionSignature => CompletionSignatureValue;
    internal bool HasCanonicalHeader => HeaderSize == StructureSize && HeaderVersion == StructureVersion && IsInitializedValue == 1 && ExpectedSelfAddressValue != 0 && ExpectedCodeBaseValue != 0 && ExpectedCompositeEntryAddressValue != 0 && ExpectedGateEntryAddressValue != 0 && ExpectedReturnSiteAddressValue != 0 && ExpectedOriginalRflagsValue != 0 && ExpectedFirmwareCr3RawValue != 0 && ExpectedFirmwareCr4RawValue != 0 && ExpectedBoundaryCanaryValue != 0 && ExpectedCompositeReturnValueValue == (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success && ExpectedInterruptEnabledValue <= 1 && ExpectedPgeWasSetValue <= 1 && InterruptPolicyValue == M01OwnedCr3CompositeCallGateInterruptPolicy.PreserveEntryState && PgePolicyValue == M01OwnedCr3CompositeCallGatePgePolicy.ClearDuringOwnedIntervalAndRestoreExact && BoundaryCanaryValue == ExpectedBoundaryCanaryValue && ReservedOutput0 == 0 && ReservedOutput1 == 0 && ReservedOutput2 == 0;
    internal bool OutputsAreZero => CanaryBeforeValue == 0 && PostReturnRflagsValue == 0 && PostReturnCr3RawValue == 0 && PostReturnCr4RawValue == 0 && CanaryAfterValue == 0 && CompositeReturnValueValue == 0 && CallCountValue == 0 && PostReturnObservationCountValue == 0 && CompletionSignatureValue == 0 && ReservedOutput0 == 0 && ReservedOutput1 == 0 && ReservedOutput2 == 0;

    public static M01OwnedCr3CompositeCallGateObservation CreateObserved(M01OwnedCr3CompositeCallGateObservation initializedObservation, M01OwnedCr3CompositeCallGateObservationLifecycle lifecycle, ulong boundaryCanary, ulong canaryBefore, ulong postReturnRflags, ulong postReturnCr3Raw, ulong postReturnCr4Raw, ulong canaryAfter, uint compositeReturnValue, uint callCount, uint postReturnObservationCount, uint completionSignature, ulong reservedOutput0 = 0, uint reservedOutput1 = 0, ulong reservedOutput2 = 0)
    {
        initializedObservation.LifecycleValue = lifecycle;
        initializedObservation.BoundaryCanaryValue = boundaryCanary;
        initializedObservation.CanaryBeforeValue = canaryBefore;
        initializedObservation.PostReturnRflagsValue = postReturnRflags;
        initializedObservation.PostReturnCr3RawValue = postReturnCr3Raw;
        initializedObservation.PostReturnCr4RawValue = postReturnCr4Raw;
        initializedObservation.CanaryAfterValue = canaryAfter;
        initializedObservation.CompositeReturnValueValue = compositeReturnValue;
        initializedObservation.CallCountValue = (byte)callCount;
        initializedObservation.PostReturnObservationCountValue = (byte)postReturnObservationCount;
        initializedObservation.CompletionSignatureValue = completionSignature;
        initializedObservation.ReservedOutput0 = (ushort)reservedOutput0;
        initializedObservation.ReservedOutput1 = callCount > 255U || postReturnObservationCount > 255U || reservedOutput0 > 65535UL ? 1U : reservedOutput1;
        initializedObservation.ReservedOutput2 = reservedOutput2;
        return initializedObservation;
    }
}
