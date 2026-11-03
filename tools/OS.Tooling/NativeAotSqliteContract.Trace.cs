using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling;
public static partial class NativeAotSqliteContract
{
    public sealed record Trace(IReadOnlyList<TraceCall> Calls, int UnjoinedLines, IReadOnlyList<string> Successors)
    {
        public IReadOnlyList<TraceRow> Rows { get; init; } = Array.Empty<TraceRow>();
        public int UnreturnedCalls => Calls.Count(c => c.Result.StartsWith('?') && c.Syscall is not ("exit" or "exit_group"));
        public int TerminalUnreturnedCalls => Calls.Count(c => c.Result.StartsWith('?') && c.Syscall is "exit" or "exit_group");
        // Normal exit is explicitly nonreturning. It needs the same source's
        // subsequent exact successful terminal record, never a fabricated return.
        public bool CompleteTraceProjection => Rows.Count > 0 && UnjoinedLines == 0 && UnreturnedCalls == 0 && !Calls.Any(c => c.Syscall is "exit" or "exit_group" && c.Result != "?") && Rows.Select(r => r.Source).Distinct(StringComparer.Ordinal).All(source =>
        {
            var rows = Rows.Where(r => r.Source == source).ToArray();
            if (rows.Count(r => r.Kind == TraceRowKind.ProcessTerminal) != 1 || !Regex.IsMatch(rows[^1].Raw, @"^\d+\.\d+ \+\+\+ exited with 0 \+\+\+$", RegexOptions.CultureInvariant))
                return false;
            var exits = Calls.Where(c => c.Source == source && c.Result.StartsWith('?') && c.Syscall is "exit" or "exit_group").ToArray();
            return exits.Length == 0 || exits.Length == 1 && exits[0].Arguments == "0" && exits[0].Result == "?" && rows.Length >= 2 && rows[^2].Line == exits[0].Line && rows[^2].Kind == TraceRowKind.Call;
        });
        public bool GrantsCompleteTraceOrExecutionAuthority => false;
    }
}
