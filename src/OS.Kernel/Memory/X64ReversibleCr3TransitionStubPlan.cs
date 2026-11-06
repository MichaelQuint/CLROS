namespace OS.Kernel.Memory;
/// <summary>
/// Emits and validates one relocation-free, callback-free x64 transition
/// stub. Host methods only materialize or compare bytes and audit caller-owned
/// POD observations; they never invoke the privileged instructions.
/// </summary>

public static unsafe partial class X64ReversibleCr3TransitionStubPlan
{
    public const ulong EntryAlignment = 16;
    public const ulong RequiredPointerAlignment = 16;
    public const ulong MaximumByteCapacity = 4096;
    public const ulong LowCanonicalExclusiveCeiling = 0x0000_8000_0000_0000UL;
    public const ulong RequiredByteLength = 1981;
    public const ulong AuditedInstructionCount = 485;
    public const ulong ExternalPatchCount = 0;
    public const bool HasExternalRelocations = false;
    public const bool UsesWindowsX64Abi = true;
    public const bool InvokesCallbacks = false;
    public const bool TouchesNonvolatileGeneralRegisters = false;
    public const bool TouchesXmm6ThroughXmm15 = false;
    public const bool PreservesStackPointer = true;
    public const bool PreservesShadowSpace = true;
    public const bool SupportsPcide = false;
    public const bool SupportsFiveLevelPaging = false;
    public const ulong MaximumSimultaneousPushBytes = 8;
    public const ulong PreservedCallerShadowSpaceBytes = 32;
    public const ulong Cr3PermittedLowBitsMask = 0x18;
    public const ulong Cr3ArchitecturalBaseMask = 0x000f_ffff_ffff_f000UL;
    public const ulong Cr4PageGlobalEnableBit = 1UL << 7;
    public const ulong Cr4FiveLevelPagingBit = 1UL << 12;
    public const ulong Cr4PcideBit = 1UL << 17;
    public const ulong RflagsInterruptEnableBit = 1UL << 9;
    public const ulong RflagsArchitecturalReservedOneBit = 1UL << 1;
    public const uint StateStructureSizeOffset = 0;
    public const uint StateStructureVersionOffset = 4;
    public const uint StateIsInitializedOffset = 6;
    public const uint StateReserved0Offset = 7;
    public const uint StateOwnedCr3RawOffset = 8;
    public const uint StateStackStartOffset = 16;
    public const uint StateStackEndExclusiveOffset = 24;
    public const uint StateCanaryAddressOffset = 32;
    public const uint StateExpectedCanaryOffset = 40;
    public const uint StateReplacementCanaryOffset = 48;
    public const uint StateOriginalRflagsOffset = 56;
    public const uint StateOriginalCr3RawOffset = 64;
    public const uint StateOriginalCr4RawOffset = 72;
    public const uint StateClearedCr4ReadbackOffset = 80;
    public const uint StateOwnedCr3ReadbackOffset = 88;
    public const uint StateCanaryBeforeOffset = 96;
    public const uint StateCanaryAfterOffset = 104;
    public const uint StateFirmwareCr3ReadbackOffset = 112;
    public const uint StateRestoredCr4ReadbackOffset = 120;
    public const uint StateCliRflagsReadbackOffset = 128;
    public const uint StateRestoredRflagsReadbackOffset = 136;
    public const uint StateEntryRspOffset = 144;
    public const uint StateExitRspOffset = 152;
    public const uint StatePostRestoreCanaryReadbackOffset = 160;
    public const uint StateFirmwareCallbackCountOffset = 168;
    public const uint StateRuntimeStatusOffset = 172;
    public const uint StateCheckpointOffset = 173;
    public const uint StateReserved1Offset = 174;
    public const uint StateReserved2Offset = 176;
    public static X64ReversibleCr3TransitionStubPlanResult Emit(byte* destination, ulong destinationCapacity, ulong entryAddress)
    {
        X64ReversibleCr3TransitionStubPlanStatus preflight = ValidateWriteBoundary(destination, destinationCapacity, entryAddress);
        if (preflight != X64ReversibleCr3TransitionStubPlanStatus.Success)
        {
            return Fail(preflight, entryAddress, 0, destinationCapacity);
        }

        StubBuilder measurement = new StubBuilder(null, MaximumByteCapacity, StubBuilderMode.Measure);
        BuildPlan(ref measurement);
        if (!measurement.Succeeded || measurement.Position != RequiredByteLength || measurement.InstructionCount != AuditedInstructionCount)
        {
            return new X64ReversibleCr3TransitionStubPlanResult(X64ReversibleCr3TransitionStubPlanStatus.InternalEncodingFailure, 0, RequiredByteLength, entryAddress, measurement.InvalidByteIndex, measurement.Position, destinationCapacity, measurement.InstructionCount);
        }

        StubBuilder writer = new StubBuilder(destination, destinationCapacity, StubBuilderMode.Emit);
        BuildPlan(ref writer);
        if (!writer.Succeeded || writer.Position != RequiredByteLength || writer.InstructionCount != AuditedInstructionCount)
        {
            return new X64ReversibleCr3TransitionStubPlanResult(X64ReversibleCr3TransitionStubPlanStatus.InternalEncodingFailure, writer.Position, RequiredByteLength, entryAddress, writer.InvalidByteIndex, writer.Position, destinationCapacity, writer.InstructionCount);
        }

        return new X64ReversibleCr3TransitionStubPlanResult(X64ReversibleCr3TransitionStubPlanStatus.Success, RequiredByteLength, RequiredByteLength, entryAddress, X64ReversibleCr3TransitionStubPlanResult.NoIndex, RequiredByteLength, destinationCapacity, writer.InstructionCount);
    }

    public static X64ReversibleCr3TransitionStubPlanResult Validate(byte* source, ulong sourceCount, ulong sourceCapacity, ulong entryAddress)
    {
        if (sourceCount == 0)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.InvalidSourceCount, entryAddress, sourceCount, sourceCapacity);
        }

        if (sourceCount < RequiredByteLength)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.TruncatedBytePlan, entryAddress, sourceCount, sourceCapacity);
        }

        if (sourceCount > RequiredByteLength)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.OversizedBytePlan, entryAddress, sourceCount, sourceCapacity);
        }

        if (sourceCapacity < sourceCount)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.SourceCapacityTooSmall, entryAddress, sourceCount, sourceCapacity);
        }

        if (sourceCapacity > MaximumByteCapacity)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.CapacityExceedsLimit, entryAddress, sourceCount, sourceCapacity);
        }

        if (source == null)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.MissingSource, entryAddress, sourceCount, sourceCapacity);
        }

        if ((((ulong)source) & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.MisalignedSource, entryAddress, sourceCount, sourceCapacity);
        }

        if (!IsValidEntryAddress(entryAddress))
        {
            return Fail(entryAddress == 0 ? X64ReversibleCr3TransitionStubPlanStatus.InvalidEntryAddress : X64ReversibleCr3TransitionStubPlanStatus.MisalignedEntryAddress, entryAddress, sourceCount, sourceCapacity);
        }

        ulong sourceAddress = (ulong)source;
        if (sourceAddress + sourceCapacity < sourceAddress)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.SourceSpanOverflow, entryAddress, sourceCount, sourceCapacity);
        }

        if (entryAddress + RequiredByteLength < entryAddress)
        {
            return Fail(X64ReversibleCr3TransitionStubPlanStatus.EntryAddressOverflow, entryAddress, sourceCount, sourceCapacity);
        }

        StubBuilder reader = new StubBuilder(source, sourceCount, StubBuilderMode.Validate);
        BuildPlan(ref reader);
        if (!reader.Succeeded)
        {
            return new X64ReversibleCr3TransitionStubPlanResult(X64ReversibleCr3TransitionStubPlanStatus.ByteMismatch, 0, RequiredByteLength, entryAddress, reader.InvalidByteIndex, sourceCount, sourceCapacity, reader.InstructionCount);
        }

        if (reader.Position != RequiredByteLength || reader.InstructionCount != AuditedInstructionCount)
        {
            return new X64ReversibleCr3TransitionStubPlanResult(X64ReversibleCr3TransitionStubPlanStatus.InternalEncodingFailure, 0, RequiredByteLength, entryAddress, X64ReversibleCr3TransitionStubPlanResult.NoIndex, sourceCount, sourceCapacity, reader.InstructionCount);
        }

        return new X64ReversibleCr3TransitionStubPlanResult(X64ReversibleCr3TransitionStubPlanStatus.Success, RequiredByteLength, RequiredByteLength, entryAddress, X64ReversibleCr3TransitionStubPlanResult.NoIndex, sourceCount, sourceCapacity, reader.InstructionCount);
    }

    public static X64ReversibleCr3TransitionEvidenceResult Evaluate(X64ReversibleCr3TransitionRuntimeState* state, X64ReversibleCr3TransitionTerminalKind terminalKind)
    {
        return Evaluate(state, (ulong)state, terminalKind);
    }

    public static X64ReversibleCr3TransitionEvidenceResult Evaluate(X64ReversibleCr3TransitionRuntimeState* state, ulong runtimeStateAddress, X64ReversibleCr3TransitionTerminalKind terminalKind)
    {
        return EvaluateCore(state, runtimeStateAddress, terminalKind).WithRuntimeStateAddress(runtimeStateAddress);
    }

    private static X64ReversibleCr3TransitionEvidenceResult EvaluateCore(X64ReversibleCr3TransitionRuntimeState* state, ulong runtimeStateAddress, X64ReversibleCr3TransitionTerminalKind terminalKind)
    {
        if (terminalKind == X64ReversibleCr3TransitionTerminalKind.Timeout)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.Timeout, terminalKind, state, 0, 0);
        }

        if (terminalKind == X64ReversibleCr3TransitionTerminalKind.Reset)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.Reset, terminalKind, state, 0, 0);
        }

        if (terminalKind == X64ReversibleCr3TransitionTerminalKind.TripleFault)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.TripleFault, terminalKind, state, 0, 0);
        }

        if (terminalKind != X64ReversibleCr3TransitionTerminalKind.Returned)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidTerminalKind, terminalKind, state, 0, 0);
        }

        if (state == null)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.MissingState, terminalKind, state, 0, 0);
        }

        ulong observationStateAddress = (ulong)state;
        if (!IsValidRuntimeStateAddress(observationStateAddress))
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidRuntimeState, terminalKind, state, LowCanonicalExclusiveCeiling, observationStateAddress);
        }

        if (!IsValidRuntimeStateAddress(runtimeStateAddress))
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidRuntimeState, terminalKind, state, LowCanonicalExclusiveCeiling, runtimeStateAddress);
        }

        X64ReversibleCr3TransitionRuntimeState snapshot = *state;
        if (!snapshot.IsInitialized)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidRuntimeState, terminalKind, state, 1, 0);
        }

        if (snapshot.FirmwareCallbackCount != 0)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.FirmwareCallbackObserved, terminalKind, state, 0, snapshot.FirmwareCallbackCount);
        }

        if (!IsValidOwnedCr3(snapshot.OwnedCr3Raw))
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidOwnedCr3, terminalKind, state, 0, snapshot.OwnedCr3Raw);
        }

        if (!IsValidStackRange(snapshot.StackStart, snapshot.StackEndExclusive))
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidStackRange, terminalKind, state, snapshot.StackStart, snapshot.StackEndExclusive);
        }

        if (RangesOverlap(runtimeStateAddress, X64ReversibleCr3TransitionRuntimeState.StructureSize, snapshot.StackStart, snapshot.StackEndExclusive - snapshot.StackStart))
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidRuntimeState, terminalKind, state, snapshot.StackStart, snapshot.StackEndExclusive);
        }

        if (!IsValidCanary(snapshot) || !IsValidCanaryPlacement(runtimeStateAddress, snapshot.CanaryAddress))
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary, terminalKind, state, snapshot.ExpectedCanary, snapshot.ReplacementCanary);
        }

        if (RangesOverlap(snapshot.CanaryAddress, sizeof(ulong), snapshot.StackStart, snapshot.StackEndExclusive - snapshot.StackStart))
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary, terminalKind, state, snapshot.StackStart, snapshot.StackEndExclusive);
        }

        if (snapshot.RuntimeStatus == X64ReversibleCr3TransitionRuntimeStatus.Uninitialized)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.DefaultRuntimeResult, terminalKind, state, (ulong)X64ReversibleCr3TransitionRuntimeStatus.Success, 0);
        }

        if (snapshot.RuntimeStatus != X64ReversibleCr3TransitionRuntimeStatus.Success)
        {
            return Evidence(TranslateRuntimeFailure(snapshot.RuntimeStatus), terminalKind, state, (ulong)X64ReversibleCr3TransitionRuntimeStatus.Success, (ulong)snapshot.RuntimeStatus);
        }

        if ((snapshot.OriginalRflags & RflagsArchitecturalReservedOneBit) != RflagsArchitecturalReservedOneBit)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InterruptStateMismatch, terminalKind, state, RflagsArchitecturalReservedOneBit, snapshot.OriginalRflags & RflagsArchitecturalReservedOneBit);
        }

        if ((snapshot.OriginalCr4Raw & Cr4FiveLevelPagingBit) != 0)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.La57NotSupported, terminalKind, state, 0, snapshot.OriginalCr4Raw);
        }

        if ((snapshot.OriginalCr4Raw & Cr4PcideBit) != 0)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.PcideNotSupported, terminalKind, state, 0, snapshot.OriginalCr4Raw);
        }

        if (!IsValidFirmwareRawCr3(snapshot.OriginalCr3Raw))
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidFirmwareCr3, terminalKind, state, 0, snapshot.OriginalCr3Raw);
        }

        if ((snapshot.OriginalCr3Raw & Cr3ArchitecturalBaseMask) == snapshot.OwnedCr3Raw)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InvalidOwnedCr3, terminalKind, state, snapshot.OwnedCr3Raw, snapshot.OriginalCr3Raw);
        }

        ulong expectedClearedCr4 = snapshot.OriginalCr4Raw & ~Cr4PageGlobalEnableBit;
        if (snapshot.ClearedCr4Readback != expectedClearedCr4)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.PgeClearReadbackMismatch, terminalKind, state, expectedClearedCr4, snapshot.ClearedCr4Readback);
        }

        if (snapshot.OwnedCr3Readback != snapshot.OwnedCr3Raw)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.OwnedCr3ReadbackMismatch, terminalKind, state, snapshot.OwnedCr3Raw, snapshot.OwnedCr3Readback);
        }

        if (snapshot.FirmwareCr3Readback != snapshot.OriginalCr3Raw)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.RawCr3BitLoss, terminalKind, state, snapshot.OriginalCr3Raw, snapshot.FirmwareCr3Readback);
        }

        if (snapshot.RestoredCr4Readback != snapshot.OriginalCr4Raw)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.RawCr4BitLoss, terminalKind, state, snapshot.OriginalCr4Raw, snapshot.RestoredCr4Readback);
        }

        ulong cliInvariantMask = RflagsInterruptEnableBit | RflagsArchitecturalReservedOneBit;
        ulong observedCliInvariants = snapshot.CliRflagsReadback & cliInvariantMask;
        if (observedCliInvariants != RflagsArchitecturalReservedOneBit)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.InterruptStateMismatch, terminalKind, state, RflagsArchitecturalReservedOneBit, observedCliInvariants);
        }

        if (snapshot.RestoredRflagsReadback != snapshot.OriginalRflags)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.RflagsRestoreReadbackMismatch, terminalKind, state, snapshot.OriginalRflags, snapshot.RestoredRflagsReadback);
        }

        if (snapshot.CanaryBefore != snapshot.ExpectedCanary)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.CanaryBeforeMismatch, terminalKind, state, snapshot.ExpectedCanary, snapshot.CanaryBefore);
        }

        if (snapshot.CanaryAfter != snapshot.ReplacementCanary)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.CanaryAfterMismatch, terminalKind, state, snapshot.ReplacementCanary, snapshot.CanaryAfter);
        }

        if (snapshot.PostRestoreCanaryReadback != snapshot.ReplacementCanary)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.PostRestoreCanaryMismatch, terminalKind, state, snapshot.ReplacementCanary, snapshot.PostRestoreCanaryReadback);
        }

        if (snapshot.EntryRsp != snapshot.ExitRsp || snapshot.EntryRsp < snapshot.StackStart + MaximumSimultaneousPushBytes || snapshot.EntryRsp >= snapshot.StackEndExclusive || snapshot.ExitRsp < snapshot.StackStart || snapshot.ExitRsp >= snapshot.StackEndExclusive || (snapshot.EntryRsp & 0xf) != 8)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.RspEscape, terminalKind, state, snapshot.EntryRsp, snapshot.ExitRsp);
        }

        if (snapshot.Checkpoint != X64ReversibleCr3TransitionCheckpoint.Returned)
        {
            return Evidence(X64ReversibleCr3TransitionStubPlanStatus.CheckpointMismatch, terminalKind, state, (ulong)X64ReversibleCr3TransitionCheckpoint.Returned, (ulong)snapshot.Checkpoint);
        }

        return Evidence(X64ReversibleCr3TransitionStubPlanStatus.Success, terminalKind, state, 0, 0);
    }

    public static bool IsValidOwnedCr3(ulong rawCr3)
    {
        return rawCr3 != 0 && (rawCr3 & 0xfffUL) == 0 && (rawCr3 & ~Cr3ArchitecturalBaseMask) == 0;
    }

    public static bool IsValidFirmwareRawCr3(ulong rawCr3)
    {
        ulong baseAddress = rawCr3 & Cr3ArchitecturalBaseMask;
        ulong unsupportedLowBits = rawCr3 & (0xfffUL & ~Cr3PermittedLowBitsMask);
        ulong unsupportedHighBits = rawCr3 & ~(Cr3ArchitecturalBaseMask | Cr3PermittedLowBitsMask);
        return baseAddress != 0 && unsupportedLowBits == 0 && unsupportedHighBits == 0;
    }

    public static bool IsValidRuntimeStateAddress(ulong runtimeStateAddress)
    {
        return runtimeStateAddress != 0 && (runtimeStateAddress & 7) == 0 && IsLowCanonicalRange(runtimeStateAddress, X64ReversibleCr3TransitionRuntimeState.StructureSize);
    }

    public static bool IsValidRuntimeStorageLayout(ulong runtimeStateAddress, ulong canaryAddress, ulong stackStart, ulong stackEndExclusive)
    {
        if (!IsValidRuntimeStateAddress(runtimeStateAddress) || !IsValidStackRange(stackStart, stackEndExclusive) || !IsValidCanaryPlacement(runtimeStateAddress, canaryAddress))
        {
            return false;
        }

        ulong stackLength = stackEndExclusive - stackStart;
        return !RangesOverlap(runtimeStateAddress, X64ReversibleCr3TransitionRuntimeState.StructureSize, stackStart, stackLength) && !RangesOverlap(canaryAddress, sizeof(ulong), stackStart, stackLength);
    }

    private static void BuildPlan(ref StubBuilder writer)
    {
        ulong jumpToMain = EmitJump(ref writer);
        ulong cleanupRestoreCr3 = writer.Position;
        EmitMovRegFromState(ref writer, Register.Rax, StateOriginalCr3RawOffset);
        EmitMovCr3FromRax(ref writer);
        EmitMovRaxFromCr3(ref writer);
        EmitMovStateFromReg(ref writer, StateFirmwareCr3ReadbackOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateOriginalCr3RawOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        ulong firmwareCr3Matched = EmitJcc(ref writer, Condition.Equal);
        EmitMovR9dImmediate(ref writer, X64ReversibleCr3TransitionRuntimeStatus.FirmwareCr3ReadbackMismatch);
        ulong skipPostRestoreCanary = EmitJump(ref writer);
        writer.PatchRel32(firmwareCr3Matched, writer.Position);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.FirmwareCr3Restored);
        EmitMovRegFromState(ref writer, Register.R11, StateCanaryAddressOffset);
        EmitMovRaxFromR11(ref writer);
        EmitMovStateFromReg(ref writer, StatePostRestoreCanaryReadbackOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateReplacementCanaryOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        ulong postRestoreCanaryMatched = EmitJcc(ref writer, Condition.Equal);
        EmitCmpR9dImmediate(ref writer, (byte)X64ReversibleCr3TransitionRuntimeStatus.Success);
        ulong preservePrimaryFailure = EmitJcc(ref writer, Condition.NotEqual);
        EmitMovR9dImmediate(ref writer, X64ReversibleCr3TransitionRuntimeStatus.PostRestoreCanaryMismatch);
        writer.PatchRel32(postRestoreCanaryMatched, writer.Position);
        writer.PatchRel32(preservePrimaryFailure, writer.Position);
        ulong cleanupRestoreCr4 = writer.Position;
        writer.PatchRel32(skipPostRestoreCanary, cleanupRestoreCr4);
        EmitMovRegFromState(ref writer, Register.Rax, StateOriginalCr4RawOffset);
        EmitMovCr4FromRax(ref writer);
        EmitMovRaxFromCr4(ref writer);
        EmitMovStateFromReg(ref writer, StateRestoredCr4ReadbackOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateOriginalCr4RawOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        ulong cr4Matched = EmitJcc(ref writer, Condition.Equal);
        EmitMovR9dImmediate(ref writer, X64ReversibleCr3TransitionRuntimeStatus.Cr4RestoreReadbackMismatch);
        ulong skipCr4Checkpoint = EmitJump(ref writer);
        writer.PatchRel32(cr4Matched, writer.Position);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.Cr4Restored);
        ulong cleanupRestoreFlags = writer.Position;
        writer.PatchRel32(skipCr4Checkpoint, cleanupRestoreFlags);
        EmitMovRegFromState(ref writer, Register.Rax, StateOriginalRflagsOffset);
        EmitPushRax(ref writer);
        EmitPopfq(ref writer);
        EmitPushfq(ref writer);
        EmitPopRax(ref writer);
        EmitMovStateFromReg(ref writer, StateRestoredRflagsReadbackOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateOriginalRflagsOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        ulong rflagsMatched = EmitJcc(ref writer, Condition.Equal);
        EmitMovR9dImmediate(ref writer, X64ReversibleCr3TransitionRuntimeStatus.RflagsRestoreReadbackMismatch);
        ulong skipRflagsCheckpoint = EmitJump(ref writer);
        writer.PatchRel32(rflagsMatched, writer.Position);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.RflagsRestored);
        writer.PatchRel32(skipRflagsCheckpoint, writer.Position);
        EmitMovRaxFromRsp(ref writer);
        EmitMovStateFromReg(ref writer, StateExitRspOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateEntryRspOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        ulong rspMatched = EmitJcc(ref writer, Condition.Equal);
        EmitMovR9dImmediate(ref writer, X64ReversibleCr3TransitionRuntimeStatus.RspRestoreMismatch);
        writer.PatchRel32(rspMatched, writer.Position);
        EmitCmpR9dImmediate(ref writer, (byte)X64ReversibleCr3TransitionRuntimeStatus.Success);
        ulong transitionFailed = EmitJcc(ref writer, Condition.NotEqual);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.Returned);
        writer.PatchRel32(transitionFailed, writer.Position);
        EmitMovEaxFromR9d(ref writer);
        EmitMovAlToState(ref writer, StateRuntimeStatusOffset);
        EmitMovRegFromState(ref writer, Register.Rax, StateOriginalRflagsOffset);
        EmitPushRax(ref writer);
        EmitPopfq(ref writer);
        EmitMovEaxFromR9d(ref writer);
        EmitRet(ref writer);
        ulong main = writer.Position;
        writer.PatchRel32(jumpToMain, main);
        EmitPushfq(ref writer);
        EmitPopR8(ref writer);
        EmitTestRegReg(ref writer, Register.Rcx, Register.Rcx);
        ulong statePresent = EmitJcc(ref writer, Condition.NotEqual);
        EmitMovEaxImmediate(ref writer, (uint)X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitPushR8(ref writer);
        EmitPopfq(ref writer);
        EmitRet(ref writer);
        writer.PatchRel32(statePresent, writer.Position);
        EmitMovRegReg(ref writer, Register.R10, Register.Rcx);
        EmitMovRegReg(ref writer, Register.Rax, Register.Rcx);
        EmitTestRaxImmediate(ref writer, 7);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitMovRegReg(ref writer, Register.Rdx, Register.R10);
        EmitAddRegImmediate32(ref writer, Register.Rdx, X64ReversibleCr3TransitionRuntimeState.StructureSize);
        EmitPreCliGuard(ref writer, Condition.AboveOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitMovRegImmediate64(ref writer, Register.R11, LowCanonicalExclusiveCeiling);
        EmitCmpRegReg(ref writer, Register.Rdx, Register.R11);
        EmitPreCliGuard(ref writer, Condition.BelowOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitCmpStateDwordImmediate(ref writer, StateStructureSizeOffset, X64ReversibleCr3TransitionRuntimeState.StructureSize);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitCmpStateWordImmediate(ref writer, StateStructureVersionOffset, X64ReversibleCr3TransitionRuntimeState.StructureVersion);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitCmpStateByteImmediate(ref writer, StateIsInitializedOffset, 1);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitCmpStateByteImmediate(ref writer, StateReserved0Offset, 0);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitCmpStateWordImmediate(ref writer, StateReserved1Offset, 0);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitMovRegFromState(ref writer, Register.Rax, StateReserved2Offset);
        EmitTestRegReg(ref writer, Register.Rax, Register.Rax);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitCmpStateDwordImmediate(ref writer, StateFirmwareCallbackCountOffset, 0);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.FirmwareCallbackForbidden);
        EmitRequireStateQwordZero(ref writer, StateOriginalRflagsOffset);
        EmitRequireStateQwordZero(ref writer, StateOriginalCr3RawOffset);
        EmitRequireStateQwordZero(ref writer, StateOriginalCr4RawOffset);
        EmitRequireStateQwordZero(ref writer, StateClearedCr4ReadbackOffset);
        EmitRequireStateQwordZero(ref writer, StateOwnedCr3ReadbackOffset);
        EmitRequireStateQwordZero(ref writer, StateCanaryBeforeOffset);
        EmitRequireStateQwordZero(ref writer, StateCanaryAfterOffset);
        EmitRequireStateQwordZero(ref writer, StateFirmwareCr3ReadbackOffset);
        EmitRequireStateQwordZero(ref writer, StateRestoredCr4ReadbackOffset);
        EmitRequireStateQwordZero(ref writer, StateCliRflagsReadbackOffset);
        EmitRequireStateQwordZero(ref writer, StateRestoredRflagsReadbackOffset);
        EmitRequireStateQwordZero(ref writer, StateEntryRspOffset);
        EmitRequireStateQwordZero(ref writer, StateExitRspOffset);
        EmitRequireStateQwordZero(ref writer, StatePostRestoreCanaryReadbackOffset);
        EmitCmpStateByteImmediate(ref writer, StateRuntimeStatusOffset, (byte)X64ReversibleCr3TransitionRuntimeStatus.Uninitialized);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitCmpStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.Uninitialized);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        EmitMovRegFromState(ref writer, Register.Rax, StateOwnedCr3RawOffset);
        EmitTestRegReg(ref writer, Register.Rax, Register.Rax);
        EmitPreCliGuard(ref writer, Condition.NotEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidOwnedCr3);
        EmitTestRaxImmediate(ref writer, 0xfff);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidOwnedCr3);
        EmitMovRegReg(ref writer, Register.Rdx, Register.Rax);
        EmitShiftRightImmediate(ref writer, Register.Rdx, 52);
        EmitTestRegReg(ref writer, Register.Rdx, Register.Rdx);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidOwnedCr3);
        EmitMovRegFromState(ref writer, Register.Rax, StateStackStartOffset);
        EmitTestRegReg(ref writer, Register.Rax, Register.Rax);
        EmitPreCliGuard(ref writer, Condition.NotEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidStackRange);
        EmitTestRaxImmediate(ref writer, 0xf);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidStackRange);
        EmitMovRegFromState(ref writer, Register.Rdx, StateStackEndExclusiveOffset);
        EmitCmpRegReg(ref writer, Register.Rdx, Register.Rax);
        EmitPreCliGuard(ref writer, Condition.Above, X64ReversibleCr3TransitionRuntimeStatus.InvalidStackRange);
        EmitTestRdxImmediate(ref writer, 0xf);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidStackRange);
        EmitMovRegImmediate64(ref writer, Register.R11, LowCanonicalExclusiveCeiling);
        EmitCmpRegReg(ref writer, Register.Rdx, Register.R11);
        EmitPreCliGuard(ref writer, Condition.BelowOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidStackRange);
        EmitMovRegReg(ref writer, Register.R11, Register.Rdx);
        EmitSubRegReg(ref writer, Register.R11, Register.Rax);
        EmitCmpRegImmediate(ref writer, Register.R11, 32);
        EmitPreCliGuard(ref writer, Condition.AboveOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidStackRange);
        EmitMovRegReg(ref writer, Register.R11, Register.R10);
        EmitAddRegImmediate32(ref writer, Register.R11, X64ReversibleCr3TransitionRuntimeState.StructureSize);
        EmitMovRegFromState(ref writer, Register.Rax, StateStackStartOffset);
        EmitCmpRegReg(ref writer, Register.R11, Register.Rax);
        ulong stateBeforeStack = EmitJcc(ref writer, Condition.BelowOrEqual);
        EmitMovRegFromState(ref writer, Register.Rdx, StateStackEndExclusiveOffset);
        EmitCmpRegReg(ref writer, Register.R10, Register.Rdx);
        EmitPreCliGuard(ref writer, Condition.AboveOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
        writer.PatchRel32(stateBeforeStack, writer.Position);
        EmitMovRegFromState(ref writer, Register.Rax, StateCanaryAddressOffset);
        EmitTestRegReg(ref writer, Register.Rax, Register.Rax);
        EmitPreCliGuard(ref writer, Condition.NotEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        EmitTestRaxImmediate(ref writer, 7);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        EmitMovRegReg(ref writer, Register.Rdx, Register.Rax);
        EmitAddRegImmediate(ref writer, Register.Rdx, sizeof(ulong));
        EmitPreCliGuard(ref writer, Condition.AboveOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        EmitMovRegImmediate64(ref writer, Register.R11, LowCanonicalExclusiveCeiling);
        EmitCmpRegReg(ref writer, Register.Rdx, Register.R11);
        EmitPreCliGuard(ref writer, Condition.BelowOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        EmitMovRegReg(ref writer, Register.R11, Register.R10);
        EmitAddRegImmediate32(ref writer, Register.R11, X64ReversibleCr3TransitionRuntimeState.StructureSize);
        EmitCmpRegReg(ref writer, Register.Rax, Register.R11);
        EmitPreCliGuard(ref writer, Condition.AboveOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        EmitMovRegFromState(ref writer, Register.R11, StateStackStartOffset);
        EmitCmpRegReg(ref writer, Register.Rdx, Register.R11);
        ulong canaryBeforeStack = EmitJcc(ref writer, Condition.BelowOrEqual);
        EmitMovRegFromState(ref writer, Register.R11, StateStackEndExclusiveOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.R11);
        EmitPreCliGuard(ref writer, Condition.AboveOrEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        writer.PatchRel32(canaryBeforeStack, writer.Position);
        EmitMovRegFromState(ref writer, Register.Rax, StateExpectedCanaryOffset);
        EmitTestRegReg(ref writer, Register.Rax, Register.Rax);
        EmitPreCliGuard(ref writer, Condition.NotEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        EmitMovRegFromState(ref writer, Register.Rdx, StateReplacementCanaryOffset);
        EmitTestRegReg(ref writer, Register.Rdx, Register.Rdx);
        EmitPreCliGuard(ref writer, Condition.NotEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        EmitPreCliGuard(ref writer, Condition.NotEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary);
        EmitMovRaxFromRsp(ref writer);
        EmitMovRegReg(ref writer, Register.Rdx, Register.Rax);
        EmitAndRdxImmediate(ref writer, 0xf);
        EmitCmpRegImmediate(ref writer, Register.Rdx, 8);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.RspOutsideExpectedRange);
        EmitMovRegFromState(ref writer, Register.Rdx, StateStackStartOffset);
        EmitAddRegImmediate(ref writer, Register.Rdx, 8);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        EmitPreCliGuard(ref writer, Condition.AboveOrEqual, X64ReversibleCr3TransitionRuntimeStatus.RspOutsideExpectedRange);
        EmitMovRegFromState(ref writer, Register.Rdx, StateStackEndExclusiveOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        EmitPreCliGuard(ref writer, Condition.Below, X64ReversibleCr3TransitionRuntimeStatus.RspOutsideExpectedRange);
        EmitTestR8Immediate(ref writer, unchecked((uint)RflagsArchitecturalReservedOneBit));
        EmitPreCliGuard(ref writer, Condition.NotEqual, X64ReversibleCr3TransitionRuntimeStatus.InterruptDisableReadbackMismatch);
        EmitMovStateByteImmediate(ref writer, StateRuntimeStatusOffset, (byte)X64ReversibleCr3TransitionRuntimeStatus.Entered);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.EntryValidated);
        EmitMovStateFromReg(ref writer, StateEntryRspOffset, Register.Rax);
        EmitMovStateFromReg(ref writer, StateOriginalRflagsOffset, Register.R8);
        EmitCli(ref writer);
        EmitPushfq(ref writer);
        EmitPopRax(ref writer);
        EmitMovStateFromReg(ref writer, StateCliRflagsReadbackOffset, Register.Rax);
        EmitMovRegReg(ref writer, Register.Rdx, Register.Rax);
        EmitAndRdxImmediate(ref writer, unchecked((uint)(RflagsInterruptEnableBit | RflagsArchitecturalReservedOneBit)));
        EmitCmpRegImmediate(ref writer, Register.Rdx, unchecked((byte)RflagsArchitecturalReservedOneBit));
        EmitCleanupGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InterruptDisableReadbackMismatch, cleanupRestoreFlags);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.InterruptsDisabled);
        EmitMovRaxFromCr3(ref writer);
        EmitMovStateFromReg(ref writer, StateOriginalCr3RawOffset, Register.Rax);
        EmitMovRaxFromCr4(ref writer);
        EmitMovStateFromReg(ref writer, StateOriginalCr4RawOffset, Register.Rax);
        EmitTestRaxImmediate(ref writer, unchecked((uint)Cr4FiveLevelPagingBit));
        EmitCleanupGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.La57NotSupported, cleanupRestoreFlags);
        EmitTestRaxImmediate(ref writer, unchecked((uint)Cr4PcideBit));
        EmitCleanupGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.PcideNotSupported, cleanupRestoreFlags);
        EmitMovRegFromState(ref writer, Register.Rdx, StateOriginalCr3RawOffset);
        EmitAndRdxImmediate(ref writer, 0xffff_f000);
        EmitMovRegFromState(ref writer, Register.R11, StateOwnedCr3RawOffset);
        EmitCmpRegReg(ref writer, Register.Rdx, Register.R11);
        EmitCleanupGuard(ref writer, Condition.NotEqual, X64ReversibleCr3TransitionRuntimeStatus.InvalidOwnedCr3, cleanupRestoreFlags);
        EmitAndRaxImmediate(ref writer, unchecked((uint)~Cr4PageGlobalEnableBit));
        EmitMovCr4FromRax(ref writer);
        EmitMovRaxFromCr4(ref writer);
        EmitMovStateFromReg(ref writer, StateClearedCr4ReadbackOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateOriginalCr4RawOffset);
        EmitAndRdxImmediate(ref writer, unchecked((uint)~Cr4PageGlobalEnableBit));
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        EmitCleanupGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.PgeClearReadbackMismatch, cleanupRestoreCr4);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.PgeCleared);
        EmitMovRegFromState(ref writer, Register.Rax, StateOwnedCr3RawOffset);
        EmitMovCr3FromRax(ref writer);
        EmitMovRaxFromCr3(ref writer);
        EmitMovStateFromReg(ref writer, StateOwnedCr3ReadbackOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateOwnedCr3RawOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        EmitCleanupGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.OwnedCr3ReadbackMismatch, cleanupRestoreCr3);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.OwnedCr3Loaded);
        EmitMovRegFromState(ref writer, Register.R11, StateCanaryAddressOffset);
        EmitMovRaxFromR11(ref writer);
        EmitMovStateFromReg(ref writer, StateCanaryBeforeOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateExpectedCanaryOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        EmitCleanupGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.CanaryBeforeMismatch, cleanupRestoreCr3);
        EmitMovRegFromState(ref writer, Register.Rax, StateReplacementCanaryOffset);
        EmitMovR11FromRax(ref writer);
        EmitMovRaxFromR11(ref writer);
        EmitMovStateFromReg(ref writer, StateCanaryAfterOffset, Register.Rax);
        EmitMovRegFromState(ref writer, Register.Rdx, StateReplacementCanaryOffset);
        EmitCmpRegReg(ref writer, Register.Rax, Register.Rdx);
        EmitCleanupGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.CanaryAfterMismatch, cleanupRestoreCr3);
        EmitMovStateByteImmediate(ref writer, StateCheckpointOffset, (byte)X64ReversibleCr3TransitionCheckpoint.CanaryUpdated);
        EmitMovR9dImmediate(ref writer, X64ReversibleCr3TransitionRuntimeStatus.Success);
        ulong normalCleanup = EmitJump(ref writer);
        writer.PatchRel32(normalCleanup, cleanupRestoreCr3);
    }

    private static void EmitRequireStateQwordZero(ref StubBuilder writer, uint stateOffset)
    {
        EmitMovRegFromState(ref writer, Register.Rax, stateOffset);
        EmitTestRegReg(ref writer, Register.Rax, Register.Rax);
        EmitPreCliGuard(ref writer, Condition.Equal, X64ReversibleCr3TransitionRuntimeStatus.InvalidState);
    }

    private static void EmitPreCliGuard(ref StubBuilder writer, Condition successCondition, X64ReversibleCr3TransitionRuntimeStatus failureStatus)
    {
        ulong success = EmitJcc(ref writer, successCondition);
        EmitMovEaxImmediate(ref writer, (uint)failureStatus);
        EmitPushR8(ref writer);
        EmitPopfq(ref writer);
        EmitRet(ref writer);
        writer.PatchRel32(success, writer.Position);
    }

    private static void EmitCleanupGuard(ref StubBuilder writer, Condition successCondition, X64ReversibleCr3TransitionRuntimeStatus failureStatus, ulong cleanupTarget)
    {
        ulong success = EmitJcc(ref writer, successCondition);
        EmitMovR9dImmediate(ref writer, failureStatus);
        ulong cleanup = EmitJump(ref writer);
        writer.PatchRel32(cleanup, cleanupTarget);
        writer.PatchRel32(success, writer.Position);
    }

    private static ulong EmitJump(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0xe9);
        return writer.Rel32Placeholder();
    }

    private static ulong EmitJcc(ref StubBuilder writer, Condition condition)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte((byte)condition);
        return writer.Rel32Placeholder();
    }

    private static void EmitMovRegReg(ref StubBuilder writer, Register destination, Register source)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        byte sourceValue = (byte)source;
        writer.Byte((byte)(0x48 | (sourceValue >= 8 ? 4 : 0) | (destinationValue >= 8 ? 1 : 0)));
        writer.Byte(0x89);
        writer.Byte((byte)(0xc0 | ((sourceValue & 7) << 3) | (destinationValue & 7)));
    }

    private static void EmitMovRegFromState(ref StubBuilder writer, Register destination, uint stateOffset)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        writer.Byte((byte)(0x49 | (destinationValue >= 8 ? 4 : 0)));
        writer.Byte(0x8b);
        writer.Byte((byte)(0x82 | ((destinationValue & 7) << 3)));
        writer.UInt32(stateOffset);
    }

    private static void EmitMovStateFromReg(ref StubBuilder writer, uint stateOffset, Register source)
    {
        writer.Instruction();
        byte sourceValue = (byte)source;
        writer.Byte((byte)(0x49 | (sourceValue >= 8 ? 4 : 0)));
        writer.Byte(0x89);
        writer.Byte((byte)(0x82 | ((sourceValue & 7) << 3)));
        writer.UInt32(stateOffset);
    }

    private static void EmitMovStateByteImmediate(ref StubBuilder writer, uint stateOffset, byte value)
    {
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0xc6);
        writer.Byte(0x82);
        writer.UInt32(stateOffset);
        writer.Byte(value);
    }

    private static void EmitMovAlToState(ref StubBuilder writer, uint stateOffset)
    {
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0x88);
        writer.Byte(0x82);
        writer.UInt32(stateOffset);
    }

    private static void EmitCmpStateDwordImmediate(ref StubBuilder writer, uint stateOffset, uint value)
    {
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0x81);
        writer.Byte(0xba);
        writer.UInt32(stateOffset);
        writer.UInt32(value);
    }

    private static void EmitCmpStateWordImmediate(ref StubBuilder writer, uint stateOffset, ushort value)
    {
        writer.Instruction();
        writer.Byte(0x66);
        writer.Byte(0x41);
        writer.Byte(0x81);
        writer.Byte(0xba);
        writer.UInt32(stateOffset);
        writer.UInt16(value);
    }

    private static void EmitCmpStateByteImmediate(ref StubBuilder writer, uint stateOffset, byte value)
    {
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0x80);
        writer.Byte(0xba);
        writer.UInt32(stateOffset);
        writer.Byte(value);
    }

    private static void EmitTestRegReg(ref StubBuilder writer, Register left, Register right)
    {
        writer.Instruction();
        byte leftValue = (byte)left;
        byte rightValue = (byte)right;
        writer.Byte((byte)(0x48 | (rightValue >= 8 ? 4 : 0) | (leftValue >= 8 ? 1 : 0)));
        writer.Byte(0x85);
        writer.Byte((byte)(0xc0 | ((rightValue & 7) << 3) | (leftValue & 7)));
    }

    private static void EmitTestRaxImmediate(ref StubBuilder writer, uint value)
    {
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0xa9);
        writer.UInt32(value);
    }

    private static void EmitTestRdxImmediate(ref StubBuilder writer, uint value)
    {
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0xf7);
        writer.Byte(0xc2);
        writer.UInt32(value);
    }

    private static void EmitTestR8Immediate(ref StubBuilder writer, uint value)
    {
        writer.Instruction();
        writer.Byte(0x49);
        writer.Byte(0xf7);
        writer.Byte(0xc0);
        writer.UInt32(value);
    }

    private static void EmitCmpRegReg(ref StubBuilder writer, Register left, Register right)
    {
        writer.Instruction();
        byte leftValue = (byte)left;
        byte rightValue = (byte)right;
        writer.Byte((byte)(0x48 | (rightValue >= 8 ? 4 : 0) | (leftValue >= 8 ? 1 : 0)));
        writer.Byte(0x39);
        writer.Byte((byte)(0xc0 | ((rightValue & 7) << 3) | (leftValue & 7)));
    }

    private static void EmitCmpRegImmediate(ref StubBuilder writer, Register register, byte value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0x83);
        writer.Byte((byte)(0xf8 | (registerValue & 7)));
        writer.Byte(value);
    }

    private static void EmitCmpR9dImmediate(ref StubBuilder writer, byte value)
    {
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0x83);
        writer.Byte(0xf9);
        writer.Byte(value);
    }

    private static void EmitAndRaxImmediate(ref StubBuilder writer, uint value)
    {
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x25);
        writer.UInt32(value);
    }

    private static void EmitAndRdxImmediate(ref StubBuilder writer, uint value)
    {
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x81);
        writer.Byte(0xe2);
        writer.UInt32(value);
    }

    private static void EmitAddRegImmediate(ref StubBuilder writer, Register register, byte value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0x83);
        writer.Byte((byte)(0xc0 | (registerValue & 7)));
        writer.Byte(value);
    }

    private static void EmitAddRegImmediate32(ref StubBuilder writer, Register register, uint value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0x81);
        writer.Byte((byte)(0xc0 | (registerValue & 7)));
        writer.UInt32(value);
    }

    private static void EmitSubRegReg(ref StubBuilder writer, Register destination, Register source)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        byte sourceValue = (byte)source;
        writer.Byte((byte)(0x48 | (sourceValue >= 8 ? 4 : 0) | (destinationValue >= 8 ? 1 : 0)));
        writer.Byte(0x29);
        writer.Byte((byte)(0xc0 | ((sourceValue & 7) << 3) | (destinationValue & 7)));
    }

    private static void EmitShiftRightImmediate(ref StubBuilder writer, Register register, byte count)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0xc1);
        writer.Byte((byte)(0xe8 | (registerValue & 7)));
        writer.Byte(count);
    }

    private static void EmitMovR9dImmediate(ref StubBuilder writer, X64ReversibleCr3TransitionRuntimeStatus value)
    {
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0xb9);
        writer.UInt32((uint)value);
    }

    private static void EmitMovEaxImmediate(ref StubBuilder writer, uint value)
    {
        writer.Instruction();
        writer.Byte(0xb8);
        writer.UInt32(value);
    }

    private static void EmitMovRegImmediate64(ref StubBuilder writer, Register register, ulong value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte((byte)(0xb8 | (registerValue & 7)));
        writer.UInt64(value);
    }

    private static void EmitMovEaxFromR9d(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x44);
        writer.Byte(0x89);
        writer.Byte(0xc8);
    }

    private static void EmitMovRaxFromRsp(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x89);
        writer.Byte(0xe0);
    }

    private static void EmitMovRaxFromR11(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x49);
        writer.Byte(0x8b);
        writer.Byte(0x03);
    }

    private static void EmitMovR11FromRax(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x49);
        writer.Byte(0x89);
        writer.Byte(0x03);
    }

    private static void EmitMovRaxFromCr3(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0x20);
        writer.Byte(0xd8);
    }

    private static void EmitMovCr3FromRax(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0x22);
        writer.Byte(0xd8);
    }

    private static void EmitMovRaxFromCr4(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0x20);
        writer.Byte(0xe0);
    }

    private static void EmitMovCr4FromRax(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0x22);
        writer.Byte(0xe0);
    }

    private static void EmitPushfq(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x9c);
    }

    private static void EmitPopRax(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x58);
    }

    private static void EmitPopR8(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0x58);
    }

    private static void EmitCli(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0xfa);
    }

    private static void EmitPushRax(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x50);
    }

    private static void EmitPushR8(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0x50);
    }

    private static void EmitPopfq(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0x9d);
    }

    private static void EmitRet(ref StubBuilder writer)
    {
        writer.Instruction();
        writer.Byte(0xc3);
    }

    private static X64ReversibleCr3TransitionStubPlanStatus ValidateWriteBoundary(byte* destination, ulong destinationCapacity, ulong entryAddress)
    {
        if (destinationCapacity > MaximumByteCapacity)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.CapacityExceedsLimit;
        }

        if (destination == null)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.MissingDestination;
        }

        if ((((ulong)destination) & (RequiredPointerAlignment - 1)) != 0)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.MisalignedDestination;
        }

        ulong destinationAddress = (ulong)destination;
        if (destinationAddress + destinationCapacity < destinationAddress)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.DestinationSpanOverflow;
        }

        if (!IsValidEntryAddress(entryAddress))
        {
            return entryAddress == 0 ? X64ReversibleCr3TransitionStubPlanStatus.InvalidEntryAddress : X64ReversibleCr3TransitionStubPlanStatus.MisalignedEntryAddress;
        }

        if (entryAddress + RequiredByteLength < entryAddress)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.EntryAddressOverflow;
        }

        if (destinationCapacity < RequiredByteLength)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.DestinationCapacityTooSmall;
        }

        return X64ReversibleCr3TransitionStubPlanStatus.Success;
    }

    private static bool IsValidEntryAddress(ulong entryAddress)
    {
        return entryAddress != 0 && (entryAddress & (EntryAlignment - 1)) == 0;
    }

    private static bool IsValidStackRange(ulong start, ulong endExclusive)
    {
        return start != 0 && endExclusive > start && endExclusive - start >= 32 && (start & 0xf) == 0 && (endExclusive & 0xf) == 0 && endExclusive <= LowCanonicalExclusiveCeiling;
    }

    private static bool IsValidCanary(X64ReversibleCr3TransitionRuntimeState state)
    {
        return state.CanaryAddress != 0 && (state.CanaryAddress & 7) == 0 && state.ExpectedCanary != 0 && state.ReplacementCanary != 0 && state.ExpectedCanary != state.ReplacementCanary;
    }

    private static bool IsValidCanaryPlacement(ulong runtimeStateAddress, ulong canaryAddress)
    {
        if (!IsValidRuntimeStateAddress(runtimeStateAddress) || canaryAddress == 0 || (canaryAddress & 7) != 0 || !IsLowCanonicalRange(canaryAddress, sizeof(ulong)))
        {
            return false;
        }

        ulong runtimeStateEnd = runtimeStateAddress + X64ReversibleCr3TransitionRuntimeState.StructureSize;
        return canaryAddress >= runtimeStateEnd;
    }

    private static bool IsLowCanonicalRange(ulong start, ulong length)
    {
        if (length == 0)
        {
            return false;
        }

        ulong endExclusive = start + length;
        return endExclusive >= start && endExclusive <= LowCanonicalExclusiveCeiling;
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstLength, ulong secondStart, ulong secondLength)
    {
        if (firstLength == 0 || secondLength == 0)
        {
            return false;
        }

        ulong firstEnd = firstStart + firstLength;
        ulong secondEnd = secondStart + secondLength;
        if (firstEnd < firstStart || secondEnd < secondStart)
        {
            return true;
        }

        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static X64ReversibleCr3TransitionStubPlanStatus TranslateRuntimeFailure(X64ReversibleCr3TransitionRuntimeStatus status)
    {
        if (status == X64ReversibleCr3TransitionRuntimeStatus.FirmwareCallbackForbidden)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.FirmwareCallbackObserved;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.InvalidOwnedCr3)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.InvalidOwnedCr3;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.InvalidStackRange)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.InvalidStackRange;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.InvalidCanary)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.InvalidCanary;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.RspOutsideExpectedRange || status == X64ReversibleCr3TransitionRuntimeStatus.RspRestoreMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.RspEscape;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.InterruptDisableReadbackMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.InterruptStateMismatch;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.La57NotSupported)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.La57NotSupported;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.PcideNotSupported)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.PcideNotSupported;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.PgeClearReadbackMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.PgeClearReadbackMismatch;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.OwnedCr3ReadbackMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.OwnedCr3ReadbackMismatch;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.CanaryBeforeMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.CanaryBeforeMismatch;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.CanaryAfterMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.CanaryAfterMismatch;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.FirmwareCr3ReadbackMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.FirmwareCr3ReadbackMismatch;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.Cr4RestoreReadbackMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.Cr4RestoreReadbackMismatch;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.RflagsRestoreReadbackMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.RflagsRestoreReadbackMismatch;
        }

        if (status == X64ReversibleCr3TransitionRuntimeStatus.PostRestoreCanaryMismatch)
        {
            return X64ReversibleCr3TransitionStubPlanStatus.PostRestoreCanaryMismatch;
        }

        return X64ReversibleCr3TransitionStubPlanStatus.RuntimeReportedFailure;
    }

    private static X64ReversibleCr3TransitionStubPlanResult Fail(X64ReversibleCr3TransitionStubPlanStatus status, ulong entryAddress, ulong sourceCount, ulong sourceCapacity)
    {
        return new X64ReversibleCr3TransitionStubPlanResult(status, 0, RequiredByteLength, entryAddress, X64ReversibleCr3TransitionStubPlanResult.NoIndex, sourceCount, sourceCapacity, 0);
    }

    private static X64ReversibleCr3TransitionEvidenceResult Evidence(X64ReversibleCr3TransitionStubPlanStatus status, X64ReversibleCr3TransitionTerminalKind terminalKind, X64ReversibleCr3TransitionRuntimeState* state, ulong expectedValue, ulong observedValue)
    {
        ulong runtimeStateAddress = (ulong)state;
        X64ReversibleCr3TransitionRuntimeState snapshot = state == null || !IsValidRuntimeStateAddress(runtimeStateAddress) ? default : *state;
        return new X64ReversibleCr3TransitionEvidenceResult(status, terminalKind, runtimeStateAddress, snapshot, expectedValue, observedValue);
    }
}
