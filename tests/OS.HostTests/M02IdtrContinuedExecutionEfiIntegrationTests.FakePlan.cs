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
    private sealed class FakePlan
    {
        public bool BootServicesPresent { get; init; } = true;
        public bool AllocatePresent { get; init; } = true;
        public bool FreePresent { get; init; } = true;
        public ulong CodeAddress { get; init; } = ValidCodeAddress;
        public ulong DataAddress { get; init; } = ValidDataAddress;
        public ulong CodeAllocationStatus
        {
            get; init;
        }
        public ulong DataAllocationStatus
        {
            get; init;
        }
        public bool CorruptNormalizedPage
        {
            get; init;
        }
        public bool CorruptIdt
        {
            get; init;
        }
        public bool CorruptTransitionStub
        {
            get; init;
        }
        public bool PublicationSucceeded { get; init; } = true;
        public FakeObservation Observation
        {
            get; init;
        }
        public ulong DataReleaseStatus
        {
            get; init;
        }
        public ulong CodeReleaseStatus
        {
            get; init;
        }
    }
}
