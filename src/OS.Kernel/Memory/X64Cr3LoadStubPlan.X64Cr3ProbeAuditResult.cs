namespace OS.Kernel.Memory;
public readonly struct X64Cr3ProbeAuditResult
{
    public const ulong StructureSize = 64;
    private readonly X64Cr3LoadStubPlanStatus _status;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly ulong _eventCount;
    private readonly ulong _eventCapacity;
    private readonly ulong _validatedEventCount;
    private readonly ulong _invalidEventIndex;
    private readonly ulong _finalRawCr3;
    private readonly ulong _finalCanaryValue;
    private readonly ulong _elapsedTicks;
    private X64Cr3ProbeAuditResult(X64Cr3LoadStubPlanStatus status, ulong eventCount, ulong eventCapacity, ulong validatedEventCount, ulong invalidEventIndex, ulong finalRawCr3, ulong finalCanaryValue, ulong elapsedTicks)
    {
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _eventCount = eventCount;
        _eventCapacity = eventCapacity;
        _validatedEventCount = validatedEventCount;
        _invalidEventIndex = invalidEventIndex;
        _finalRawCr3 = finalRawCr3;
        _finalCanaryValue = finalCanaryValue;
        _elapsedTicks = elapsedTicks;
    }

    public X64Cr3LoadStubPlanStatus Status => _status;
    public bool Succeeded => _status == X64Cr3LoadStubPlanStatus.Success && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _eventCount == X64Cr3LoadStubPlan.RequiredEventCount && _eventCapacity >= _eventCount && _eventCapacity <= X64Cr3LoadStubPlan.MaximumEventCount && _validatedEventCount == X64Cr3LoadStubPlan.RequiredEventCount && _invalidEventIndex == X64Cr3LoadStubPlan.NoIndex && _finalRawCr3 != 0 && _finalCanaryValue != 0;
    public ulong EventCount => _eventCount;
    public ulong EventCapacity => _eventCapacity;
    public ulong ValidatedEventCount => _validatedEventCount;
    public ulong InvalidEventIndex => _invalidEventIndex;
    public ulong FinalRawCr3 => _finalRawCr3;
    public ulong FinalCanaryValue => _finalCanaryValue;
    public ulong ElapsedTicks => _elapsedTicks;

    internal static X64Cr3ProbeAuditResult Create(X64Cr3LoadStubPlanStatus status, ulong eventCount, ulong eventCapacity, ulong validatedEventCount, ulong invalidEventIndex, ulong finalRawCr3, ulong finalCanaryValue, ulong elapsedTicks)
    {
        return new X64Cr3ProbeAuditResult(status, eventCount, eventCapacity, validatedEventCount, invalidEventIndex, finalRawCr3, finalCanaryValue, elapsedTicks);
    }
}
