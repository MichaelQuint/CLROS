namespace OS.Boot;

public static unsafe class M02SafeStubPageIntegration
{
    public const ulong PageSize = 4096;
    public const ulong SafeOffset = 0;
    public const ulong Out8Offset = 16;
    public const ulong In8Offset = 32;
    public const uint SafeReturnValue = 0x4f53_4f4b;
    public const ulong ExpectedFnv1a64 = 0x982e_5b7e_3ee5_8e56;
    public const ulong RequiredPublicationEpoch = 7;
    public const ulong ExactProbeInvocationCount = 1;
    public const ulong Fnv1a64OffsetBasis = 0xcbf2_9ce4_8422_2325;
    public const ulong Fnv1a64Prime = 0x0000_0100_0000_01b3UL;
    public const bool PortIoExecuted = false;
    public const bool PrivilegedStubTargeted = false;
    public static OS.Kernel.Diagnostics.ExistingIn8Out8StubPageState CreatePageState()
    {
        return OS.Kernel.Diagnostics.ExistingIn8Out8StubPageState.Create();
    }

    public static M02SafeStubPageIntegrationResult Materialize(byte* destination, ulong modeledDestinationAddress, ulong destinationCapacity, OS.Kernel.Diagnostics.ExistingIn8Out8StubPageState* state, out OS.Kernel.Diagnostics.ExistingIn8Out8StubPageResult materialization)
    {
        materialization = default;
        if (state == null)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.MissingPageState);
        }

        if (!state->IsValid || state->Phase != OS.Kernel.Diagnostics.ExistingIn8Out8StubPagePhase.Uninitialized || state->TransitionVersion != 0)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.InvalidPageState);
        }

        materialization = OS.Kernel.Diagnostics.ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(destination, modeledDestinationAddress, destinationCapacity, SafeOffset, Out8Offset, In8Offset, OS.Kernel.Diagnostics.PortIoStubPlan.Out8, OS.Kernel.Diagnostics.PortIoStubPlan.In8, OS.Kernel.Diagnostics.ExistingIn8Out8StubBytesRWPagesFinalize.ReadWriteProtection, RequiredPublicationEpoch, state, expectedTransitionVersion: 0);
        if (!materialization.Succeeded || materialization.Phase != OS.Kernel.Diagnostics.ExistingIn8Out8StubPagePhase.Materialized || materialization.PublicationEpoch != RequiredPublicationEpoch || materialization.TransitionVersion != 1)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.MaterializationRejected, materialization.Status);
        }

        return M02SafeStubPageIntegrationResult.Success(M02SafeStubPageIntegrationPhase.Materialized, modeledDestinationAddress, transitionVersion: 1, observedReturn: 0, probeInvocationCount: 0);
    }

    public static M02SafeStubPageIntegrationResult ValidateReadback(byte* source, ulong modeledSourceAddress, ulong sourceCapacity, OS.Kernel.Diagnostics.ExistingIn8Out8StubPageResult materialization, OS.Kernel.Diagnostics.ExistingIn8Out8StubPageState* state, out OS.Kernel.Diagnostics.ExistingIn8Out8StubPageResult readback)
    {
        readback = default;
        if (state == null)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.MissingPageState);
        }

        readback = OS.Kernel.Diagnostics.ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(source, modeledSourceAddress, sourceCapacity, materialization, state, expectedTransitionVersion: 1);
        if (!readback.ReadBackVerified || readback.Phase != OS.Kernel.Diagnostics.ExistingIn8Out8StubPagePhase.ReadBackVerified || readback.PublicationEpoch != RequiredPublicationEpoch || readback.TransitionVersion != 2 || !ValidateExactPage(source, modeledSourceAddress, sourceCapacity, out ulong observedFnv1a64) || observedFnv1a64 != ExpectedFnv1a64)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.ReadbackRejected, readback.Status);
        }

        return M02SafeStubPageIntegrationResult.Success(M02SafeStubPageIntegrationPhase.ReadBackVerified, modeledSourceAddress, transitionVersion: 2, observedReturn: 0, probeInvocationCount: 0);
    }

    public static M02SafeStubPageIntegrationResult AuditFinalization(OS.Kernel.Diagnostics.ExistingIn8Out8StubPageResult readback, ulong targetPageAddress, OS.Kernel.Diagnostics.ExistingIn8Out8StubPageState* state, out OS.Kernel.Diagnostics.ExistingIn8Out8StubPageResult finalization)
    {
        finalization = default;
        if (state == null)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.MissingPageState);
        }

        finalization = OS.Kernel.Diagnostics.ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(readback, targetPageAddress, OS.Kernel.Diagnostics.ExistingIn8Out8StubBytesRWPagesFinalize.ReadWriteProtection, OS.Kernel.Diagnostics.ExistingIn8Out8StubBytesRWPagesFinalize.ReadExecuteProtection, executionAttemptedBeforeFinalization: false, state, expectedTransitionVersion: 2);
        if (!finalization.FinalizationAudited || finalization.Phase != OS.Kernel.Diagnostics.ExistingIn8Out8StubPagePhase.FinalizationAudited || finalization.PublicationEpoch != RequiredPublicationEpoch || finalization.TransitionVersion != 3 || finalization.FinalizationCount != 1 || finalization.LiveProtection || finalization.LiveExecution || finalization.FinalProtection != OS.Kernel.Diagnostics.ExistingIn8Out8StubBytesRWPagesFinalize.ReadExecuteProtection)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.FinalizationRejected, finalization.Status);
        }

        return M02SafeStubPageIntegrationResult.Success(M02SafeStubPageIntegrationPhase.FinalizationAudited, targetPageAddress, transitionVersion: 3, observedReturn: 0, probeInvocationCount: 0);
    }

    public static M02SafeStubPageIntegrationResult AuditExecution(byte* probePage, ulong modeledProbePageAddress, ulong probePageCapacity, OS.Kernel.Diagnostics.ExistingIn8Out8StubPageResult finalization, M01OwnedCr3CompositeTransitionRuntimeState* runtimeState, M01OwnedCr3CompositeCallGateResult* gateAudit)
    {
        if (!finalization.FinalizationAudited || finalization.ModeledPageAddress != modeledProbePageAddress || finalization.PublicationEpoch != RequiredPublicationEpoch || finalization.TransitionVersion != 3)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.FinalizationRejected, finalization.Status);
        }

        if (!ValidateExactPage(probePage, modeledProbePageAddress, probePageCapacity, out ulong observedFnv1a64) || observedFnv1a64 != ExpectedFnv1a64)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.InvalidPageImage);
        }

        if (runtimeState == null)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.MissingRuntimeState);
        }

        if (gateAudit == null)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.MissingGateAudit);
        }

        if (!gateAudit->IsObservedAudit)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.GateAuditRejected);
        }

        if (runtimeState->ProbePageStart != modeledProbePageAddress || modeledProbePageAddress > ~0UL - PageSize || runtimeState->ProbePageEndExclusive != modeledProbePageAddress + PageSize)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.ProbePageMismatch);
        }

        if (runtimeState->ProbeEntry != modeledProbePageAddress + SafeOffset || runtimeState->InvlpgTarget != runtimeState->ProbeEntry)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.SafeEntryMismatch);
        }

        if (runtimeState->ExpectedProbeReturn != SafeReturnValue || runtimeState->ProbeReturn != SafeReturnValue || !runtimeState->ProbeReturned)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.SafeReturnMismatch);
        }

        if (runtimeState->ProbeInvocationCount != ExactProbeInvocationCount || runtimeState->InvlpgCount != M01OwnedCr3CompositeTransition.ExactInvlpgCount)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.InvocationCountMismatch);
        }

        if (runtimeState->ProhibitedFirmwareCallbackCount != 0)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.FirmwareCallbackObserved);
        }

        if (runtimeState->OriginalCr3Raw == 0 || runtimeState->FirmwareCr3Readback != runtimeState->OriginalCr3Raw || runtimeState->RestoredCr4Readback != runtimeState->OriginalCr4Raw || runtimeState->OriginalRflags == 0 || runtimeState->RestoredRflagsIntent != runtimeState->OriginalRflags || runtimeState->ExitRsp != runtimeState->EntryRsp)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.RestorationMismatch);
        }

        if (!runtimeState->OwnedRootWasLoaded || !runtimeState->MutationCommitted || runtimeState->CommittedProbePteReadback != runtimeState->FinalProbePteRaw || (runtimeState->CommittedProbePteReadback & (M01OwnedCr3CompositeTransition.PageWritableBit | M01OwnedCr3CompositeTransition.PageNoExecuteBit)) != 0)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.ProtectionAuditMismatch);
        }

        if (runtimeState->RuntimeStatus != M01OwnedCr3CompositeTransitionRuntimeStatus.Success || runtimeState->Checkpoint != M01OwnedCr3CompositeTransitionCheckpoint.ReturnPrepared || runtimeState->ProgressCheckpoint != M01OwnedCr3CompositeTransitionCheckpoint.PageZeroAbsent)
        {
            return M02SafeStubPageIntegrationResult.Fail(M02SafeStubPageIntegrationStatus.RuntimeTerminalMismatch);
        }

        return M02SafeStubPageIntegrationResult.Success(M02SafeStubPageIntegrationPhase.ExecutionAudited, modeledProbePageAddress, transitionVersion: 3, observedReturn: (uint)runtimeState->ProbeReturn, probeInvocationCount: runtimeState->ProbeInvocationCount);
    }

    public static bool ValidateExactPage(byte* source, ulong modeledSourceAddress, ulong sourceCapacity, out ulong observedFnv1a64)
    {
        observedFnv1a64 = 0;
        if (source == null || sourceCapacity != PageSize || !IsValidPageAddress(modeledSourceAddress) || (ulong)source != modeledSourceAddress)
        {
            return false;
        }

        byte* out8Bytes = stackalloc byte[OS.Kernel.Diagnostics.PortIoStubPlan.Out8RequiredByteLength];
        byte* in8Bytes = stackalloc byte[OS.Kernel.Diagnostics.PortIoStubPlan.In8RequiredByteLength];
        OS.Kernel.Diagnostics.PortIoStubPlanResult out8 = OS.Kernel.Diagnostics.PortIoStubPlan.Out8.Emit(out8Bytes, OS.Kernel.Diagnostics.PortIoStubPlan.Out8RequiredByteLength);
        OS.Kernel.Diagnostics.PortIoStubPlanResult in8 = OS.Kernel.Diagnostics.PortIoStubPlan.In8.Emit(in8Bytes, OS.Kernel.Diagnostics.PortIoStubPlan.In8RequiredByteLength);
        if (!out8.Succeeded || !in8.Succeeded)
        {
            return false;
        }

        ulong hash = Fnv1a64OffsetBasis;
        for (ulong index = 0; index < PageSize; index++)
        {
            byte expected = 0;
            if (index < 6)
            {
                expected = GetSafeByte(index);
            }
            else if (index >= Out8Offset && index < Out8Offset + (ulong)OS.Kernel.Diagnostics.PortIoStubPlan.Out8RequiredByteLength)
            {
                expected = out8Bytes[index - Out8Offset];
            }
            else if (index >= In8Offset && index < In8Offset + (ulong)OS.Kernel.Diagnostics.PortIoStubPlan.In8RequiredByteLength)
            {
                expected = in8Bytes[index - In8Offset];
            }

            byte observed = source[index];
            hash = unchecked((hash ^ observed) * Fnv1a64Prime);
            if (observed != expected)
            {
                return false;
            }
        }

        observedFnv1a64 = hash;
        return hash == ExpectedFnv1a64;
    }

    internal static bool IsValidPageAddress(ulong address)
    {
        return address != 0 && (address & (PageSize - 1)) == 0 && address <= ~0UL - PageSize;
    }

    private static byte GetSafeByte(ulong index)
    {
        switch (index)
        {
            case 0:
                return 0xb8;
            case 1:
                return (byte)(SafeReturnValue & 0xffU);
            case 2:
                return (byte)((SafeReturnValue >> 8) & 0xffU);
            case 3:
                return (byte)((SafeReturnValue >> 16) & 0xffU);
            case 4:
                return (byte)((SafeReturnValue >> 24) & 0xffU);
            case 5:
                return 0xc3;
            default:
                return 0;
        }
    }
}
