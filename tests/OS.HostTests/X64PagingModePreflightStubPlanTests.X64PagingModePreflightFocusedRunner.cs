using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OS.Boot;
using OS.Kernel.Memory;
#if M01_013_PREFLIGHT_FOCUSED
internal static class X64PagingModePreflightFocusedRunner
{
    public static int Main()
    {
        return X64PagingModePreflightStubPlanTests
            .RunM01013Preflight() == 0
            ? 0
            : 1;
    }
}
#endif
