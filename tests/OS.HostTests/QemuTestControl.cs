using System.Diagnostics;
using System.Text;

internal static class QemuTestControl
{
    public static string Command(params string[] arguments) => "dotnet \"" + typeof(QemuTestControl).Assembly.Location + "\" --qemu-control " + string.Join(' ', arguments.Select(x => "\"" + x.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""));
    public static async Task<int> RunAsync(string[] args)
    {
        switch (args[0])
        {
            case "http":
                using (var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture)))
                {
                    listener.Start();
                    await File.WriteAllTextAsync(args[1], "TEST_START fake.qemu\r\nOS_BOOT_OK\r\nTEST_PASS boot.sequence\r\nOS_SMOKE_PASS\r\n", Encoding.ASCII);
                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    if (args[5] == "1") { await Task.Delay(20000); return 0; }
                    byte[] request = new byte[512];
                    try { _ = await stream.ReadAsync(request); } catch (IOException) { }
                    byte[] body = Convert.FromBase64String(args[4]);
                    string phrase = args[3] switch { "200" => "OK", "204" => "No Content", "503" => "Service Unavailable", _ => "Probe" };
                    byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + args[3] + " " + phrase + "\r\nContent-Type: text/plain\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header); await stream.WriteAsync(body); return 0;
                }
            case "sleep": await Task.Delay(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture)); return 0;
            case "delayed-terminal":
                await File.WriteAllTextAsync(args[1], "OS_BOOT_OK\r\nTEST_PASS boot.sequence\r\nOS_SMOKE_PASS\r\n", Encoding.ASCII);
                await Task.Delay(200); await File.AppendAllTextAsync(args[1], args[2] + "\r\n", Encoding.ASCII); return 0;
            case "delayed-exit":
                await File.WriteAllTextAsync(args[1], "OS_BOOT_OK\r\nTEST_PASS boot.sequence\r\nOS_SMOKE_PASS\r\n", Encoding.ASCII);
                await Task.Delay(int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture)); return int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
            case "panic-writer":
                await File.WriteAllTextAsync(args[3], Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                byte[] bytes = await File.ReadAllBytesAsync(args[2]); string serial = args[1], behavior = args[4];
                if (behavior == "timeout") { await File.WriteAllTextAsync(serial, "TEST_START efi.main"); await Task.Delay(30000); return 0; }
                await File.WriteAllBytesAsync(serial, bytes);
                if (behavior != "hold") await Task.Delay(200);
                switch (behavior)
                {
                    case "append": await File.AppendAllTextAsync(serial, "X"); break;
                    case "rewrite": bytes[0] = 88; await File.WriteAllBytesAsync(serial, bytes); break;
                    case "truncate": await File.WriteAllBytesAsync(serial, []); break;
                    case "exit0": return 0;
                    case "exit7": return 7;
                }
                await Task.Delay(30000); return 0;
            default: throw new ArgumentException("unknown_qemu_control");
        }
    }
}
