namespace OS.Kernel.Memory;
/// <summary>
/// One independently authored semantic instruction record. The record binds
/// an instruction kind to an exact byte interval without deriving the
/// semantics from those bytes.
/// </summary>

public readonly struct X64PagingModePreflightInstruction
{
    public const ulong StructureSize = 24;
    private readonly ulong _operand;
    private readonly uint _byteOffset;
    private readonly ushort _byteLength;
    private readonly byte _isInitialized;
    private readonly X64PagingModePreflightInstructionKind _kind;
    private readonly X64PagingModePreflightRegister _destination;
    private readonly X64PagingModePreflightRegister _source;
    private readonly byte _stackDepthSlotsAfter;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    internal X64PagingModePreflightInstruction(X64PagingModePreflightInstructionKind kind, X64PagingModePreflightRegister destination, X64PagingModePreflightRegister source, uint byteOffset, ushort byteLength, ulong operand, byte stackDepthSlotsAfter)
    {
        _operand = operand;
        _byteOffset = byteOffset;
        _byteLength = byteLength;
        _isInitialized = 1;
        _kind = kind;
        _destination = destination;
        _source = source;
        _stackDepthSlotsAfter = stackDepthSlotsAfter;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _kind >= X64PagingModePreflightInstructionKind.PreserveNonvolatileRbx && _kind <= X64PagingModePreflightInstructionKind.Return && _destination >= X64PagingModePreflightRegister.None && _destination <= X64PagingModePreflightRegister.Stack && _source >= X64PagingModePreflightRegister.None && _source <= X64PagingModePreflightRegister.Stack && _byteLength != 0 && _stackDepthSlotsAfter <= 2 && _reserved0 == 0 && _reserved1 == 0;
    public X64PagingModePreflightInstructionKind Kind => _kind;
    public X64PagingModePreflightRegister Destination => _destination;
    public X64PagingModePreflightRegister Source => _source;
    public uint ByteOffset => _byteOffset;
    public ushort ByteLength => _byteLength;
    public ulong Operand => _operand;
    public byte StackDepthSlotsAfter => _stackDepthSlotsAfter;
}
