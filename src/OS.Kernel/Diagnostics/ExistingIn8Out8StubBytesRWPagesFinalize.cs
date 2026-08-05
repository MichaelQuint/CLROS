namespace OS.Kernel.Diagnostics;

public enum ExistingIn8Out8StubPagePhase : byte
{
    Uninitialized = 0,
    Materialized = 1,
    ReadBackVerified = 2,
    FinalizationAudited = 3
}

public enum ExistingIn8Out8StubPageStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    MissingState = 2,
    MisalignedState = 3,
    StateSpanOverflow = 4,
    InvalidState = 5,
    StaleStateVersion = 6,
    InvalidDestinationLength = 7,
    MissingDestination = 8,
    MisalignedDestination = 9,
    DestinationSpanOverflow = 10,
    InvalidModeledPageAddress = 11,
    ModeledPageSpanOverflow = 12,
    LayoutOverflow = 13,
    LayoutOutsidePage = 14,
    LayoutOverlap = 15,
    InvalidSafeOffset = 16,
    InvalidOut8Offset = 17,
    InvalidIn8Offset = 18,
    InvalidOut8Plan = 19,
    InvalidIn8Plan = 20,
    Out8SourceBytesMismatch = 21,
    In8SourceBytesMismatch = 22,
    InvalidInitialProtection = 23,
    WritableExecutableRejected = 24,
    InvalidPublicationEpoch = 25,
    InvalidExpectedResult = 26,
    ExpectedResultNotMaterialized = 27,
    InvalidSourceLength = 28,
    MissingSource = 29,
    MisalignedSource = 30,
    SourceSpanOverflow = 31,
    SourceAddressMismatch = 32,
    PartialMaterialization = 33,
    ByteReadbackMismatch = 34,
    NonzeroPadding = 35,
    PageHashMismatch = 36,
    ExpectedReadbackNotVerified = 37,
    ExecuteBeforeFinalization = 38,
    InvalidFinalProtection = 39,
    StaleFinalization = 40,
    AlreadyFinalized = 41,
    ContractLayoutMismatch = 42,
    StateStorageOverlap = 43
}

public readonly struct ExistingIn8Out8StubPageState
{
    public const ulong StructureSize = 48;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly ExistingIn8Out8StubPagePhase _phase;
    private readonly ulong _transitionVersion;
    private readonly ulong _publicationEpoch;
    private readonly ulong _modeledPageAddress;
    private readonly OS.Kernel.Memory.PageProtection _currentProtection;
    private readonly byte _finalizationCount;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly ulong _reserved2;

    private ExistingIn8Out8StubPageState(
        ExistingIn8Out8StubPagePhase phase,
        ulong transitionVersion,
        ulong publicationEpoch,
        ulong modeledPageAddress,
        OS.Kernel.Memory.PageProtection currentProtection,
        byte finalizationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _phase = phase;
        _transitionVersion = transitionVersion;
        _publicationEpoch = publicationEpoch;
        _modeledPageAddress = modeledPageAddress;
        _currentProtection = currentProtection;
        _finalizationCount = finalizationCount;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0;

    public ExistingIn8Out8StubPagePhase Phase => _phase;

    public ulong TransitionVersion => _transitionVersion;

    public ulong PublicationEpoch => _publicationEpoch;

    public ulong ModeledPageAddress => _modeledPageAddress;

    public OS.Kernel.Memory.PageProtection CurrentProtection =>
        _currentProtection;

    public byte FinalizationCount => _finalizationCount;

    public bool IsValid
    {
        get
        {
            if (!IsInitialized)
            {
                return false;
            }

            if (_phase == ExistingIn8Out8StubPagePhase.Uninitialized)
            {
                return _transitionVersion == 0
                    && _publicationEpoch == 0
                    && _modeledPageAddress == 0
                    && _currentProtection
                        == OS.Kernel.Memory.PageProtection.None
                    && _finalizationCount == 0;
            }

            if (_publicationEpoch == 0
                || !ExistingIn8Out8StubBytesRWPagesFinalize
                    .IsValidModeledPageAddress(_modeledPageAddress))
            {
                return false;
            }

            if (_phase == ExistingIn8Out8StubPagePhase.Materialized)
            {
                return _transitionVersion == 1
                    && _currentProtection
                        == ExistingIn8Out8StubBytesRWPagesFinalize
                            .ReadWriteProtection
                    && _finalizationCount == 0;
            }

            if (_phase
                == ExistingIn8Out8StubPagePhase.ReadBackVerified)
            {
                return _transitionVersion == 2
                    && _currentProtection
                        == ExistingIn8Out8StubBytesRWPagesFinalize
                            .ReadWriteProtection
                    && _finalizationCount == 0;
            }

            return _phase
                    == ExistingIn8Out8StubPagePhase.FinalizationAudited
                && _transitionVersion == 3
                && _currentProtection
                    == ExistingIn8Out8StubBytesRWPagesFinalize
                        .ReadExecuteProtection
                && _finalizationCount == 1;
        }
    }

    public static ExistingIn8Out8StubPageState Create()
    {
        return new ExistingIn8Out8StubPageState(
            ExistingIn8Out8StubPagePhase.Uninitialized,
            transitionVersion: 0,
            publicationEpoch: 0,
            modeledPageAddress: 0,
            OS.Kernel.Memory.PageProtection.None,
            finalizationCount: 0);
    }

    internal static ExistingIn8Out8StubPageState Advance(
        ExistingIn8Out8StubPagePhase phase,
        ulong transitionVersion,
        ulong publicationEpoch,
        ulong modeledPageAddress,
        OS.Kernel.Memory.PageProtection currentProtection,
        byte finalizationCount)
    {
        return new ExistingIn8Out8StubPageState(
            phase,
            transitionVersion,
            publicationEpoch,
            modeledPageAddress,
            currentProtection,
            finalizationCount);
    }
}

public readonly struct ExistingIn8Out8StubPageResult
{
    public const ulong StructureSize = 112;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly ExistingIn8Out8StubPageStatus _status;
    private readonly ExistingIn8Out8StubPagePhase _phase;
    private readonly byte _liveProtection;
    private readonly byte _liveExecution;
    private readonly byte _finalizationCount;
    private readonly OS.Kernel.Memory.PageProtection _initialProtection;
    private readonly OS.Kernel.Memory.PageProtection _finalProtection;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly ulong _modeledPageAddress;
    private readonly ulong _pageByteCount;
    private readonly ulong _safeOffset;
    private readonly ulong _out8Offset;
    private readonly ulong _in8Offset;
    private readonly ulong _bytesWrittenOrVerified;
    private readonly ulong _instructionByteCount;
    private readonly ulong _occupiedExtent;
    private readonly ulong _fnv1a64;
    private readonly ulong _publicationEpoch;
    private readonly ulong _transitionVersion;
    private readonly ulong _reserved2;

    private ExistingIn8Out8StubPageResult(
        ExistingIn8Out8StubPageStatus status,
        ExistingIn8Out8StubPagePhase phase,
        ulong modeledPageAddress,
        ulong bytesWrittenOrVerified,
        ulong publicationEpoch,
        ulong transitionVersion,
        byte finalizationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _phase = phase;
        _liveProtection = 0;
        _liveExecution = 0;
        _finalizationCount = finalizationCount;
        _initialProtection =
            ExistingIn8Out8StubBytesRWPagesFinalize
                .ReadWriteProtection;
        _finalProtection =
            ExistingIn8Out8StubBytesRWPagesFinalize
                .ReadExecuteProtection;
        _reserved0 = 0;
        _reserved1 = 0;
        _modeledPageAddress = modeledPageAddress;
        _pageByteCount =
            ExistingIn8Out8StubBytesRWPagesFinalize.PageSize;
        _safeOffset =
            ExistingIn8Out8StubBytesRWPagesFinalize.SafeOffset;
        _out8Offset =
            ExistingIn8Out8StubBytesRWPagesFinalize.Out8Offset;
        _in8Offset =
            ExistingIn8Out8StubBytesRWPagesFinalize.In8Offset;
        _bytesWrittenOrVerified = bytesWrittenOrVerified;
        _instructionByteCount =
            ExistingIn8Out8StubBytesRWPagesFinalize
                .InstructionByteCount;
        _occupiedExtent =
            ExistingIn8Out8StubBytesRWPagesFinalize.OccupiedExtent;
        _fnv1a64 =
            ExistingIn8Out8StubBytesRWPagesFinalize.ExpectedFnv1a64;
        _publicationEpoch = publicationEpoch;
        _transitionVersion = transitionVersion;
        _reserved2 = 0;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0;

    public ExistingIn8Out8StubPageStatus Status => _status;

    public ExistingIn8Out8StubPagePhase Phase => _phase;

    public ulong ModeledPageAddress => _modeledPageAddress;

    public ulong PageByteCount => _pageByteCount;

    public ulong SafeOffset => _safeOffset;

    public ulong Out8Offset => _out8Offset;

    public ulong In8Offset => _in8Offset;

    public ulong BytesWrittenOrVerified => _bytesWrittenOrVerified;

    public ulong InstructionByteCount => _instructionByteCount;

    public ulong OccupiedExtent => _occupiedExtent;

    public ulong Fnv1a64 => _fnv1a64;

    public ulong PublicationEpoch => _publicationEpoch;

    public ulong TransitionVersion => _transitionVersion;

    public OS.Kernel.Memory.PageProtection InitialProtection =>
        _initialProtection;

    public OS.Kernel.Memory.PageProtection FinalProtection =>
        _finalProtection;

    public byte FinalizationCount => _finalizationCount;

    public bool LiveProtection => _liveProtection != 0;

    public bool LiveExecution => _liveExecution != 0;

    public bool ReadBackVerified =>
        Succeeded
        && (_phase
                == ExistingIn8Out8StubPagePhase.ReadBackVerified
            || _phase
                == ExistingIn8Out8StubPagePhase.FinalizationAudited);

    public bool FinalizationAudited =>
        Succeeded
        && _phase
            == ExistingIn8Out8StubPagePhase.FinalizationAudited;

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized
                || _status != ExistingIn8Out8StubPageStatus.Success
                || _phase
                    < ExistingIn8Out8StubPagePhase.Materialized
                || _phase
                    > ExistingIn8Out8StubPagePhase
                        .FinalizationAudited
                || !ExistingIn8Out8StubBytesRWPagesFinalize
                    .IsValidModeledPageAddress(_modeledPageAddress)
                || _pageByteCount
                    != ExistingIn8Out8StubBytesRWPagesFinalize.PageSize
                || _safeOffset
                    != ExistingIn8Out8StubBytesRWPagesFinalize.SafeOffset
                || _out8Offset
                    != ExistingIn8Out8StubBytesRWPagesFinalize.Out8Offset
                || _in8Offset
                    != ExistingIn8Out8StubBytesRWPagesFinalize.In8Offset
                || _bytesWrittenOrVerified
                    != ExistingIn8Out8StubBytesRWPagesFinalize.PageSize
                || _instructionByteCount
                    != ExistingIn8Out8StubBytesRWPagesFinalize
                        .InstructionByteCount
                || _occupiedExtent
                    != ExistingIn8Out8StubBytesRWPagesFinalize
                        .OccupiedExtent
                || _fnv1a64
                    != ExistingIn8Out8StubBytesRWPagesFinalize
                        .ExpectedFnv1a64
                || _publicationEpoch == 0
                || _initialProtection
                    != ExistingIn8Out8StubBytesRWPagesFinalize
                        .ReadWriteProtection
                || _finalProtection
                    != ExistingIn8Out8StubBytesRWPagesFinalize
                        .ReadExecuteProtection
                || LiveProtection
                || LiveExecution)
            {
                return false;
            }

            if (_phase
                == ExistingIn8Out8StubPagePhase.Materialized)
            {
                return _transitionVersion == 1
                    && _finalizationCount == 0;
            }

            if (_phase
                == ExistingIn8Out8StubPagePhase.ReadBackVerified)
            {
                return _transitionVersion == 2
                    && _finalizationCount == 0;
            }

            return _transitionVersion == 3
                && _finalizationCount == 1;
        }
    }

    internal static ExistingIn8Out8StubPageResult Success(
        ExistingIn8Out8StubPagePhase phase,
        ulong modeledPageAddress,
        ulong publicationEpoch,
        ulong transitionVersion,
        byte finalizationCount)
    {
        return new ExistingIn8Out8StubPageResult(
            ExistingIn8Out8StubPageStatus.Success,
            phase,
            modeledPageAddress,
            ExistingIn8Out8StubBytesRWPagesFinalize.PageSize,
            publicationEpoch,
            transitionVersion,
            finalizationCount);
    }

    internal static ExistingIn8Out8StubPageResult Fail(
        ExistingIn8Out8StubPageStatus status)
    {
        return new ExistingIn8Out8StubPageResult(
            status,
            ExistingIn8Out8StubPagePhase.Uninitialized,
            modeledPageAddress: 0,
            bytesWrittenOrVerified: 0,
            publicationEpoch: 0,
            transitionVersion: 0,
            finalizationCount: 0);
    }
}

public static unsafe class ExistingIn8Out8StubBytesRWPagesFinalize
{
    public const ulong PageSize = 4096;
    public const ulong RequiredPointerAlignment = PageSize;
    public const ulong RequiredStateAlignment = 8;
    public const ulong SafeOffset = 0;
    public const ulong SafeLength = 6;
    public const ulong Out8Offset = 16;
    public const ulong Out8Length = 7;
    public const ulong In8Offset = 32;
    public const ulong In8Length = 5;
    public const ulong InstructionByteCount = 18;
    public const ulong OccupiedExtent = 37;
    public const uint SafeReturnValue = 0x4f53_4f4b;
    public const ulong ExpectedFnv1a64 = 0x982e_5b7e_3ee5_8e56;
    public const ulong Fnv1a64OffsetBasis = 0xcbf2_9ce4_8422_2325;
    public const ulong Fnv1a64Prime = 0x0000_0100_0000_01b3UL;
    public const bool PerformsLiveProtectionChange = false;
    public const bool PerformsLiveExecution = false;
    public const bool InvokesSafeStub = false;
    public const bool InvokesPortIo = false;

    public const OS.Kernel.Memory.PageProtection
        ReadWriteProtection =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Write);

    public const OS.Kernel.Memory.PageProtection
        ReadExecuteProtection =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Execute);

    public static ExistingIn8Out8StubPageResult Materialize(
        byte* destination,
        ulong modeledDestinationAddress,
        ulong destinationCapacity,
        ulong safeOffset,
        ulong out8Offset,
        ulong in8Offset,
        PortIoStubPlan out8Plan,
        PortIoStubPlan in8Plan,
        OS.Kernel.Memory.PageProtection initialProtection,
        ulong publicationEpoch,
        ExistingIn8Out8StubPageState* state,
        ulong expectedTransitionVersion)
    {
        ExistingIn8Out8StubPageStatus stateStatus =
            ValidateStatePointer(state);
        if (stateStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(stateStatus);
        }

        ExistingIn8Out8StubPageState currentState = *state;
        if (!currentState.IsValid
            || currentState.Phase
                != ExistingIn8Out8StubPagePhase.Uninitialized)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.InvalidState);
        }

        if (expectedTransitionVersion
                != currentState.TransitionVersion)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.StaleStateVersion);
        }

        ExistingIn8Out8StubPageStatus layoutStatus =
            ValidateLayout(safeOffset, out8Offset, in8Offset);
        if (layoutStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(layoutStatus);
        }

        if (out8Plan.OperationKind != PortIoOperationKind.Out8)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.InvalidOut8Plan);
        }

        if (in8Plan.OperationKind != PortIoOperationKind.In8)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.InvalidIn8Plan);
        }

        ExistingIn8Out8StubPageStatus protectionStatus =
            ValidateInitialProtection(initialProtection);
        if (protectionStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                protectionStatus);
        }

        if (publicationEpoch == 0)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus
                    .InvalidPublicationEpoch);
        }

        ExistingIn8Out8StubPageStatus storageStatus =
            ValidateStorage(
                destination,
                destinationCapacity,
                modeledDestinationAddress,
                isSource: false);
        if (storageStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                storageStatus);
        }

        if (StateOverlapsStorage(
                state,
                destination,
                destinationCapacity))
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.StateStorageOverlap);
        }

        byte* expectedPage = stackalloc byte[(int)PageSize];
        ExistingIn8Out8StubPageStatus buildStatus =
            BuildExpectedPage(
                expectedPage,
                out8Plan,
                in8Plan,
                out ulong expectedHash);
        if (buildStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(buildStatus);
        }

        if (expectedHash != ExpectedFnv1a64)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.PageHashMismatch);
        }

        for (ulong index = 0; index < PageSize; index++)
        {
            destination[index] = expectedPage[index];
        }

        ulong nextVersion = currentState.TransitionVersion + 1;
        *state = ExistingIn8Out8StubPageState.Advance(
            ExistingIn8Out8StubPagePhase.Materialized,
            nextVersion,
            publicationEpoch,
            modeledDestinationAddress,
            ReadWriteProtection,
            finalizationCount: 0);

        return ExistingIn8Out8StubPageResult.Success(
            ExistingIn8Out8StubPagePhase.Materialized,
            modeledDestinationAddress,
            publicationEpoch,
            nextVersion,
            finalizationCount: 0);
    }

    public static ExistingIn8Out8StubPageResult ValidateReadback(
        byte* source,
        ulong modeledSourceAddress,
        ulong sourceCapacity,
        ExistingIn8Out8StubPageResult expectedMaterialization,
        ExistingIn8Out8StubPageState* state,
        ulong expectedTransitionVersion)
    {
        ExistingIn8Out8StubPageStatus stateStatus =
            ValidateStatePointer(state);
        if (stateStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(stateStatus);
        }

        ExistingIn8Out8StubPageState currentState = *state;
        if (!currentState.IsValid)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.InvalidState);
        }

        if (expectedTransitionVersion
                != currentState.TransitionVersion)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.StaleStateVersion);
        }

        if (!expectedMaterialization.Succeeded)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.InvalidExpectedResult);
        }

        if (expectedMaterialization.Phase
                != ExistingIn8Out8StubPagePhase.Materialized
            || currentState.Phase
                != ExistingIn8Out8StubPagePhase.Materialized
            || currentState.TransitionVersion
                != expectedMaterialization.TransitionVersion
            || currentState.PublicationEpoch
                != expectedMaterialization.PublicationEpoch
            || currentState.ModeledPageAddress
                != expectedMaterialization.ModeledPageAddress
            || currentState.CurrentProtection
                != ReadWriteProtection)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus
                    .ExpectedResultNotMaterialized);
        }

        ExistingIn8Out8StubPageStatus storageStatus =
            ValidateStorage(
                source,
                sourceCapacity,
                modeledSourceAddress,
                isSource: true);
        if (storageStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                storageStatus);
        }

        if (StateOverlapsStorage(state, source, sourceCapacity))
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.StateStorageOverlap);
        }

        if (modeledSourceAddress
            != expectedMaterialization.ModeledPageAddress)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.SourceAddressMismatch);
        }

        byte* expectedPage = stackalloc byte[(int)PageSize];
        ExistingIn8Out8StubPageStatus buildStatus =
            BuildExpectedPage(
                expectedPage,
                PortIoStubPlan.Out8,
                PortIoStubPlan.In8,
                out ulong expectedHash);
        if (buildStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(buildStatus);
        }

        ulong observedHash = Fnv1a64OffsetBasis;
        for (ulong index = 0; index < PageSize; index++)
        {
            byte expectedByte = expectedPage[index];
            byte observedByte = source[index];
            observedHash = unchecked(
                (observedHash ^ observedByte) * Fnv1a64Prime);
            if (observedByte == expectedByte)
            {
                continue;
            }

            if (expectedByte == 0)
            {
                return ExistingIn8Out8StubPageResult.Fail(
                    ExistingIn8Out8StubPageStatus.NonzeroPadding);
            }

            if (observedByte == 0)
            {
                return ExistingIn8Out8StubPageResult.Fail(
                    ExistingIn8Out8StubPageStatus
                        .PartialMaterialization);
            }

            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.ByteReadbackMismatch);
        }

        if (expectedHash != ExpectedFnv1a64
            || observedHash != ExpectedFnv1a64)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.PageHashMismatch);
        }

        ulong nextVersion = currentState.TransitionVersion + 1;
        *state = ExistingIn8Out8StubPageState.Advance(
            ExistingIn8Out8StubPagePhase.ReadBackVerified,
            nextVersion,
            currentState.PublicationEpoch,
            currentState.ModeledPageAddress,
            ReadWriteProtection,
            finalizationCount: 0);

        return ExistingIn8Out8StubPageResult.Success(
            ExistingIn8Out8StubPagePhase.ReadBackVerified,
            currentState.ModeledPageAddress,
            currentState.PublicationEpoch,
            nextVersion,
            finalizationCount: 0);
    }

    public static ExistingIn8Out8StubPageResult AuditFinalization(
        ExistingIn8Out8StubPageResult verifiedReadback,
        ulong targetPageAddress,
        OS.Kernel.Memory.PageProtection initialProtection,
        OS.Kernel.Memory.PageProtection finalProtection,
        bool executionAttemptedBeforeFinalization,
        ExistingIn8Out8StubPageState* state,
        ulong expectedTransitionVersion)
    {
        ExistingIn8Out8StubPageStatus stateStatus =
            ValidateStatePointer(state);
        if (stateStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(stateStatus);
        }

        ExistingIn8Out8StubPageState currentState = *state;
        if (!currentState.IsValid)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.InvalidState);
        }

        if (currentState.Phase
            == ExistingIn8Out8StubPagePhase.FinalizationAudited)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.AlreadyFinalized);
        }

        if (expectedTransitionVersion
                != currentState.TransitionVersion)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.StaleFinalization);
        }

        if (!verifiedReadback.Succeeded)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.InvalidExpectedResult);
        }

        if (verifiedReadback.Phase
                != ExistingIn8Out8StubPagePhase.ReadBackVerified
            || currentState.Phase
                != ExistingIn8Out8StubPagePhase.ReadBackVerified)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus
                    .ExpectedReadbackNotVerified);
        }

        if (targetPageAddress != verifiedReadback.ModeledPageAddress
            || targetPageAddress != currentState.ModeledPageAddress
            || verifiedReadback.PublicationEpoch
                != currentState.PublicationEpoch
            || verifiedReadback.TransitionVersion
                != currentState.TransitionVersion)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus.StaleFinalization);
        }

        ExistingIn8Out8StubPageStatus initialStatus =
            ValidateInitialProtection(initialProtection);
        if (initialStatus != ExistingIn8Out8StubPageStatus.Success)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                initialStatus);
        }

        if (executionAttemptedBeforeFinalization)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus
                    .ExecuteBeforeFinalization);
        }

        if (IsWritableExecutable(finalProtection))
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus
                    .WritableExecutableRejected);
        }

        if (finalProtection != ReadExecuteProtection)
        {
            return ExistingIn8Out8StubPageResult.Fail(
                ExistingIn8Out8StubPageStatus
                    .InvalidFinalProtection);
        }

        ulong nextVersion = currentState.TransitionVersion + 1;
        *state = ExistingIn8Out8StubPageState.Advance(
            ExistingIn8Out8StubPagePhase.FinalizationAudited,
            nextVersion,
            currentState.PublicationEpoch,
            currentState.ModeledPageAddress,
            ReadExecuteProtection,
            finalizationCount: 1);

        return ExistingIn8Out8StubPageResult.Success(
            ExistingIn8Out8StubPagePhase.FinalizationAudited,
            currentState.ModeledPageAddress,
            currentState.PublicationEpoch,
            nextVersion,
            finalizationCount: 1);
    }

#if !NOSTDLIB_EFI
    public static ExistingIn8Out8StubPageResult Materialize(
        Span<byte> destination,
        ulong modeledDestinationAddress,
        ulong safeOffset,
        ulong out8Offset,
        ulong in8Offset,
        PortIoStubPlan out8Plan,
        PortIoStubPlan in8Plan,
        OS.Kernel.Memory.PageProtection initialProtection,
        ulong publicationEpoch,
        ref ExistingIn8Out8StubPageState state,
        ulong expectedTransitionVersion)
    {
        fixed (byte* destinationPointer = destination)
        fixed (ExistingIn8Out8StubPageState* statePointer = &state)
        {
            return Materialize(
                destinationPointer,
                modeledDestinationAddress,
                (ulong)destination.Length,
                safeOffset,
                out8Offset,
                in8Offset,
                out8Plan,
                in8Plan,
                initialProtection,
                publicationEpoch,
                statePointer,
                expectedTransitionVersion);
        }
    }

    public static ExistingIn8Out8StubPageResult ValidateReadback(
        ReadOnlySpan<byte> source,
        ulong modeledSourceAddress,
        ExistingIn8Out8StubPageResult expectedMaterialization,
        ref ExistingIn8Out8StubPageState state,
        ulong expectedTransitionVersion)
    {
        fixed (byte* sourcePointer = source)
        fixed (ExistingIn8Out8StubPageState* statePointer = &state)
        {
            return ValidateReadback(
                sourcePointer,
                modeledSourceAddress,
                (ulong)source.Length,
                expectedMaterialization,
                statePointer,
                expectedTransitionVersion);
        }
    }

    public static ExistingIn8Out8StubPageResult AuditFinalization(
        ExistingIn8Out8StubPageResult verifiedReadback,
        ulong targetPageAddress,
        OS.Kernel.Memory.PageProtection initialProtection,
        OS.Kernel.Memory.PageProtection finalProtection,
        bool executionAttemptedBeforeFinalization,
        ref ExistingIn8Out8StubPageState state,
        ulong expectedTransitionVersion)
    {
        fixed (ExistingIn8Out8StubPageState* statePointer = &state)
        {
            return AuditFinalization(
                verifiedReadback,
                targetPageAddress,
                initialProtection,
                finalProtection,
                executionAttemptedBeforeFinalization,
                statePointer,
                expectedTransitionVersion);
        }
    }
#endif

    internal static bool IsValidModeledPageAddress(
        ulong modeledPageAddress)
    {
        return modeledPageAddress != 0
            && (modeledPageAddress & (PageSize - 1)) == 0
            && modeledPageAddress <= ~0UL - PageSize;
    }

    private static ExistingIn8Out8StubPageStatus ValidateStatePointer(
        ExistingIn8Out8StubPageState* state)
    {
        if (state == null)
        {
            return ExistingIn8Out8StubPageStatus.MissingState;
        }

        ulong stateAddress = (ulong)state;
        if ((stateAddress & (RequiredStateAlignment - 1)) != 0)
        {
            return ExistingIn8Out8StubPageStatus.MisalignedState;
        }

        if (stateAddress
            > ~0UL - ExistingIn8Out8StubPageState.StructureSize)
        {
            return ExistingIn8Out8StubPageStatus.StateSpanOverflow;
        }

        return ExistingIn8Out8StubPageStatus.Success;
    }

    private static ExistingIn8Out8StubPageStatus ValidateStorage(
        byte* storage,
        ulong capacity,
        ulong modeledPageAddress,
        bool isSource)
    {
        if (capacity != PageSize)
        {
            return isSource
                ? ExistingIn8Out8StubPageStatus.InvalidSourceLength
                : ExistingIn8Out8StubPageStatus
                    .InvalidDestinationLength;
        }

        if (storage == null)
        {
            return isSource
                ? ExistingIn8Out8StubPageStatus.MissingSource
                : ExistingIn8Out8StubPageStatus.MissingDestination;
        }

        ulong storageAddress = (ulong)storage;
        if ((storageAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return isSource
                ? ExistingIn8Out8StubPageStatus.MisalignedSource
                : ExistingIn8Out8StubPageStatus
                    .MisalignedDestination;
        }

        if (storageAddress > ~0UL - capacity)
        {
            return isSource
                ? ExistingIn8Out8StubPageStatus.SourceSpanOverflow
                : ExistingIn8Out8StubPageStatus
                    .DestinationSpanOverflow;
        }

        if (modeledPageAddress == 0
            || (modeledPageAddress & (PageSize - 1)) != 0)
        {
            return ExistingIn8Out8StubPageStatus
                .InvalidModeledPageAddress;
        }

        if (modeledPageAddress > ~0UL - PageSize)
        {
            return ExistingIn8Out8StubPageStatus
                .ModeledPageSpanOverflow;
        }

        return ExistingIn8Out8StubPageStatus.Success;
    }

    private static ExistingIn8Out8StubPageStatus ValidateLayout(
        ulong safeOffset,
        ulong out8Offset,
        ulong in8Offset)
    {
        if (!TryGetEnd(safeOffset, SafeLength, out ulong safeEnd)
            || !TryGetEnd(out8Offset, Out8Length, out ulong out8End)
            || !TryGetEnd(in8Offset, In8Length, out ulong in8End))
        {
            return ExistingIn8Out8StubPageStatus.LayoutOverflow;
        }

        if (safeEnd > PageSize
            || out8End > PageSize
            || in8End > PageSize)
        {
            return ExistingIn8Out8StubPageStatus.LayoutOutsidePage;
        }

        if (RangesOverlap(safeOffset, safeEnd, out8Offset, out8End)
            || RangesOverlap(safeOffset, safeEnd, in8Offset, in8End)
            || RangesOverlap(out8Offset, out8End, in8Offset, in8End))
        {
            return ExistingIn8Out8StubPageStatus.LayoutOverlap;
        }

        if (safeOffset != SafeOffset)
        {
            return ExistingIn8Out8StubPageStatus.InvalidSafeOffset;
        }

        if (out8Offset != Out8Offset)
        {
            return ExistingIn8Out8StubPageStatus.InvalidOut8Offset;
        }

        if (in8Offset != In8Offset)
        {
            return ExistingIn8Out8StubPageStatus.InvalidIn8Offset;
        }

        return ExistingIn8Out8StubPageStatus.Success;
    }

    private static ExistingIn8Out8StubPageStatus BuildExpectedPage(
        byte* page,
        PortIoStubPlan out8Plan,
        PortIoStubPlan in8Plan,
        out ulong fnv1a64)
    {
        fnv1a64 = 0;
        for (ulong index = 0; index < PageSize; index++)
        {
            page[index] = 0;
        }

        page[SafeOffset + 0] = 0xb8;
        page[SafeOffset + 1] =
            (byte)(SafeReturnValue & 0xffU);
        page[SafeOffset + 2] =
            (byte)((SafeReturnValue >> 8) & 0xffU);
        page[SafeOffset + 3] =
            (byte)((SafeReturnValue >> 16) & 0xffU);
        page[SafeOffset + 4] =
            (byte)((SafeReturnValue >> 24) & 0xffU);
        page[SafeOffset + 5] = PortIoStubPlan.RetOpcode;

        PortIoStubPlanResult out8Result =
            out8Plan.Emit(page + Out8Offset, (int)Out8Length);
        if (!out8Result.Succeeded
            || out8Result.BytesWritten != (int)Out8Length)
        {
            return ExistingIn8Out8StubPageStatus.InvalidOut8Plan;
        }

        PortIoStubPlanResult in8Result =
            in8Plan.Emit(page + In8Offset, (int)In8Length);
        if (!in8Result.Succeeded
            || in8Result.BytesWritten != (int)In8Length)
        {
            return ExistingIn8Out8StubPageStatus.InvalidIn8Plan;
        }

        if (!HasExactOut8Bytes(page + Out8Offset))
        {
            return ExistingIn8Out8StubPageStatus
                .Out8SourceBytesMismatch;
        }

        if (!HasExactIn8Bytes(page + In8Offset))
        {
            return ExistingIn8Out8StubPageStatus
                .In8SourceBytesMismatch;
        }

        ulong hash = Fnv1a64OffsetBasis;
        for (ulong index = 0; index < PageSize; index++)
        {
            hash = unchecked(
                (hash ^ page[index]) * Fnv1a64Prime);
        }

        fnv1a64 = hash;
        return ExistingIn8Out8StubPageStatus.Success;
    }

    private static bool HasExactOut8Bytes(byte* bytes)
    {
        return bytes[0] == PortIoStubPlan.MovR8ToRm8Opcode
            && bytes[1] == PortIoStubPlan.MovDlToAlModRm
            && bytes[2] == PortIoStubPlan.OperandSizeOverridePrefix
            && bytes[3] == PortIoStubPlan.MovR16ToRm16Opcode
            && bytes[4] == PortIoStubPlan.MovCxToDxModRm
            && bytes[5] == PortIoOperationPlan.Out8Opcode
            && bytes[6] == PortIoStubPlan.RetOpcode;
    }

    private static bool HasExactIn8Bytes(byte* bytes)
    {
        return bytes[0] == PortIoStubPlan.OperandSizeOverridePrefix
            && bytes[1] == PortIoStubPlan.MovR16ToRm16Opcode
            && bytes[2] == PortIoStubPlan.MovCxToDxModRm
            && bytes[3] == PortIoOperationPlan.In8Opcode
            && bytes[4] == PortIoStubPlan.RetOpcode;
    }

    private static ExistingIn8Out8StubPageStatus
        ValidateInitialProtection(
            OS.Kernel.Memory.PageProtection protection)
    {
        if (IsWritableExecutable(protection))
        {
            return ExistingIn8Out8StubPageStatus
                .WritableExecutableRejected;
        }

        return protection == ReadWriteProtection
            ? ExistingIn8Out8StubPageStatus.Success
            : ExistingIn8Out8StubPageStatus
                .InvalidInitialProtection;
    }

    private static bool IsWritableExecutable(
        OS.Kernel.Memory.PageProtection protection)
    {
        byte value = (byte)protection;
        return (value & (byte)OS.Kernel.Memory.PageProtection.Write)
                != 0
            && (value
                & (byte)OS.Kernel.Memory.PageProtection.Execute)
                != 0;
    }

    private static bool TryGetEnd(
        ulong start,
        ulong length,
        out ulong end)
    {
        end = 0;
        if (start > ~0UL - length)
        {
            return false;
        }

        end = start + length;
        return true;
    }

    private static bool RangesOverlap(
        ulong firstStart,
        ulong firstEnd,
        ulong secondStart,
        ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static bool StateOverlapsStorage(
        ExistingIn8Out8StubPageState* state,
        byte* storage,
        ulong storageCapacity)
    {
        ulong stateStart = (ulong)state;
        ulong storageStart = (ulong)storage;
        ulong stateEnd =
            stateStart + ExistingIn8Out8StubPageState.StructureSize;
        ulong storageEnd = storageStart + storageCapacity;
        return RangesOverlap(
            stateStart,
            stateEnd,
            storageStart,
            storageEnd);
    }
}
