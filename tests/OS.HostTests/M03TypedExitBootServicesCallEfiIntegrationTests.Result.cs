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

#if TYPED_EBS_CAPTURE_HOST
using OS.Boot;
using Core = OS.Boot.EfiEntry.TypedExitBootServicesCallProbe;
using MapCore = OS.Boot.EfiEntry.FinalMapRetryProbe;

#else
internal static partial class M03TypedExitBootServicesCallEfiIntegrationTests
{
    private sealed record Result(int Exit, bool Timeout, string Output, string Error);
}
#endif
