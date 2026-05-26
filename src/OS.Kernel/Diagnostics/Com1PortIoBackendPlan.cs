using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum Com1PortIoBackendPlanStatus
{
    Success = 0,
    InvalidSerialConfiguration = 1,
    MissingOperationPlan = 2,
    MissingStubPlan = 3,
    StubMemoryPlanUnavailable = 4,
    StubMemoryPlanTooSmall = 5
}

public readonly struct Com1PortIoBackendPlanResult
{
    private Com1PortIoBackendPlanResult(
        bool isInitialized,
        Com1PortIoBackendPlanStatus status,
        SerialPortConfigurationStatus serialConfigurationStatus,
        PortIoOperationPlanStatus operationPlanStatus,
        PortIoStubPlanStatus stubPlanStatus,
        ExecutableStubMemoryPlanStatus stubMemoryPlanStatus,
        int requiredStubBytes,
        nuint availableStubBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        SerialConfigurationStatus = serialConfigurationStatus;
        OperationPlanStatus = operationPlanStatus;
        StubPlanStatus = stubPlanStatus;
        StubMemoryPlanStatus = stubMemoryPlanStatus;
        RequiredStubBytes = requiredStubBytes;
        AvailableStubBytes = availableStubBytes;
    }

    public bool IsInitialized { get; }

    public Com1PortIoBackendPlanStatus Status { get; }

    public SerialPortConfigurationStatus SerialConfigurationStatus { get; }

    public PortIoOperationPlanStatus OperationPlanStatus { get; }

    public PortIoStubPlanStatus StubPlanStatus { get; }

    public ExecutableStubMemoryPlanStatus StubMemoryPlanStatus { get; }

    public int RequiredStubBytes { get; }

    public nuint AvailableStubBytes { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == Com1PortIoBackendPlanStatus.Success &&
        SerialConfigurationStatus == SerialPortConfigurationStatus.Success &&
        OperationPlanStatus == PortIoOperationPlanStatus.Success &&
        StubPlanStatus == PortIoStubPlanStatus.Success &&
        StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success &&
        RequiredStubBytes > 0 &&
        AvailableStubBytes >= (nuint)RequiredStubBytes;

    public static Com1PortIoBackendPlanResult Success(int requiredStubBytes, nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(
            true,
            Com1PortIoBackendPlanStatus.Success,
            SerialPortConfigurationStatus.Success,
            PortIoOperationPlanStatus.Success,
            PortIoStubPlanStatus.Success,
            ExecutableStubMemoryPlanStatus.Success,
            requiredStubBytes,
            availableStubBytes);
    }

    public static Com1PortIoBackendPlanResult InvalidSerialConfiguration(
        SerialPortConfigurationStatus serialConfigurationStatus,
        int requiredStubBytes,
        nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(
            true,
            Com1PortIoBackendPlanStatus.InvalidSerialConfiguration,
            serialConfigurationStatus,
            PortIoOperationPlanStatus.Success,
            PortIoStubPlanStatus.Success,
            ExecutableStubMemoryPlanStatus.Success,
            requiredStubBytes,
            availableStubBytes);
    }

    public static Com1PortIoBackendPlanResult MissingOperationPlan(
        PortIoOperationPlanStatus operationPlanStatus,
        int requiredStubBytes,
        nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(
            true,
            Com1PortIoBackendPlanStatus.MissingOperationPlan,
            SerialPortConfigurationStatus.Success,
            operationPlanStatus,
            PortIoStubPlanStatus.Success,
            ExecutableStubMemoryPlanStatus.Success,
            requiredStubBytes,
            availableStubBytes);
    }

    public static Com1PortIoBackendPlanResult MissingStubPlan(
        PortIoStubPlanStatus stubPlanStatus,
        int requiredStubBytes,
        nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(
            true,
            Com1PortIoBackendPlanStatus.MissingStubPlan,
            SerialPortConfigurationStatus.Success,
            PortIoOperationPlanStatus.Success,
            stubPlanStatus,
            ExecutableStubMemoryPlanStatus.Success,
            requiredStubBytes,
            availableStubBytes);
    }

    public static Com1PortIoBackendPlanResult StubMemoryPlanUnavailable(
        ExecutableStubMemoryPlanStatus stubMemoryPlanStatus,
        int requiredStubBytes)
    {
        return new Com1PortIoBackendPlanResult(
            true,
            Com1PortIoBackendPlanStatus.StubMemoryPlanUnavailable,
            SerialPortConfigurationStatus.Success,
            PortIoOperationPlanStatus.Success,
            PortIoStubPlanStatus.Success,
            stubMemoryPlanStatus,
            requiredStubBytes,
            0);
    }

    public static Com1PortIoBackendPlanResult StubMemoryPlanTooSmall(
        int requiredStubBytes,
        nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(
            true,
            Com1PortIoBackendPlanStatus.StubMemoryPlanTooSmall,
            SerialPortConfigurationStatus.Success,
            PortIoOperationPlanStatus.Success,
            PortIoStubPlanStatus.Success,
            ExecutableStubMemoryPlanStatus.Success,
            requiredStubBytes,
            availableStubBytes);
    }
}

public readonly struct Com1PortIoBackendPlan
{
    public const int RequiredExecutableStubBytes =
        PortIoStubPlan.Out8RequiredByteLength + PortIoStubPlan.In8RequiredByteLength;

    public Com1PortIoBackendPlan(
        SerialPortConfiguration serialConfiguration,
        PortIoOperationPlan out8OperationPlan,
        PortIoOperationPlan in8OperationPlan,
        PortIoStubPlan out8StubPlan,
        PortIoStubPlan in8StubPlan,
        ExecutableStubMemoryPlanResult executableStubMemoryPlan)
    {
        SerialConfiguration = serialConfiguration;
        Out8OperationPlan = out8OperationPlan;
        In8OperationPlan = in8OperationPlan;
        Out8StubPlan = out8StubPlan;
        In8StubPlan = in8StubPlan;
        ExecutableStubMemoryPlan = executableStubMemoryPlan;
    }

    public SerialPortConfiguration SerialConfiguration { get; }

    public PortIoOperationPlan Out8OperationPlan { get; }

    public PortIoOperationPlan In8OperationPlan { get; }

    public PortIoStubPlan Out8StubPlan { get; }

    public PortIoStubPlan In8StubPlan { get; }

    public ExecutableStubMemoryPlanResult ExecutableStubMemoryPlan { get; }

    public static Com1PortIoBackendPlan CreateReady()
    {
        return CreateReady(
            OS.Kernel.Runtime.ExecutableStubMemoryPlan.Create(
                RequiredExecutableStubBytes,
                OS.Kernel.Runtime.ExecutableStubMemoryPlan.DefaultPageSize,
                OS.Kernel.Runtime.ExecutableStubMemoryPlan.DefaultAlignment));
    }

    public static Com1PortIoBackendPlan CreateReady(
        ExecutableStubMemoryPlanResult executableStubMemoryPlan)
    {
        return new Com1PortIoBackendPlan(
            SerialPortConfiguration.Com1Default,
            PortIoOperationPlan.Out8,
            PortIoOperationPlan.In8,
            PortIoStubPlan.Out8,
            PortIoStubPlan.In8,
            executableStubMemoryPlan);
    }

    public Com1PortIoBackendPlanResult Validate()
    {
        SerialPortConfigurationStatus serialStatus = ValidateSerialConfiguration();
        if (serialStatus != SerialPortConfigurationStatus.Success)
        {
            return Com1PortIoBackendPlanResult.InvalidSerialConfiguration(
                serialStatus,
                RequiredExecutableStubBytes,
                GetAvailableStubBytes());
        }

        PortIoOperationPlanStatus outOperationStatus =
            ValidateOperationPlan(Out8OperationPlan, PortIoOperationKind.Out8);
        if (outOperationStatus != PortIoOperationPlanStatus.Success)
        {
            return Com1PortIoBackendPlanResult.MissingOperationPlan(
                outOperationStatus,
                RequiredExecutableStubBytes,
                GetAvailableStubBytes());
        }

        PortIoOperationPlanStatus inOperationStatus =
            ValidateOperationPlan(In8OperationPlan, PortIoOperationKind.In8);
        if (inOperationStatus != PortIoOperationPlanStatus.Success)
        {
            return Com1PortIoBackendPlanResult.MissingOperationPlan(
                inOperationStatus,
                RequiredExecutableStubBytes,
                GetAvailableStubBytes());
        }

        PortIoStubPlanStatus outStubStatus = ValidateStubPlan(
            Out8StubPlan,
            PortIoOperationKind.Out8,
            PortIoStubPlan.Out8RequiredByteLength);
        if (outStubStatus != PortIoStubPlanStatus.Success)
        {
            return Com1PortIoBackendPlanResult.MissingStubPlan(
                outStubStatus,
                RequiredExecutableStubBytes,
                GetAvailableStubBytes());
        }

        PortIoStubPlanStatus inStubStatus = ValidateStubPlan(
            In8StubPlan,
            PortIoOperationKind.In8,
            PortIoStubPlan.In8RequiredByteLength);
        if (inStubStatus != PortIoStubPlanStatus.Success)
        {
            return Com1PortIoBackendPlanResult.MissingStubPlan(
                inStubStatus,
                RequiredExecutableStubBytes,
                GetAvailableStubBytes());
        }

        if (!ExecutableStubMemoryPlan.Succeeded)
        {
            return Com1PortIoBackendPlanResult.StubMemoryPlanUnavailable(
                ExecutableStubMemoryPlan.Status,
                RequiredExecutableStubBytes);
        }

        nuint availableStubBytes = GetAvailableStubBytes();
        if (availableStubBytes < (nuint)RequiredExecutableStubBytes)
        {
            return Com1PortIoBackendPlanResult.StubMemoryPlanTooSmall(
                RequiredExecutableStubBytes,
                availableStubBytes);
        }

        return Com1PortIoBackendPlanResult.Success(
            RequiredExecutableStubBytes,
            availableStubBytes);
    }

    private SerialPortConfigurationStatus ValidateSerialConfiguration()
    {
        Span<SerialPortWrite> writes =
            stackalloc SerialPortWrite[SerialPortConfiguration.RequiredInitializationWriteCount];

        SerialPortInitializationPlanResult result =
            SerialConfiguration.EmitInitializationSequence(writes);

        if (!result.Succeeded)
        {
            return result.Status;
        }

        return result.WritesEmitted == SerialPortConfiguration.RequiredInitializationWriteCount
            ? SerialPortConfigurationStatus.Success
            : SerialPortConfigurationStatus.OutputBufferTooSmall;
    }

    private static PortIoOperationPlanStatus ValidateOperationPlan(
        PortIoOperationPlan plan,
        PortIoOperationKind expectedOperationKind)
    {
        if (plan.OperationKind != expectedOperationKind)
        {
            return PortIoOperationPlanStatus.InvalidOperationKind;
        }

        Span<byte> bytes = stackalloc byte[PortIoOperationPlan.RequiredByteLength];
        PortIoOperationPlanResult result = plan.Emit(bytes);
        if (!result.Succeeded)
        {
            return result.Status;
        }

        byte expectedOpcode = expectedOperationKind == PortIoOperationKind.Out8
            ? PortIoOperationPlan.Out8Opcode
            : PortIoOperationPlan.In8Opcode;

        return result.BytesWritten == PortIoOperationPlan.RequiredByteLength
            && result.RequiredBytes == PortIoOperationPlan.RequiredByteLength
            && bytes[0] == expectedOpcode
                ? PortIoOperationPlanStatus.Success
                : PortIoOperationPlanStatus.InvalidOperationKind;
    }

    private static PortIoStubPlanStatus ValidateStubPlan(
        PortIoStubPlan plan,
        PortIoOperationKind expectedOperationKind,
        int expectedRequiredBytes)
    {
        if (plan.OperationKind != expectedOperationKind)
        {
            return PortIoStubPlanStatus.InvalidOperationKind;
        }

        Span<byte> bytes = stackalloc byte[PortIoStubPlan.MaximumRequiredByteLength];
        PortIoStubPlanResult result = plan.Emit(bytes);
        if (!result.Succeeded)
        {
            return result.Status;
        }

        if (result.BytesWritten != expectedRequiredBytes
            || result.RequiredBytes != expectedRequiredBytes
            || bytes[0] != PortIoStubPlan.OperandSizeOverridePrefix
            || bytes[1] != PortIoStubPlan.MovR16ToRm16Opcode
            || bytes[2] != PortIoStubPlan.MovCxToDxModRm)
        {
            return PortIoStubPlanStatus.InvalidOperationKind;
        }

        if (expectedOperationKind == PortIoOperationKind.Out8)
        {
            return bytes[3] == PortIoStubPlan.MovR8ToRm8Opcode
                && bytes[4] == PortIoStubPlan.MovDlToAlModRm
                && bytes[5] == PortIoOperationPlan.Out8Opcode
                && bytes[6] == PortIoStubPlan.RetOpcode
                    ? PortIoStubPlanStatus.Success
                    : PortIoStubPlanStatus.InvalidOperationKind;
        }

        return bytes[3] == PortIoOperationPlan.In8Opcode
            && bytes[4] == PortIoStubPlan.RetOpcode
                ? PortIoStubPlanStatus.Success
                : PortIoStubPlanStatus.InvalidOperationKind;
    }

    private nuint GetAvailableStubBytes()
    {
        if (ExecutableStubMemoryPlan.RequiredByteLength < ExecutableStubMemoryPlan.PayloadOffset)
        {
            return 0;
        }

        return ExecutableStubMemoryPlan.RequiredByteLength - ExecutableStubMemoryPlan.PayloadOffset;
    }
}
