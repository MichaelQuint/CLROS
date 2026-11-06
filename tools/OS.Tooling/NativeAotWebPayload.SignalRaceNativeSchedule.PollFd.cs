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
        [StructLayout(LayoutKind.Sequential)]
        private struct PollFd
        {
            internal int Fd;
            internal short Events, Returned;
        }
    }
}
