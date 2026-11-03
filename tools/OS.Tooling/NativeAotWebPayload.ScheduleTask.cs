using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotWebPayload
{
    private sealed record ScheduleTask(int Pid, int Tid, ScheduleStat Initial, int Cpu, string Affinity);
}
