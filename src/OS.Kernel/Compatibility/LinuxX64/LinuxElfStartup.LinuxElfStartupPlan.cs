using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Text;

namespace OS.Kernel.Compatibility.LinuxX64;
public struct LinuxElfStartupPlan
{
    public LinuxElfStartupStatus Status;
    public uint ArgumentCount, EnvironmentCount, UsedBytes;
    public ulong StackStart, StackBytes, StackPointer, RandomAddress, ExecutableName, ProgramHeaders, Entry;
    public readonly bool Succeeded => Status == LinuxElfStartupStatus.Ready;
}
