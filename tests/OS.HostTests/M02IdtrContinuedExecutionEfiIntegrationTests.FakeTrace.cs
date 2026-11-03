using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Tools.QemuRunner;

internal static unsafe partial class M02IdtrContinuedExecutionEfiIntegrationTests
{
    private sealed class FakeTrace
    {
        public List<FakeAllocation> AllocationCalls { get; } = [];
        public List<FakeRelease> ReleaseCalls { get; } = [];
        public List<string> Events { get; } = [];
        public bool Succeeded { get; set; }
        public bool ReadyEmitted { get; set; }
        public bool FailureEmitted { get; set; }
        public bool FailClosed { get; set; }
    }
}
