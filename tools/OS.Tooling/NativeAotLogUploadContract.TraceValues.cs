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
    /// <summary>Copied raw trace rows, including unfinished/resumed, unreturned
    /// terminal exit and unknown records. No inferred returns or descriptor/path
    /// causality. Complete projection is separate from successful process exit.</summary>

    public sealed class TraceValues
    {
        internal TraceValues(string phase, IEnumerable<TraceRow> rows, int exitedFiles, int files)
        {
            Phase = phase;
            Rows = Array.AsReadOnly(rows.ToArray());
            ExitedFiles = exitedFiles;
            Files = files;
        }

        public string Phase { get; }
        public ReadOnlyCollection<TraceRow> Rows { get; }
        public int Files { get; }
        public int ExitedFiles { get; }
        public int UnjoinedRows => Rows.Count(row => row.Kind is TraceKind.UnfinishedCall or TraceKind.ResumedCall);
        public int UnreturnedRows => Rows.Count(row => row.Kind == TraceKind.UnreturnedCall);
        /// <summary>Explicit unknown results for nonreturning exit syscalls.
        /// These remain unreturned rows and do not complete the projection.</summary>

        public int TerminalUnreturnedRows => Rows.Count(row => row.Kind == TraceKind.UnreturnedCall && row.Syscall is "exit" or "exit_group");
        public int NonterminalUnreturnedRows => UnreturnedRows - TerminalUnreturnedRows;
        public int UnclassifiedRows => Rows.Count(row => row.Kind == TraceKind.Unclassified);
        public bool CompleteTraceProjection => ExitedFiles == Files && UnjoinedRows == 0 && UnreturnedRows == 0 && UnclassifiedRows == 0;
        public bool GrantsSyscallClosureLockFlushOrPersistenceAuthority => false;
    }
}
