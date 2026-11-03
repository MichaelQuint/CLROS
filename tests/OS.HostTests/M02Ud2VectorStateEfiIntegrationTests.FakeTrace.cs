using OS.Tooling;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static partial class M02Ud2VectorStateEfiIntegrationTests
{
    private sealed class FakeTrace
    {
        public List<string> Events { get; } = [];
        public List<FakeAllocation> AllocationCalls { get; } = [];
        public List<FakeRelease> ReleaseCalls { get; } = [];
        public DisabledDefaultUd2VectorStatePreTerminalStage Stage { get; set; }
        public DisabledDefaultUd2VectorStatePreTerminalStatus Status { get; set; }
        public bool Succeeded { get; set; }
        public bool TerminalAllowed { get; set; }
        public bool FailureEmitted { get; set; }
    }
}
