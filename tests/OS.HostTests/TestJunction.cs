using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal static class TestJunction
{
    // A mount-point reparse record creates an unprivileged junction in a test-owned directory.
    internal static void Create(string link, string target)
    {
        Directory.CreateDirectory(link);
        string substitute = @"\??\" + Path.GetFullPath(target);
        byte[] substituteBytes = Encoding.Unicode.GetBytes(substitute);
        byte[] printBytes = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
        byte[] record = new byte[16 + substituteBytes.Length + 2 + printBytes.Length + 2];
        BitConverter.TryWriteBytes(record.AsSpan(0), 0xA0000003u);
        BitConverter.TryWriteBytes(record.AsSpan(4), checked((ushort)(record.Length - 8)));
        BitConverter.TryWriteBytes(record.AsSpan(8), (ushort)0);
        BitConverter.TryWriteBytes(record.AsSpan(10), checked((ushort)substituteBytes.Length));
        BitConverter.TryWriteBytes(record.AsSpan(12), checked((ushort)(substituteBytes.Length + 2)));
        BitConverter.TryWriteBytes(record.AsSpan(14), checked((ushort)printBytes.Length));
        substituteBytes.CopyTo(record, 16);
        printBytes.CopyTo(record, 18 + substituteBytes.Length);
        using SafeFileHandle handle = CreateFileW(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid || !DeviceIoControl(handle, 0x000900A4, record, (uint)record.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, uint inputBytes,
        IntPtr output, uint outputBytes, out uint returned, IntPtr overlapped);
}
