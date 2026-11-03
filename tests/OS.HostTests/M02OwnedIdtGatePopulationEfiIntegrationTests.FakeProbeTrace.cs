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

internal static partial class M02OwnedIdtGatePopulationEfiIntegrationTests
{
    private sealed class FakeProbeTrace
    {
        public bool StartEmitted
        {
            get; set;
        }
        public int AllocateCalls
        {
            get; set;
        }
        public int StubMaterializeCalls
        {
            get; set;
        }
        public int StubReadBackCalls
        {
            get; set;
        }
        public int IdtMaterializeCalls
        {
            get; set;
        }
        public int IdtReadBackCalls
        {
            get; set;
        }
        public int FreeCalls
        {
            get; set;
        }
        public ulong FreedAddress
        {
            get; set;
        }
        public int FreedPages
        {
            get; set;
        }
        public bool ReleaseCompleted
        {
            get; set;
        }
        public bool ReadyEmitted
        {
            get; set;
        }
        public bool PassEmitted
        {
            get; set;
        }
        public bool FailureEmitted
        {
            get; set;
        }
        public ulong StubFingerprint
        {
            get; set;
        }
        public ulong NormalizedPolicyFingerprint
        {
            get; set;
        }
        public string StubPageSha256 { get; set; } = string.Empty;
        public bool RepresentativeSlotsExact
        {
            get; set;
        }
        public bool RepresentativeDescriptorsExact
        {
            get; set;
        }
        public bool AllHandlersInsideStubPage
        {
            get; set;
        }
        public bool ThirdPageUnchanged
        {
            get; set;
        }
    }
}
