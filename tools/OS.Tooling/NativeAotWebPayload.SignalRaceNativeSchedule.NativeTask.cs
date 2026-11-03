using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling;
// Controlled Linux-native experiment only; the selected v2/v3 controller owns each hook.
// No guest code uses this scheduling helper.
public static partial class NativeAotWebPayload
{
    private sealed partial class SignalRaceNativeSchedule
    {
        private sealed class NativeTask(int pid, int tid, FileStream stat, SafeFileHandle pidfd, Stat initial) : IDisposable
        {
            internal readonly int Pid = pid, Tid = tid;
            internal readonly Stat Initial = initial;
            internal int Cpu;
            internal Stat Read()
            {
                stat.Position = 0;
                using var reader = new StreamReader(stat, Encoding.UTF8, false, 1024, leaveOpen: true);
                string text = reader.ReadToEnd();
                Require(text.Length <= 65536, "signalrace_schedule_stat_size");
                Stat current = ParseStat(text);
                Require(current.Tid == Initial.Tid && current.Start == Initial.Start && current.Group == Initial.Group && current.Session == Initial.Session, "signalrace_schedule_generation");
                return current;
            }

            internal bool Exited()
            {
                PollFd poll = new()
                {
                    Fd = (int)pidfd.DangerousGetHandle(),
                    Events = 1
                };
                int count = NativePoll(ref poll, 1, 0);
                NativeCheck(count >= 0, "pidfd_poll");
                Require((poll.Returned & ~(1 | 16)) == 0, "signalrace_schedule_pidfd_event");
                return count == 1 && (poll.Returned & (1 | 16)) != 0;
            }

            internal void EnsureLive()
            {
                Require(!Exited(), "signalrace_schedule_task_exit");
                _ = Read();
            }

            public void Dispose()
            {
                pidfd.Dispose();
                stat.Dispose();
            }
        }
    }
}
