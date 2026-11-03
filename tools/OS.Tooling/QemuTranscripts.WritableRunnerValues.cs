using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied original file identities used to reproduce the writable
    /// runner's exact stdout and summary. Values alone grant no process, image,
    /// device, execution or persistence authority; the enclosing owner retains
    /// and revalidates those files across the actual joined invocation.</summary>

    public sealed record WritableRunnerValues(string WorkingPath, string BootIdentity, long BootLength, string BootSha256, string WorkingIdentity, long WorkingLength, string InitialSha256)
    {
        internal string[] Rows(bool joined) => ["WRITABLE_DATA_IMAGE " + WorkingPath, $"WRITABLE_DATA_BOOT identity={BootIdentity} bytes={BootLength} sha256={BootSha256}", $"WRITABLE_DATA_WORKING identity={WorkingIdentity} bytes={WorkingLength} initialSha256={InitialSha256} content=mutable cache=writeback", "WRITABLE_DATA_LIFETIME joined=" + (joined ? "true" : "false") + " finalVerified=" + (joined ? "true" : "false") + " acceptance=pending"];
    }
}
