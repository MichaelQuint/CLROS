using System.IO;
using System.Runtime.InteropServices;

/// <summary>Linux x64 diagnostic syscalls for an externally owned signal probe.
/// The caller owns descriptors, target threads and saved process/thread state;
/// local unmanaged buffers live through each synchronous call and never escape.
/// Unsupported hosts refuse before loading libc. Unexpected native results throw;
/// Wait alone returns the original result and immediately captured errno. No
/// helper installs a managed signal handler or establishes native/guest authority.</summary>
internal static unsafe class KestrelSignalProbeAbi
{
    internal const int Signal = 12;
    internal const byte WakeByte = 0x5a;
    internal const byte RecoveryByte = 0x6b;
    internal static bool Supported => OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64;

    [StructLayout(LayoutKind.Sequential)]
    internal struct KernelAction { internal ulong Handler, Flags, Restorer, Mask; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct AltStack { internal ulong Address; internal uint Flags, Padding; internal ulong Bytes; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct EpollEvent { internal uint Events; internal ulong Data; }
    internal readonly record struct Result(long Value, int Errno);
    internal readonly record struct WaitResult(long Value, int Errno, uint Events, ulong Data);

    internal static bool SameAction(KernelAction a, KernelAction b) =>
        a.Handler == b.Handler && a.Flags == b.Flags && a.Restorer == b.Restorer && a.Mask == b.Mask;
    internal static bool SameStack(AltStack a, AltStack b) =>
        a.Address == b.Address && a.Flags == b.Flags && a.Padding == b.Padding && a.Bytes == b.Bytes;
    internal static bool IsDefaultAction(KernelAction action) => action.Handler == 0;
    // Shape only: executable mapping, current registration ownership and drift
    // checks remain the caller's responsibility; no old ELF address is admitted.
    internal static bool IsInstalledAction(KernelAction action) => action.Handler > 1 && action.Restorer != 0 &&
        (action.Flags & 0x14000004UL) == 0x14000004UL;

    internal static KernelAction ReadAction()
    {
        KernelAction action = default;
        Exact(Invoke(13, Signal, 0, (nint)(&action), 8), 0, "read_action"); return action;
    }
    // Only the external probe owner may restore its saved action after sender,
    // native waiter and managed callback quiescence and registration disposal.
    internal static void SetAction(KernelAction action) =>
        Exact(Invoke(13, Signal, (nint)(&action), 0, 8), 0, "set_action");
    internal static ulong ReadMask()
    {
        ulong mask = 0;
        Exact(Invoke(14, 2, 0, (nint)(&mask), 8), 0, "read_mask"); return mask;
    }
    internal static void SetMask(ulong mask) => Exact(Invoke(14, 2, (nint)(&mask), 0, 8), 0, "set_mask");
    internal static AltStack ReadAltStack()
    {
        AltStack stack = default;
        Exact(Invoke(131, 0, (nint)(&stack)), 0, "read_altstack"); return stack;
    }
    internal static int GetPid() => Positive(Invoke(39), "getpid");
    internal static int GetTid() => Positive(Invoke(186), "gettid");
    internal static void CreatePipe(out int read, out int write)
    {
        read = write = -1;
        int* pair = stackalloc int[2]; pair[0] = pair[1] = -1;
        Exact(Invoke(293, (nint)pair, 0x80800), 0, "pipe2");
        if (pair[0] < 0 || pair[1] < 0 || pair[0] == pair[1]) throw new IOException("signal_probe_pipe_descriptors");
        read = pair[0]; write = pair[1];
    }
    internal static int CreateEpoll()
    {
        Result result = Invoke(291, 0x80000);
        if (result.Value < 0 || result.Value > int.MaxValue) throw Error("epoll_create1", result);
        return (int)result.Value;
    }
    internal static void AddRead(int epoll, int read, ulong tag)
    {
        EpollEvent value = new() { Events = 1, Data = tag };
        Exact(Invoke(233, epoll, 1, read, (nint)(&value)), 0, "epoll_ctl");
    }
    internal static WaitResult Wait(int epoll, int timeoutMilliseconds)
    {
        EpollEvent value = new() { Events = uint.MaxValue, Data = ulong.MaxValue };
        Result result = Invoke(232, epoll, (nint)(&value), 1, timeoutMilliseconds);
        return new(result.Value, result.Errno, value.Events, value.Data);
    }
    // Borrow only a live NONBLOCK pipe descriptor; short/EOF/EAGAIN results
    // fail rather than retrying or inventing which byte was consumed.
    internal static byte ReadByte(int descriptor)
    {
        byte value = 0; Exact(Invoke(0, descriptor, (nint)(&value), 1), 1, "read_byte"); return value;
    }
    internal static void WriteByte(int descriptor, byte value) =>
        Exact(Invoke(1, descriptor, (nint)(&value), 1), 1, "write_byte");
    internal static void WriteWake(int descriptor) => WriteByte(descriptor, WakeByte);
    internal static void Close(int descriptor) => Exact(Invoke(3, descriptor), 0, "close");
    internal static void SendSignal(int processId, int targetTid)
    {
        RequireSupported();
        if (processId <= 0 || targetTid <= 0) throw new ArgumentOutOfRangeException(nameof(targetTid));
        Exact(Invoke(234, processId, targetTid, Signal), 0, "tgkill");
    }

    private static void RequireSupported()
    {
        if (!Supported) throw new PlatformNotSupportedException("signal probe requires Linux x64");
    }
    private static Result Invoke(nint number, nint a0 = 0, nint a1 = 0, nint a2 = 0, nint a3 = 0, nint a4 = 0, nint a5 = 0)
    {
        RequireSupported();
        nint value = Syscall(number, a0, a1, a2, a3, a4, a5);
        int error = Marshal.GetLastPInvokeError();
        return new((long)value, error);
    }
    private static void Exact(Result result, long expected, string operation)
    {
        if (result.Value != expected) throw Error(operation, result);
    }
    private static int Positive(Result result, string operation)
    {
        if (result.Value <= 0 || result.Value > int.MaxValue) throw Error(operation, result);
        return (int)result.Value;
    }
    private static IOException Error(string operation, Result result) =>
        new($"signal_probe_{operation} value={result.Value} errno={result.Errno}");
    [DllImport("libc", EntryPoint = "syscall", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    private static extern nint Syscall(nint number, nint a0, nint a1, nint a2, nint a3, nint a4, nint a5);
}
