namespace OS.Kernel.Memory;
/// <summary>
/// One caller-owned observation in the modeled probe transcript.
/// </summary>

public readonly struct X64Cr3ProbeEvent
{
    public const ulong StructureSize = 40;
    private readonly ulong _observedRawCr3;
    private readonly OS.Boot.VirtualAddress _observedRsp;
    private readonly ulong _observedCanary;
    private readonly ulong _elapsedTicks;
    private readonly byte _isInitialized;
    private readonly X64Cr3ProbeEventKind _kind;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    public X64Cr3ProbeEvent(X64Cr3ProbeEventKind kind, ulong observedRawCr3, OS.Boot.VirtualAddress observedRsp, ulong observedCanary, ulong elapsedTicks)
    {
        _observedRawCr3 = observedRawCr3;
        _observedRsp = observedRsp;
        _observedCanary = observedCanary;
        _elapsedTicks = elapsedTicks;
        _isInitialized = 1;
        _kind = kind;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _kind >= X64Cr3ProbeEventKind.PreMarkerEmitted && _kind <= X64Cr3ProbeEventKind.TripleFault && _observedRsp.IsValid && _reserved0 == 0 && _reserved1 == 0;
    public X64Cr3ProbeEventKind Kind => _kind;
    public ulong ObservedRawCr3 => _observedRawCr3;
    public OS.Boot.VirtualAddress ObservedRsp => _observedRsp;
    public ulong ObservedCanary => _observedCanary;
    public ulong ElapsedTicks => _elapsedTicks;
}
