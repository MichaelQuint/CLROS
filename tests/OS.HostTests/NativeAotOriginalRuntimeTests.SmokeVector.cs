using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Manifest;
using OS.Tooling;
using Vector = NativeAotThreadingTranscriptTests.Vector;
using Thread = NativeAotThreadingTranscriptTests.Thread;

internal static partial class NativeAotOriginalRuntimeTests
{
    // Complete independent synthetic kernel observations, never a guest capture
    // or a way to obtain the sealed package/instruction factory authority.
    private sealed class SmokeVector
    {
        internal readonly Vector Core = new(true);
        internal readonly StringBuilder Text = new();
        internal SmokeVector(bool earlyTimer = false, bool wakeTimer = false, int clockReads = 2, bool clockProgress = true, bool requeueTimer = false)
        {
            var v = Core.Start();
            v.Rows[1] = v.Rows[1].Replace("entropyBytes=32", "entropyBytes=256", StringComparison.Ordinal);
            v.Rows[2] = v.Rows[2].Replace("packageBytes=1577712", "packageBytes=1511248", StringComparison.Ordinal);
            v.Rows[4] = v.Rows[4].Replace("workers=8", "workers=16", StringComparison.Ordinal);
            v.Call(309, -38, [0x500100, 0, 0]); // Observed getcpu fallback; no output is written.
            Write("TEST_START nativeaot.runtime");
            Write("NATIVEAOT_RUNTIME_EH_OK");
            Thread final = v.Clone(2, 2), signal = v.Clone(3, 3), worker = v.Clone(4, 4), pool = v.Clone(5, 5), timer = v.Clone(6, 6);
            v.Irq(10, 6, 6);
            v.Select(final);
            v.Wait(0x500200);
            v.Drain(20);
            v.Supervisor(3, 5, 6);
            v.Select(signal);
            v.Call(0, 0, [3, 0x500700, 1], 3, 2);
            v.Drain(30);
            v.Supervisor(3, 4, 6);
            v.Select(worker);
            v.Wait(0x500210);
            v.Drain(40);
            v.Supervisor(3, 3, 6);
            v.Select(pool);
            v.Call(186, 5);
            v.Wait(0x500214);
            v.Drain(50);
            v.Supervisor(3, 2, 6);
            v.Select(timer);
            v.Wait(0x500218, 100);
            v.Drain(60);
            v.Supervisor(3, 1, 6);
            v.Select(v.Main);
            if (earlyTimer)
                FireTimer(6);
            v.Wake(0x500210);
            v.Wait(0x500234);
            v.Drain(v.Clock);
            v.Supervisor(3, 1, 6);
            v.Select(worker);
            v.Resume(0);
            v.Wake(0x500234);
            v.Call(60, 0, [0], 4);
            v.Drain(v.Clock);
            v.Exit(4, 1, 6);
            v.Select(v.Main);
            v.Resume(0);
            Write("NATIVEAOT_RUNTIME_GC_THREADS_TLS_OK");
            if (!earlyTimer)
                FireTimer(5);
            Write("NATIVEAOT_RUNTIME_TASK_TIMER_OK");
            Write("TEST_PASS nativeaot.runtime");
            v.Call(231, 0, [0], 2);
            v.Drain(v.Clock);
            v.Exit(2, 4, 5);
            int ready = 3, active = 4;
            foreach (var t in new[]
            {
                final,
                signal,
                pool,
                timer
            }

            )
            {
                v.Select(t);
                v.Exit(4, ready--, active--);
            }

            v.Rows.Add("M16_008_BUDGET_END nanos=100");
            v.End(6);
            void FireTimer(int active)
            {
                if (wakeTimer)
                {
                    if (clockReads > 0)
                        v.Call(228, 0, [6, 0x500600]);
                    if (requeueTimer)
                        v.Call(202, 1, [0x500218, 131, 1, 0, 0x50021c]);
                    else
                        v.Wake(0x500218);
                    v.Wait(0x500234);
                    v.Drain(clockProgress ? 80UL : 60UL);
                    v.Supervisor(3, 1, active);
                    v.Select(timer);
                    v.Resume(0);
                    if (clockReads > 1)
                        v.Call(228, 0, [6, 0x500600]);
                    v.Wake(0x500234);
                    v.Wait(0x500218);
                    v.Drain(v.Clock);
                    v.Supervisor(3, 1, active);
                    v.Select(v.Main);
                    v.Resume(0);
                    return;
                }

                v.Wait(0x500234);
                v.Drain(v.Clock);
                v.Supervisor(3, 0, active);
                v.Tick(100, 1);
                v.Select(timer);
                v.Resume(-110);
                v.Wake(0x500234);
                v.Wait(0x500218);
                v.Drain(100);
                v.Supervisor(3, 1, active);
                v.Select(v.Main);
                v.Resume(0);
            }
        }

        internal void Write(string line)
        {
            string data = line + "\n";
            Text.Append(data);
            int bytes = Encoding.UTF8.GetByteCount(data);
            Core.Lengths.Add(bytes);
            Core.Rows.Add($"M16_005_WRITE fd=1 bytes={bytes} data={line}");
            Core.Call(1, bytes, [1, 0x500400, (uint)bytes]);
        }

        internal QemuTranscripts.ThreadingProtocolReport Inspect(string[]? rows = null) => QemuTranscripts.InspectNativeAotRuntimeProtocolForTests(App, rows ?? Core.Rows.ToArray(), NativeAotThreadingTranscriptTests.Elf(), r => r == 0x400202, r => r == 0x400202, Core.Lengths, Text.ToString(), 8192, 12288);
        internal byte[] Raw()
        {
            using MemoryStream raw = new();
            int offset = 0, index = 0;
            foreach (string row in Core.Rows)
            {
                string value = row;
                if (row.StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
                {
                    int count = Core.Lengths[index++];
                    value = row[..(row.IndexOf(" data=", StringComparison.Ordinal) + 6)] + Text.ToString(offset, count);
                    offset += count;
                }

                raw.Write(Encoding.UTF8.GetBytes(value + "\r\n"));
            }

            return raw.ToArray();
        }
    }
}
