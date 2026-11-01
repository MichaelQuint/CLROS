using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public readonly struct Com1PortIoBackendPlanResult
{
    private Com1PortIoBackendPlanResult(bool isInitialized, Com1PortIoBackendPlanStatus status, SerialPortConfigurationStatus serialConfigurationStatus, PortIoOperationPlanStatus operationPlanStatus, PortIoStubPlanStatus stubPlanStatus, ExecutableStubMemoryPlanStatus stubMemoryPlanStatus, int requiredStubBytes, nuint availableStubBytes)
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
    public bool Succeeded => IsInitialized && Status == Com1PortIoBackendPlanStatus.Success && SerialConfigurationStatus == SerialPortConfigurationStatus.Success && OperationPlanStatus == PortIoOperationPlanStatus.Success && StubPlanStatus == PortIoStubPlanStatus.Success && StubMemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success && RequiredStubBytes > 0 && AvailableStubBytes >= (nuint)RequiredStubBytes;

    public static Com1PortIoBackendPlanResult Success(int requiredStubBytes, nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(true, Com1PortIoBackendPlanStatus.Success, SerialPortConfigurationStatus.Success, PortIoOperationPlanStatus.Success, PortIoStubPlanStatus.Success, ExecutableStubMemoryPlanStatus.Success, requiredStubBytes, availableStubBytes);
    }

    public static Com1PortIoBackendPlanResult InvalidSerialConfiguration(SerialPortConfigurationStatus serialConfigurationStatus, int requiredStubBytes, nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(true, Com1PortIoBackendPlanStatus.InvalidSerialConfiguration, serialConfigurationStatus, PortIoOperationPlanStatus.Success, PortIoStubPlanStatus.Success, ExecutableStubMemoryPlanStatus.Success, requiredStubBytes, availableStubBytes);
    }

    public static Com1PortIoBackendPlanResult MissingOperationPlan(PortIoOperationPlanStatus operationPlanStatus, int requiredStubBytes, nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(true, Com1PortIoBackendPlanStatus.MissingOperationPlan, SerialPortConfigurationStatus.Success, operationPlanStatus, PortIoStubPlanStatus.Success, ExecutableStubMemoryPlanStatus.Success, requiredStubBytes, availableStubBytes);
    }

    public static Com1PortIoBackendPlanResult MissingStubPlan(PortIoStubPlanStatus stubPlanStatus, int requiredStubBytes, nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(true, Com1PortIoBackendPlanStatus.MissingStubPlan, SerialPortConfigurationStatus.Success, PortIoOperationPlanStatus.Success, stubPlanStatus, ExecutableStubMemoryPlanStatus.Success, requiredStubBytes, availableStubBytes);
    }

    public static Com1PortIoBackendPlanResult StubMemoryPlanUnavailable(ExecutableStubMemoryPlanStatus stubMemoryPlanStatus, int requiredStubBytes)
    {
        return new Com1PortIoBackendPlanResult(true, Com1PortIoBackendPlanStatus.StubMemoryPlanUnavailable, SerialPortConfigurationStatus.Success, PortIoOperationPlanStatus.Success, PortIoStubPlanStatus.Success, stubMemoryPlanStatus, requiredStubBytes, 0);
    }

    public static Com1PortIoBackendPlanResult StubMemoryPlanTooSmall(int requiredStubBytes, nuint availableStubBytes)
    {
        return new Com1PortIoBackendPlanResult(true, Com1PortIoBackendPlanStatus.StubMemoryPlanTooSmall, SerialPortConfigurationStatus.Success, PortIoOperationPlanStatus.Success, PortIoStubPlanStatus.Success, ExecutableStubMemoryPlanStatus.Success, requiredStubBytes, availableStubBytes);
    }
}
