using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotLogUploadContract
{
    /// <summary>Expected bytes/digests computed by this host inspector, not
    /// cryptographic reports from the native app. Actual files are inspected
    /// independently by each original reference owner.</summary>

    public sealed record Transcript(string Phase, FileValues ExpectedFiles)
    {
        public bool GrantsOriginalProcessOrFlushAuthority => false;
    }
}
