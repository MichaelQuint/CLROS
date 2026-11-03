using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static partial class QemuRunnerHttpProbeTests
{
    private readonly struct ProbeRunResult
    {
        public ProbeRunResult(ProcessResult process, string summaryLogPath)
        {
            Process = process;
            SummaryLogPath = summaryLogPath;
        }

        public ProcessResult Process { get; }
        public string SummaryLogPath { get; }
    }
}
