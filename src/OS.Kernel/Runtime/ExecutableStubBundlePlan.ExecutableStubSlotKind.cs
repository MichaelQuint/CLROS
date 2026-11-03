namespace OS.Kernel.Runtime;

public enum ExecutableStubSlotKind : byte
{
    Unknown = 0,
    Com1Out8 = 1,
    Com1In8 = 2,
    InstallIdt = 3,
    InstallGdt = 4,
    LoadTaskRegister = 5,
    InvalidOpcodeProbe = 6,
    NullWriteProbe = 7
}
