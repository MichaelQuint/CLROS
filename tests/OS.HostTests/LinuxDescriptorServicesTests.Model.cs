using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using Services = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe partial class LinuxDescriptorServicesTests
{
    internal struct Model
    {
        public LinuxMemoryServices.Admission Memory;
        public uint InputCopies, OutputCopies, SinkCalls, SinkBytes, Channel, FailInputAt, FailOutputAt, MaximumInputBytes;
        public ulong InputBytes;
        public bool FailOutput, FailSink;
        public byte* RequiredOutputSource;
        public fixed byte Sink[8192];
    }
}
