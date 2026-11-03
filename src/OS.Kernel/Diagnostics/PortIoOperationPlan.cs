namespace OS.Kernel.Diagnostics;

public readonly struct PortIoOperationPlan
{
    public const byte Out8Opcode = 0xee;
    public const byte In8Opcode = 0xec;
    public const int RequiredByteLength = 1;
    public PortIoOperationPlan(PortIoOperationKind operationKind)
    {
        OperationKind = operationKind;
    }

    public PortIoOperationKind OperationKind
    {
        get;
    }
    public static PortIoOperationPlan Out8 => new PortIoOperationPlan(PortIoOperationKind.Out8);
    public static PortIoOperationPlan In8 => new PortIoOperationPlan(PortIoOperationKind.In8);

    public unsafe PortIoOperationPlanResult Emit(byte* destination, int destinationLength)
    {
        PortIoOperationPlanStatus status = Validate(OperationKind);
        if (status != PortIoOperationPlanStatus.Success)
        {
            return PortIoOperationPlanResult.Fail(status);
        }

        if (destinationLength < RequiredByteLength)
        {
            return PortIoOperationPlanResult.Fail(PortIoOperationPlanStatus.OutputBufferTooSmall);
        }

        if (destination == null)
        {
            return PortIoOperationPlanResult.Fail(PortIoOperationPlanStatus.MissingDestination);
        }

        destination[0] = GetOpcode(OperationKind);
        return PortIoOperationPlanResult.Success(RequiredByteLength);
    }

#if !NOSTDLIB_EFI
    public unsafe PortIoOperationPlanResult Emit(Span<byte> destination)
    {
        fixed (byte* pointer = destination)
        {
            return Emit(pointer, destination.Length);
        }
    }

#endif
    public static PortIoOperationPlanStatus Validate(PortIoOperationKind operationKind)
    {
        if (operationKind == PortIoOperationKind.Out8 || operationKind == PortIoOperationKind.In8)
        {
            return PortIoOperationPlanStatus.Success;
        }

        return PortIoOperationPlanStatus.InvalidOperationKind;
    }

    private static byte GetOpcode(PortIoOperationKind operationKind)
    {
        if (operationKind == PortIoOperationKind.Out8)
        {
            return Out8Opcode;
        }

        return In8Opcode;
    }
}
