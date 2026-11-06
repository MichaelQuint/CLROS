namespace OS.Kernel.Diagnostics;

public readonly struct Com1OwnedPortIoConfigurationResult
{
    private Com1OwnedPortIoConfigurationResult(bool isInitialized, Com1OwnedPortIoConfigurationStatus status, int writesCompleted, int requiredWrites, int failedWriteIndex, Com1OwnedPortIoOperationResult lastOperation)
    {
        IsInitialized = isInitialized;
        Status = status;
        WritesCompleted = writesCompleted;
        RequiredWrites = requiredWrites;
        FailedWriteIndex = failedWriteIndex;
        LastOperation = lastOperation;
    }

    public bool IsInitialized
    {
        get;
    }
    public Com1OwnedPortIoConfigurationStatus Status
    {
        get;
    }
    public int WritesCompleted
    {
        get;
    }
    public int RequiredWrites
    {
        get;
    }
    public int FailedWriteIndex
    {
        get;
    }
    public Com1OwnedPortIoOperationResult LastOperation
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == Com1OwnedPortIoConfigurationStatus.Success && RequiredWrites == Com1OwnedPortIoBackend.RequiredConfigurationWriteCount && WritesCompleted == RequiredWrites && FailedWriteIndex == -1 && LastOperation.Succeeded;

    internal static Com1OwnedPortIoConfigurationResult Pass(int writesCompleted, Com1OwnedPortIoOperationResult lastOperation)
    {
        return new Com1OwnedPortIoConfigurationResult(true, Com1OwnedPortIoConfigurationStatus.Success, writesCompleted, Com1OwnedPortIoBackend.RequiredConfigurationWriteCount, -1, lastOperation);
    }

    internal static Com1OwnedPortIoConfigurationResult Fail(Com1OwnedPortIoConfigurationStatus status, int writesCompleted, int failedWriteIndex, Com1OwnedPortIoOperationResult lastOperation)
    {
        return new Com1OwnedPortIoConfigurationResult(true, status, writesCompleted, Com1OwnedPortIoBackend.RequiredConfigurationWriteCount, failedWriteIndex, lastOperation);
    }
}
