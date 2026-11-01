namespace OS.Kernel.Memory;
/// <summary>
/// Caller request to bind one verified stub entry to one exact invalidation
/// target. The request is a call plan, not execution evidence.
/// </summary>

public readonly struct X64InvlpgInvocationRequest
{
    public const ulong StructureSize = 56;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved0;
    private readonly OS.Boot.VirtualAddress _entryAddress;
    private readonly X64TlbInvalidationRecord _target;
    private readonly PageProtection _codeProtection;
    private readonly byte _reserved1;
    private readonly ushort _reserved2;
    private readonly uint _reserved3;
    private readonly ulong _invocationCount;
    private X64InvlpgInvocationRequest(OS.Boot.VirtualAddress entryAddress, X64TlbInvalidationRecord target, PageProtection codeProtection, ulong invocationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved0 = 0;
        _entryAddress = entryAddress;
        _target = target;
        _codeProtection = codeProtection;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _invocationCount = invocationCount;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0;
    public OS.Boot.VirtualAddress EntryAddress => _entryAddress;
    public X64TlbInvalidationRecord Target => _target;
    public PageProtection CodeProtection => _codeProtection;
    public ulong InvocationCount => _invocationCount;

    public static X64InvlpgInvocationRequest Create(OS.Boot.VirtualAddress entryAddress, X64TlbInvalidationRecord target, PageProtection codeProtection, ulong invocationCount)
    {
        return new X64InvlpgInvocationRequest(entryAddress, target, codeProtection, invocationCount);
    }

    public X64InvlpgStubPlanStatus Validate()
    {
        if (!IsInitialized || !_entryAddress.IsValid || !_entryAddress.IsAligned(X64InvlpgStubPlan.EntryAlignment) || _target.Validate() != X64TlbInvalidationStatus.Success)
        {
            return X64InvlpgStubPlanStatus.InvalidInvocationRequest;
        }

        if (_invocationCount != 1)
        {
            return X64InvlpgStubPlanStatus.InvocationCountMustBeOne;
        }

        byte protection = (byte)_codeProtection;
        bool writable = (protection & (byte)PageProtection.Write) != 0;
        bool executable = (protection & (byte)PageProtection.Execute) != 0;
        if (writable && executable)
        {
            return X64InvlpgStubPlanStatus.WritableExecutableCodeRejected;
        }

        PageProtection expected = (PageProtection)((byte)PageProtection.Read | (byte)PageProtection.Execute);
        return _codeProtection == expected ? X64InvlpgStubPlanStatus.Success : X64InvlpgStubPlanStatus.InvalidCodeProtection;
    }
}
