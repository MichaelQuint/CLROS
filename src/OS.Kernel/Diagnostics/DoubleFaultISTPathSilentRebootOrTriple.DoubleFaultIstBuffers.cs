namespace OS.Kernel.Diagnostics;
/// <summary>Four accessible, stable, disjoint caller buffers. No pointer is execution authority.</summary>

public readonly unsafe struct DoubleFaultIstBuffers
{
    public DoubleFaultIstBuffers(byte* context, ulong contextCapacity, byte* stream, ulong streamCapacity, byte* slots, ulong slotCapacity, byte* idt, ulong idtCapacity)
    {
        Context = context;
        ContextCapacity = contextCapacity;
        Stream = stream;
        StreamCapacity = streamCapacity;
        Slots = slots;
        SlotCapacity = slotCapacity;
        Idt = idt;
        IdtCapacity = idtCapacity;
    }

    public byte* Context { get; }
    public ulong ContextCapacity { get; }
    public byte* Stream { get; }
    public ulong StreamCapacity { get; }
    public byte* Slots { get; }
    public ulong SlotCapacity { get; }
    public byte* Idt { get; }
    public ulong IdtCapacity { get; }
}
