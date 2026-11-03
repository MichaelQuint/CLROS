using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public readonly struct Com1PortIoBackendPlan
{
    public const int RequiredExecutableStubBytes = PortIoStubPlan.Out8RequiredByteLength + PortIoStubPlan.In8RequiredByteLength;
    public Com1PortIoBackendPlan(SerialPortConfiguration serialConfiguration, PortIoOperationPlan out8OperationPlan, PortIoOperationPlan in8OperationPlan, PortIoStubPlan out8StubPlan, PortIoStubPlan in8StubPlan, ExecutableStubMemoryPlanResult executableStubMemoryPlan)
    {
        SerialConfiguration = serialConfiguration;
        Out8OperationPlan = out8OperationPlan;
        In8OperationPlan = in8OperationPlan;
        Out8StubPlan = out8StubPlan;
        In8StubPlan = in8StubPlan;
        ExecutableStubMemoryPlan = executableStubMemoryPlan;
    }

    public SerialPortConfiguration SerialConfiguration
    {
        get;
    }
    public PortIoOperationPlan Out8OperationPlan
    {
        get;
    }
    public PortIoOperationPlan In8OperationPlan
    {
        get;
    }
    public PortIoStubPlan Out8StubPlan
    {
        get;
    }
    public PortIoStubPlan In8StubPlan
    {
        get;
    }
    public ExecutableStubMemoryPlanResult ExecutableStubMemoryPlan
    {
        get;
    }

    public static Com1PortIoBackendPlan CreateReady()
    {
        return CreateReady(OS.Kernel.Runtime.ExecutableStubMemoryPlan.Create(RequiredExecutableStubBytes, OS.Kernel.Runtime.ExecutableStubMemoryPlan.DefaultPageSize, OS.Kernel.Runtime.ExecutableStubMemoryPlan.DefaultAlignment));
    }

    public static Com1PortIoBackendPlan CreateReady(ExecutableStubMemoryPlanResult executableStubMemoryPlan)
    {
        return new Com1PortIoBackendPlan(SerialPortConfiguration.Com1Default, PortIoOperationPlan.Out8, PortIoOperationPlan.In8, PortIoStubPlan.Out8, PortIoStubPlan.In8, executableStubMemoryPlan);
    }

    public Com1PortIoBackendPlanResult Validate()
    {
        SerialPortConfigurationStatus serialStatus = ValidateSerialConfiguration();
        if (serialStatus != SerialPortConfigurationStatus.Success)
        {
            return Com1PortIoBackendPlanResult.InvalidSerialConfiguration(serialStatus, RequiredExecutableStubBytes, GetAvailableStubBytes());
        }

        PortIoOperationPlanStatus outOperationStatus = ValidateOperationPlan(Out8OperationPlan, PortIoOperationKind.Out8);
        if (outOperationStatus != PortIoOperationPlanStatus.Success)
        {
            return Com1PortIoBackendPlanResult.MissingOperationPlan(outOperationStatus, RequiredExecutableStubBytes, GetAvailableStubBytes());
        }

        PortIoOperationPlanStatus inOperationStatus = ValidateOperationPlan(In8OperationPlan, PortIoOperationKind.In8);
        if (inOperationStatus != PortIoOperationPlanStatus.Success)
        {
            return Com1PortIoBackendPlanResult.MissingOperationPlan(inOperationStatus, RequiredExecutableStubBytes, GetAvailableStubBytes());
        }

        PortIoStubPlanStatus outStubStatus = ValidateStubPlan(Out8StubPlan, PortIoOperationKind.Out8, PortIoStubPlan.Out8RequiredByteLength);
        if (outStubStatus != PortIoStubPlanStatus.Success)
        {
            return Com1PortIoBackendPlanResult.MissingStubPlan(outStubStatus, RequiredExecutableStubBytes, GetAvailableStubBytes());
        }

        PortIoStubPlanStatus inStubStatus = ValidateStubPlan(In8StubPlan, PortIoOperationKind.In8, PortIoStubPlan.In8RequiredByteLength);
        if (inStubStatus != PortIoStubPlanStatus.Success)
        {
            return Com1PortIoBackendPlanResult.MissingStubPlan(inStubStatus, RequiredExecutableStubBytes, GetAvailableStubBytes());
        }

        if (!ExecutableStubMemoryPlan.Succeeded)
        {
            return Com1PortIoBackendPlanResult.StubMemoryPlanUnavailable(ExecutableStubMemoryPlan.Status, RequiredExecutableStubBytes);
        }

        nuint availableStubBytes = GetAvailableStubBytes();
        if (availableStubBytes < (nuint)RequiredExecutableStubBytes)
        {
            return Com1PortIoBackendPlanResult.StubMemoryPlanTooSmall(RequiredExecutableStubBytes, availableStubBytes);
        }

        return Com1PortIoBackendPlanResult.Success(RequiredExecutableStubBytes, availableStubBytes);
    }

    private SerialPortConfigurationStatus ValidateSerialConfiguration()
    {
        Span<SerialPortWrite> writes = stackalloc SerialPortWrite[SerialPortConfiguration.RequiredInitializationWriteCount];
        SerialPortInitializationPlanResult result = SerialConfiguration.EmitInitializationSequence(writes);
        if (!result.Succeeded)
        {
            return result.Status;
        }

        return result.WritesEmitted == SerialPortConfiguration.RequiredInitializationWriteCount ? SerialPortConfigurationStatus.Success : SerialPortConfigurationStatus.OutputBufferTooSmall;
    }

    private static PortIoOperationPlanStatus ValidateOperationPlan(PortIoOperationPlan plan, PortIoOperationKind expectedOperationKind)
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

        byte expectedOpcode = expectedOperationKind == PortIoOperationKind.Out8 ? PortIoOperationPlan.Out8Opcode : PortIoOperationPlan.In8Opcode;
        return result.BytesWritten == PortIoOperationPlan.RequiredByteLength && result.RequiredBytes == PortIoOperationPlan.RequiredByteLength && bytes[0] == expectedOpcode ? PortIoOperationPlanStatus.Success : PortIoOperationPlanStatus.InvalidOperationKind;
    }

    private static PortIoStubPlanStatus ValidateStubPlan(PortIoStubPlan plan, PortIoOperationKind expectedOperationKind, int expectedRequiredBytes)
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

        if (result.BytesWritten != expectedRequiredBytes || result.RequiredBytes != expectedRequiredBytes)
        {
            return PortIoStubPlanStatus.InvalidOperationKind;
        }

        if (expectedOperationKind == PortIoOperationKind.Out8)
        {
            return bytes[0] == PortIoStubPlan.MovR8ToRm8Opcode && bytes[1] == PortIoStubPlan.MovDlToAlModRm && bytes[2] == PortIoStubPlan.OperandSizeOverridePrefix && bytes[3] == PortIoStubPlan.MovR16ToRm16Opcode && bytes[4] == PortIoStubPlan.MovCxToDxModRm && bytes[5] == PortIoOperationPlan.Out8Opcode && bytes[6] == PortIoStubPlan.RetOpcode ? PortIoStubPlanStatus.Success : PortIoStubPlanStatus.InvalidOperationKind;
        }

        return bytes[0] == PortIoStubPlan.OperandSizeOverridePrefix && bytes[1] == PortIoStubPlan.MovR16ToRm16Opcode && bytes[2] == PortIoStubPlan.MovCxToDxModRm && bytes[3] == PortIoOperationPlan.In8Opcode && bytes[4] == PortIoStubPlan.RetOpcode ? PortIoStubPlanStatus.Success : PortIoStubPlanStatus.InvalidOperationKind;
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
