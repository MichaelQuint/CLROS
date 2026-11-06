using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Tooling;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;
using Probe = OS.Runtime.NativeAotModuleRegistrationProbe;
using Status = OS.Kernel.Runtime.TypeManagerModuleStatus;

internal static partial class M04NativeAotModuleRegistrationEfiIntegrationTests
{
    private sealed record Result(int Exit, bool Timeout, string Output, string Error);
}
