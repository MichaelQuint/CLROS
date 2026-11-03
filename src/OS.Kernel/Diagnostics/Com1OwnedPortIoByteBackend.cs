namespace OS.Kernel.Diagnostics;
#if !NOSTDLIB_EFI
public sealed class Com1OwnedPortIoByteBackend : IBytePortBackend
{
    private readonly Com1OwnedPortIoBackend _backend;
    public Com1OwnedPortIoByteBackend(Com1OwnedPortIoBackend backend)
    {
        _backend = backend;
    }

    public Com1OwnedPortIoOperationResult LastOperation
    {
        get; private set;
    }

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
