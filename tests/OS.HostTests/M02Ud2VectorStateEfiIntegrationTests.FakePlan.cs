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
    private sealed class FakePlan
    {
        public byte CurrentState
        {
            get; init;
        }
        public bool BootServicesPresent { get; init; } = true;
        public bool AllocatePresent { get; init; } = true;
        public bool FreePresent { get; init; } = true;
        public ulong CodeAllocationStatus
        {
            get; init;
        }
        public ulong CodeAddress { get; init; } = ValidCodeAddress;
        public ulong DataAllocationStatus
        {
            get; init;
        }
        public ulong DataAddress { get; init; } = ValidDataAddress;
        public bool MaterializationSucceeded { get; init; } = true;
        public bool ReadBackSucceeded { get; init; } = true;
        public bool MarkerTransportSucceeded { get; init; } = true;
        public bool PublicationSucceeded { get; init; } = true;
    }
}
