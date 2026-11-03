using OS.Tools.QemuRunner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static partial class Program
{
    private sealed class QmpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly Action<string> _log;
        private int _nextId;
        public QmpConnection(TcpClient client, int timeoutSeconds, Action<string> log)
        {
            _client = client;
            NetworkStream stream = client.GetStream();
            stream.ReadTimeout = checked(timeoutSeconds * 1000);
            stream.WriteTimeout = checked(timeoutSeconds * 1000);
            _reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            _writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };
            _log = log;
        }

        public void Initialize()
        {
            string greeting = ReadLineRequired("qmp_greeting_missing");
            _log("qmpGreetingBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(greeting)));
            using JsonDocument document = JsonDocument.Parse(greeting);
            if (!document.RootElement.TryGetProperty("QMP", out JsonElement qmp) || !qmp.TryGetProperty("version", out JsonElement version) || !version.TryGetProperty("qemu", out JsonElement qemuVersion) || !qemuVersion.TryGetProperty("major", out _))
            {
                throw new InvalidDataException("qmp_greeting_invalid");
            }

            Send("qmp_capabilities", null);
        }

        public void RequireMemoryShape()
        {
            JsonElement summary = Send("query-memory-size-summary", null);
            long pluggedMemoryBytes = 0;
            if (!summary.TryGetProperty("base-memory", out JsonElement baseMemory) || !baseMemory.TryGetInt64(out long baseMemoryBytes) || baseMemoryBytes != GuestMemoryBytes || (summary.TryGetProperty("plugged-memory", out JsonElement pluggedMemory) && !pluggedMemory.TryGetInt64(out pluggedMemoryBytes)) || pluggedMemoryBytes != 0)
            {
                throw new InvalidDataException("qmp_memory_shape_invalid");
            }

            _log("qmpMemoryShapeValidated=true");
        }

        public void StopAndRequirePaused()
        {
            Send("stop", null);
            JsonElement status = Send("query-status", null);
            if (!status.TryGetProperty("running", out JsonElement running) || running.ValueKind != JsonValueKind.False || !status.TryGetProperty("status", out JsonElement statusName) || !string.Equals(statusName.GetString(), "paused", StringComparison.Ordinal))
            {
                throw new InvalidDataException("qmp_stop_not_paused");
            }

            _log("qmpPaused=true");
        }

        public void Pmemsave(long physicalAddress, int size, string path, int chunkIndex)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>
            {
                ["val"] = physicalAddress,
                ["size"] = size,
                ["filename"] = path
            };
            Send("pmemsave", arguments, "pmemsave-" + chunkIndex.ToString("D2", CultureInfo.InvariantCulture));
        }

        public JsonElement Send(string execute, Dictionary<string, object>? arguments, string? explicitId = null)
        {
            string id = explicitId ?? "command-" + Interlocked.Increment(ref _nextId).ToString("D3", CultureInfo.InvariantCulture);
            Dictionary<string, object> request = new Dictionary<string, object>
            {
                ["execute"] = execute,
                ["id"] = id
            };
            if (arguments is not null)
            {
                request["arguments"] = arguments;
            }

            string json = JsonSerializer.Serialize(request);
            _log("qmpRequestBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
            _writer.WriteLine(json);
            while (true)
            {
                string response = ReadLineRequired("qmp_response_missing_" + Sanitize(execute));
                _log("qmpResponseBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(response)));
                using JsonDocument document = JsonDocument.Parse(response);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("event", out JsonElement eventName))
                {
                    string? name = eventName.GetString();
                    if (string.Equals(name, "RESET", StringComparison.Ordinal) || string.Equals(name, "SHUTDOWN", StringComparison.Ordinal) || string.Equals(name, "GUEST_PANICKED", StringComparison.Ordinal) || string.Equals(name, "WATCHDOG", StringComparison.Ordinal) || string.Equals(name, "POWERDOWN", StringComparison.Ordinal) || string.Equals(name, "RESUME", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("qmp_terminal_event_" + Sanitize(name ?? "unknown"));
                    }

                    continue;
                }

                if (!root.TryGetProperty("id", out JsonElement responseId) || !string.Equals(responseId.GetString(), id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (root.TryGetProperty("error", out JsonElement error))
                {
                    throw new InvalidDataException("qmp_error_" + Sanitize(execute) + "_" + Sanitize(error.ToString()));
                }

                if (!root.TryGetProperty("return", out JsonElement result))
                {
                    throw new InvalidDataException("qmp_return_missing_" + Sanitize(execute));
                }

                return result.Clone();
            }
        }

        public void Dispose()
        {
            _writer.Dispose();
            _reader.Dispose();
            _client.Dispose();
        }

        private string ReadLineRequired(string reason)
        {
            string? line = _reader.ReadLine();
            if (line is null)
            {
                throw new EndOfStreamException(reason);
            }

            return line;
        }
    }
}
