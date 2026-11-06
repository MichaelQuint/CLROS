using System.Globalization;
using System.Reflection;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;

// Independently authored copied rows, not app/ELF execution or real scheduler,
// mappings, fd, device, syscall-copy, native, guest or persistence evidence.
internal static partial class NativeAotLogUploadProtocolTests
{
    private delegate Q.NativeAotLogUploadProtocolReport Core(string[] rows, string phase, string? hash, NativeAotLogUploadOutput.Capture? output, NativeAotLogUploadProgram? program);
}
