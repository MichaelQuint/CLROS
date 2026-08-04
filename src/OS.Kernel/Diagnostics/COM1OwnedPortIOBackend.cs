namespace OS.Kernel.Diagnostics;

public enum Com1OwnedPortIoOperationStatus : byte
{
    Success = 0,
    BindingNotReady = 1,
    PortOutsideConfiguredRange = 2,
    InvocationRejected = 3
}

public readonly struct Com1OwnedPortIoOperationResult
{
    private Com1OwnedPortIoOperationResult(
        bool isInitialized,
        Com1OwnedPortIoOperationStatus status,
        CheckedPortIoFunctionPointerBindingStatus bindingStatus,
        CheckedPortIoFunctionPointerInvocationStatus invocationStatus,
        PortIoOperationKind operationKind,
        ushort port,
        byte value,
        bool wasInvoked)
    {
        IsInitialized = isInitialized;
        Status = status;
        BindingStatus = bindingStatus;
        InvocationStatus = invocationStatus;
        OperationKind = operationKind;
        Port = port;
        Value = value;
        WasInvoked = wasInvoked;
    }

    public bool IsInitialized { get; }

    public Com1OwnedPortIoOperationStatus Status { get; }

    public CheckedPortIoFunctionPointerBindingStatus BindingStatus { get; }

    public CheckedPortIoFunctionPointerInvocationStatus InvocationStatus { get; }

    public PortIoOperationKind OperationKind { get; }

    public ushort Port { get; }

    public byte Value { get; }

    public bool WasInvoked { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == Com1OwnedPortIoOperationStatus.Success &&
        BindingStatus == CheckedPortIoFunctionPointerBindingStatus.Ready &&
        InvocationStatus == CheckedPortIoFunctionPointerInvocationStatus.Success &&
        (OperationKind == PortIoOperationKind.Out8 || OperationKind == PortIoOperationKind.In8) &&
        WasInvoked;

    internal static Com1OwnedPortIoOperationResult Pass(
        CheckedPortIoFunctionPointerInvocationResult invocation)
    {
        return new Com1OwnedPortIoOperationResult(
            true,
            Com1OwnedPortIoOperationStatus.Success,
            CheckedPortIoFunctionPointerBindingStatus.Ready,
            invocation.Status,
            invocation.OperationKind,
            invocation.Port,
            invocation.Value,
            invocation.WasInvoked);
    }

    internal static Com1OwnedPortIoOperationResult Fail(
        Com1OwnedPortIoOperationStatus status,
        CheckedPortIoFunctionPointerBindingStatus bindingStatus,
        CheckedPortIoFunctionPointerInvocationStatus invocationStatus,
        PortIoOperationKind operationKind,
        ushort port,
        byte value)
    {
        return new Com1OwnedPortIoOperationResult(
            true,
            status,
            bindingStatus,
            invocationStatus,
            operationKind,
            port,
            value,
            false);
    }
}

public enum Com1OwnedPortIoConfigurationStatus : byte
{
    Success = 0,
    BackendNotReady = 1,
    ConfigurationWriteFailed = 2
}

public readonly struct Com1OwnedPortIoConfigurationResult
{
    private Com1OwnedPortIoConfigurationResult(
        bool isInitialized,
        Com1OwnedPortIoConfigurationStatus status,
        int writesCompleted,
        int requiredWrites,
        int failedWriteIndex,
        Com1OwnedPortIoOperationResult lastOperation)
    {
        IsInitialized = isInitialized;
        Status = status;
        WritesCompleted = writesCompleted;
        RequiredWrites = requiredWrites;
        FailedWriteIndex = failedWriteIndex;
        LastOperation = lastOperation;
    }

    public bool IsInitialized { get; }

    public Com1OwnedPortIoConfigurationStatus Status { get; }

    public int WritesCompleted { get; }

    public int RequiredWrites { get; }

    public int FailedWriteIndex { get; }

    public Com1OwnedPortIoOperationResult LastOperation { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == Com1OwnedPortIoConfigurationStatus.Success &&
        RequiredWrites == Com1OwnedPortIoBackend.RequiredConfigurationWriteCount &&
        WritesCompleted == RequiredWrites &&
        FailedWriteIndex == -1 &&
        LastOperation.Succeeded;

    internal static Com1OwnedPortIoConfigurationResult Pass(
        int writesCompleted,
        Com1OwnedPortIoOperationResult lastOperation)
    {
        return new Com1OwnedPortIoConfigurationResult(
            true,
            Com1OwnedPortIoConfigurationStatus.Success,
            writesCompleted,
            Com1OwnedPortIoBackend.RequiredConfigurationWriteCount,
            -1,
            lastOperation);
    }

    internal static Com1OwnedPortIoConfigurationResult Fail(
        Com1OwnedPortIoConfigurationStatus status,
        int writesCompleted,
        int failedWriteIndex,
        Com1OwnedPortIoOperationResult lastOperation)
    {
        return new Com1OwnedPortIoConfigurationResult(
            true,
            status,
            writesCompleted,
            Com1OwnedPortIoBackend.RequiredConfigurationWriteCount,
            failedWriteIndex,
            lastOperation);
    }
}

public readonly struct Com1OwnedPortIoBackend
{
    public const ushort BasePort = InvokeIn8Out8CheckedUnmanagedFunctionPointers.RequiredCom1BasePort;
    public const ushort EndPort = BasePort + InvokeIn8Out8CheckedUnmanagedFunctionPointers.MaximumCom1RegisterOffset;

    public const ushort DataRegisterPort = BasePort + 0;
    public const ushort InterruptEnableRegisterPort = BasePort + 1;
    public const ushort FifoControlRegisterPort = BasePort + 2;
    public const ushort LineControlRegisterPort = BasePort + 3;
    public const ushort ModemControlRegisterPort = BasePort + 4;
    public const ushort LineStatusRegisterPort = BasePort + 5;

    public const byte DisableInterruptsValue = 0x00;
    public const byte EnableDivisorLatchAccessValue = 0x80;
    public const byte DefaultDivisorLowValue = 0x01;
    public const byte DefaultDivisorHighValue = 0x00;
    public const byte LineControl8N1Value = 0x03;
    public const byte FifoControlEnableAndClearValue = 0xc7;
    public const byte ModemControlReadyValue = 0x0b;

    public const int RequiredConfigurationWriteCount = 7;

    private readonly CheckedPortIoFunctionPointerBindingResult _binding;

    public Com1OwnedPortIoBackend(
        CheckedPortIoFunctionPointerBindingResult binding)
    {
        _binding = binding;
    }

    public CheckedPortIoFunctionPointerBindingStatus BindingStatus => _binding.Status;

    public bool IsReady =>
        _binding.Succeeded &&
        _binding.ConfiguredBasePort == BasePort &&
        _binding.ConfiguredEndPort == EndPort;

    public Com1OwnedPortIoOperationResult WriteByte(ushort port, byte value)
    {
        if (!IsReady)
        {
            return Com1OwnedPortIoOperationResult.Fail(
                Com1OwnedPortIoOperationStatus.BindingNotReady,
                _binding.Status,
                CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady,
                PortIoOperationKind.Out8,
                port,
                value);
        }

        CheckedPortIoFunctionPointerInvocationResult invocation =
            _binding.InvokeOut8(port, value);
        if (!invocation.Succeeded)
        {
            return Com1OwnedPortIoOperationResult.Fail(
                MapInvocationFailure(invocation.Status),
                _binding.Status,
                invocation.Status,
                PortIoOperationKind.Out8,
                port,
                value);
        }

        return Com1OwnedPortIoOperationResult.Pass(invocation);
    }

    public Com1OwnedPortIoOperationResult ReadByte(ushort port)
    {
        if (!IsReady)
        {
            return Com1OwnedPortIoOperationResult.Fail(
                Com1OwnedPortIoOperationStatus.BindingNotReady,
                _binding.Status,
                CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady,
                PortIoOperationKind.In8,
                port,
                0);
        }

        CheckedPortIoFunctionPointerInvocationResult invocation =
            _binding.InvokeIn8(port);
        if (!invocation.Succeeded)
        {
            return Com1OwnedPortIoOperationResult.Fail(
                MapInvocationFailure(invocation.Status),
                _binding.Status,
                invocation.Status,
                PortIoOperationKind.In8,
                port,
                0);
        }

        return Com1OwnedPortIoOperationResult.Pass(invocation);
    }

    public Com1OwnedPortIoConfigurationResult ConfigureDefault()
    {
        int writesCompleted = 0;
        Com1OwnedPortIoOperationResult lastOperation = default;

        for (int index = 0; index < RequiredConfigurationWriteCount; index++)
        {
            GetConfigurationWrite(index, out ushort port, out byte value);
            lastOperation = WriteByte(port, value);
            if (!lastOperation.Succeeded)
            {
                Com1OwnedPortIoConfigurationStatus status =
                    lastOperation.Status == Com1OwnedPortIoOperationStatus.BindingNotReady
                        ? Com1OwnedPortIoConfigurationStatus.BackendNotReady
                        : Com1OwnedPortIoConfigurationStatus.ConfigurationWriteFailed;

                return Com1OwnedPortIoConfigurationResult.Fail(
                    status,
                    writesCompleted,
                    index,
                    lastOperation);
            }

            writesCompleted++;
        }

        return Com1OwnedPortIoConfigurationResult.Pass(
            writesCompleted,
            lastOperation);
    }

    private static Com1OwnedPortIoOperationStatus MapInvocationFailure(
        CheckedPortIoFunctionPointerInvocationStatus status)
    {
        if (status == CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady)
        {
            return Com1OwnedPortIoOperationStatus.BindingNotReady;
        }

        if (status == CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange)
        {
            return Com1OwnedPortIoOperationStatus.PortOutsideConfiguredRange;
        }

        return Com1OwnedPortIoOperationStatus.InvocationRejected;
    }

    private static void GetConfigurationWrite(
        int index,
        out ushort port,
        out byte value)
    {
        switch (index)
        {
            case 0:
                port = InterruptEnableRegisterPort;
                value = DisableInterruptsValue;
                return;
            case 1:
                port = LineControlRegisterPort;
                value = EnableDivisorLatchAccessValue;
                return;
            case 2:
                port = DataRegisterPort;
                value = DefaultDivisorLowValue;
                return;
            case 3:
                port = InterruptEnableRegisterPort;
                value = DefaultDivisorHighValue;
                return;
            case 4:
                port = LineControlRegisterPort;
                value = LineControl8N1Value;
                return;
            case 5:
                port = FifoControlRegisterPort;
                value = FifoControlEnableAndClearValue;
                return;
            default:
                port = ModemControlRegisterPort;
                value = ModemControlReadyValue;
                return;
        }
    }
}

#if !NOSTDLIB_EFI
public sealed class Com1OwnedPortIoByteBackend : IBytePortBackend
{
    private readonly Com1OwnedPortIoBackend _backend;

    public Com1OwnedPortIoByteBackend(Com1OwnedPortIoBackend backend)
    {
        _backend = backend;
    }

    public Com1OwnedPortIoOperationResult LastOperation { get; private set; }

    public bool TryReadByte(ushort port, out byte value)
    {
        LastOperation = _backend.ReadByte(port);
        value = LastOperation.Value;
        return LastOperation.Succeeded;
    }

    public bool TryWriteByte(ushort port, byte value)
    {
        LastOperation = _backend.WriteByte(port, value);
        return LastOperation.Succeeded;
    }
}
#endif
