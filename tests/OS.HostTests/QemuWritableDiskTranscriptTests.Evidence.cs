using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Storage;
using OS.Tooling;
using F = OS.Tooling.LinuxWritableDiskProofFixture;
using Q = OS.Tooling.QemuTranscripts;
using B = OS.Storage.WritableBlockSession;

// Synthetic copied protocol/value controls only. No native producer, device,
// process, CPU execution, physical persistence or reboot authority is supplied.
internal static unsafe partial class QemuWritableDiskTranscriptTests
{
    private sealed record Evidence(string[] All, int First, int Count, bool Log)
    {
        internal byte[] Package => Log ? F.CreateLogUploadPackage() : F.CreatePackage();
        internal string[] Leaf => All.Skip(First).Take(Count).ToArray();
        internal Q.WritableDiskRow[] Selected => All.Select((s, i) => new Q.WritableDiskRow(i, s)).Where(x => x.Text.StartsWith("M16_027_", StringComparison.Ordinal)).ToArray();

        internal Evidence Replace(int at, string text)
        {
            var copy = (string[])All.Clone();
            copy[at] = text;
            return this with
            {
                All = copy
            };
        }

        internal Evidence Insert(int at, string text)
        {
            var copy = All.ToList();
            copy.Insert(at, text);
            return this with
            {
                All = copy.ToArray(),
                First = at < First ? First + 1 : First,
                Count = at >= First && at < First + Count ? Count + 1 : Count
            };
        }

        internal Evidence Remove(int at)
        {
            var copy = All.ToList();
            copy.RemoveAt(at);
            return this with
            {
                All = copy.ToArray(),
                First = at < First ? First - 1 : First,
                Count = at >= First && at < First + Count ? Count - 1 : Count
            };
        }
    }
}
