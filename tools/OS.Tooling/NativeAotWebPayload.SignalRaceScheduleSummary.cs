using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Copied journal inventory only. IDs, descriptors and record counts
    /// confer no live process, syscall, scheduler, native-execution or guest authority.</summary>

    public readonly record struct SignalRaceScheduleSummary(int Tracer, int Process, int Waiter, int Actor, int Epoll, int Read, int Write, int Records);
}
