using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using D = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using LogUpload = OS.Tooling.NativeAotLogUploadContract;
using P = OS.Storage.WritableBlockReadPort;

internal static unsafe partial class LinuxContinuingWritableDescriptorServicesTests
{
    private delegate bool RetainedEngine(D.State* state, ulong epoch, ulong engineOwner, ulong engineEpoch);
}
