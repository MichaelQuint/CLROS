using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotWebPayload
{
    private sealed record SchedulePipe(int Epoll, int Read, int Write, string Inode, string EpollInfo, string ReadInfo, string WriteInfo, string Syscall, string WaitChannel);
}
