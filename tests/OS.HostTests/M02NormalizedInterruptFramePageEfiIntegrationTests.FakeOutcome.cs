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
    private readonly record struct FakeOutcome(bool Succeeded, bool ReadyEmitted, bool FailureEmitted, int AllocateCalls, int MaterializeCalls, int ReadBackCalls, int FreeCalls, int AllocateOrder, int MaterializeOrder, int ReadBackOrder, int FreeOrder, ulong Fingerprint, string PageSha256, string Vector0, string Vector8, string Vector255)
    {
        public static FakeOutcome FailBeforeAllocation()
        {
            return new FakeOutcome(false, false, true, 0, 0, 0, 0, 0, 0, 0, 0, 0, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }
}
