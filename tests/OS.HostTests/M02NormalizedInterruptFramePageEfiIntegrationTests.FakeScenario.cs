using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Tools.QemuRunner;

internal static unsafe partial class M02NormalizedInterruptFramePageEfiIntegrationTests
{
    private enum FakeScenario
    {
        Success,
        MissingBootServices,
        MissingAllocatePages,
        MissingFreePages,
        AllocationFailure,
        ZeroAddress,
        MisalignedAddress,
        NonCanonicalAddress,
        WrappingAddress,
        ReadBackMismatch,
        FreeFailure
    }
}
