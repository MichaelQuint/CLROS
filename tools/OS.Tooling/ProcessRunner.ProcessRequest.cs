using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Text;
using System.Runtime.ExceptionServices;
using OS.Tooling.Native;

namespace OS.Tooling;
public sealed record ProcessRequest(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory)
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
    public int OutputCharacterLimit { get; init; } = 4 * 1024 * 1024;
    public IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; init; } = new Dictionary<string, string?>();
    public Action<string, bool>? OutputReceived { get; init; }
    /// <summary>Windows job ownership for a caller which retains mutable files
    /// across this await. Execution and initial termination remain bounded, but
    /// uncertain cleanup keeps the same call and all original process handles
    /// pending until their signals and an empty, completely captured job agree.
    /// Cancellation does not release those handles or authorize extraction.
    /// An incomplete original census cannot become proof from a later empty job.</summary>

    public bool RetainOwnershipUntilJoined { get; init; }
    /// <summary>Opt-in Windows stderr file/handle owned by the caller across the
    /// complete retained job await. The launcher inherits an owned duplicate;
    /// it never disposes or changes inheritance of this original. No stderr pipe
    /// is captured on this route: the caller must verify the original file,
    /// content/size limits and actual join before acceptance. Stdout remains
    /// captured. Requires RetainOwnershipUntilJoined; unsupported elsewhere.</summary>

    public SafeFileHandle? StandardErrorHandle { get; init; }
    // Per-request host controls may only defer an actual join, never manufacture
    // a signaled process, empty job or complete census. No global test hook.
    internal Func<BoundedJobProcessRunner.OwnershipJoinObservation, bool>? JoinPendingForTest { get; init; }
    internal bool AssignmentFailureForTest { get; init; }
}
