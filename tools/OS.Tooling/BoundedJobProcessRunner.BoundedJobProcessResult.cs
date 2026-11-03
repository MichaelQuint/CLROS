// Extracted from the reviewed EFI verifier helper; preserve native lifecycle semantics.
#nullable disable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling.Native
{
    public sealed class BoundedJobProcessResult
    {
        public string Status { get; private set; }
        public int ExitCode { get; private set; }
        public string Output { get; private set; }
        public string StandardOutput { get; private set; }
        public string StandardError { get; private set; }
        public int OutputCharacters { get; private set; }
        public bool TerminationConfirmed { get; private set; }
        public string Detail { get; private set; }

        public BoundedJobProcessResult(string status, int exitCode, string output, int outputCharacters, bool terminationConfirmed, string detail, string standardOutput = null, string standardError = null)
        {
            Status = status;
            ExitCode = exitCode;
            Output = output ?? String.Empty;
            StandardOutput = standardOutput ?? Output;
            StandardError = standardError ?? String.Empty;
            OutputCharacters = outputCharacters;
            TerminationConfirmed = terminationConfirmed;
            Detail = detail ?? String.Empty;
        }
    }
}
