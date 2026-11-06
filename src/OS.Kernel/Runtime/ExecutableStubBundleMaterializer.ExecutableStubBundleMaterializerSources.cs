using OS.Kernel.Diagnostics;
using OS.Std.Memory;

namespace OS.Kernel.Runtime;

public readonly ref struct ExecutableStubBundleMaterializerSources
{
    public ExecutableStubBundleMaterializerSources(ReadOnlySpan<byte> com1Out8, ReadOnlySpan<byte> com1In8, ReadOnlySpan<byte> installIdt, ReadOnlySpan<byte> installGdt, ReadOnlySpan<byte> loadTaskRegister, ReadOnlySpan<byte> invalidOpcodeProbe, ReadOnlySpan<byte> nullWriteProbe)
    {
        Com1Out8 = com1Out8;
        Com1In8 = com1In8;
        InstallIdt = installIdt;
        InstallGdt = installGdt;
        LoadTaskRegister = loadTaskRegister;
        InvalidOpcodeProbe = invalidOpcodeProbe;
        NullWriteProbe = nullWriteProbe;
    }

    public ReadOnlySpan<byte> Com1Out8
    {
        get;
    }
    public ReadOnlySpan<byte> Com1In8
    {
        get;
    }
    public ReadOnlySpan<byte> InstallIdt
    {
        get;
    }
    public ReadOnlySpan<byte> InstallGdt
    {
        get;
    }
    public ReadOnlySpan<byte> LoadTaskRegister
    {
        get;
    }
    public ReadOnlySpan<byte> InvalidOpcodeProbe
    {
        get;
    }
    public ReadOnlySpan<byte> NullWriteProbe
    {
        get;
    }
}
