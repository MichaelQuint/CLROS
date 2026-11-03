using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotWebPayload
{
    private sealed record ScheduleStat(int Tid, int Parent, int Group, int Session, ulong Start, string State);
}
