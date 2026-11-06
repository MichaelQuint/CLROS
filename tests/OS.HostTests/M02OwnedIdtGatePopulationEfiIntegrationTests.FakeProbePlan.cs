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
    private readonly record struct FakeProbePlan(bool HasBootServices, bool HasAllocatePages, bool HasFreePages, ulong AllocateStatus, ulong ArenaAddress, bool MutateStubReadBack, bool MutateIdtReadBack, ulong FreeStatus, bool FailStubMaterializeResult = false, bool FailIdtMaterializeResult = false);
}
