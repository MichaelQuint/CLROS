using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OS.Tools.QemuRunner;

public static partial class WebSocketControls
{
    private sealed class SerialEvents : IDisposable
    {
        private readonly FileStream file;
        private readonly StringBuilder fragment = new();
        private long read;
        internal readonly List<string> Rows = [];
        internal SerialEvents(string path)
        {
            file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            file.Position = file.Length;
        }

        internal void Poll()
        {
            if (file.Length < file.Position)
                throw new InvalidDataException("Control serial truncated.");
            byte[] bytes = new byte[4096];
            int count;
            while ((count = file.Read(bytes)) != 0)
            {
                if ((read += count) > 8 * 1024 * 1024)
                    throw new InvalidDataException("Control serial budget.");
                for (int i = 0; i < count; i++)
                {
                    if (bytes[i] == 10)
                    {
                        string line = fragment.ToString().TrimEnd('\r');
                        fragment.Clear();
                        if (line.StartsWith("M07_027_TIMEOUT ", StringComparison.Ordinal) || line.StartsWith("M07_028_DROP ", StringComparison.Ordinal))
                            Rows.Add(line);
                        if (Rows.Count > 2)
                            throw new InvalidDataException("Extra control events.");
                    }
                    else
                    {
                        fragment.Append((char)bytes[i]);
                        if (fragment.Length > 65536)
                            throw new InvalidDataException("Control serial line budget.");
                    }
                }
            }
        }

        public void Dispose() => file.Dispose();
    }
}
