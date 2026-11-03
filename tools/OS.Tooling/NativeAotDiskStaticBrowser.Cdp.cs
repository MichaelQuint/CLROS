using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotDiskStaticBrowser
{
    /// <summary>Own one debugger socket and receive pump. A missing WebSocket
    /// close handshake is classified only after an acknowledged Browser.close,
    /// a successful joined browser and an empty command/message boundary.
    /// Disposal aborts and joins the pump; it supplies no successful shutdown.</summary>

    internal sealed partial class Cdp : IAsyncDisposable
    {
        private readonly ClientWebSocket socket = new();
        private readonly SemaphoreSlim sender = new(1);
        private readonly CancellationTokenSource stop = new();
        private readonly Dictionary<int, TaskCompletionSource<JsonElement>> pending = [];
        private readonly object gate = new();
        private readonly StreamWriter log;
        private readonly Action<string, JsonElement> receive;
        private Task? pump;
        private int sequence, total;
        private int closeCommandId = -1, pendingAtTermination = -1;
        private long messageBytes, partialBytesAtTermination = -1;
        private bool closeAcknowledged, fragmentInProgress, fragmentAtTermination, stoppedAtTermination, acceptedExpectedEof;
        private Exception? terminationFailure;
        public Termination Shutdown
        {
            get
            {
                lock (gate)
                    return new(closeCommandId, closeAcknowledged, pendingAtTermination, partialBytesAtTermination, fragmentAtTermination, stoppedAtTermination, terminationFailure?.GetType().FullName, (terminationFailure as WebSocketException)?.WebSocketErrorCode.ToString(), terminationFailure?.Message, acceptedExpectedEof);
            }
        }

        public Cdp(string path, Action<string, JsonElement> callback)
        {
            log = new(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            receive = callback;
        }

        public async Task Connect(Uri uri, CancellationToken token)
        {
            await socket.ConnectAsync(uri, token);
            pump = Read();
        }

        private void Log(string direction, string message)
        {
            string line = JsonSerializer.Serialize(new
            {
                Direction = direction,
                Message = message
            });
            lock (gate)
            {
                int bytes = checked(Encoding.UTF8.GetByteCount(line) + Encoding.UTF8.GetByteCount(log.NewLine));
                Need(bytes <= MaximumProtocolBytes - total, "protocol_capacity");
                log.WriteLine(line);
                log.Flush();
                total += bytes;
            }
        }

        public async Task<JsonElement> Call(string method, object arguments, CancellationToken token)
        {
            int id = Interlocked.Increment(ref sequence);
            TaskCompletionSource<JsonElement> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                if (method == "Browser.close")
                {
                    Need(closeCommandId == -1, "duplicate_close");
                    closeCommandId = id;
                }

                pending.Add(id, result);
            }

            string message = JsonSerializer.Serialize(new
            {
                id,
                method,
                @params = arguments
            });
            Log("send", message);
            await sender.WaitAsync(token);
            try
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(message).AsMemory(), WebSocketMessageType.Text, true, token);
            }
            finally
            {
                sender.Release();
            }

            var value = await result.Task.WaitAsync(token);
            Need(!value.TryGetProperty("error", out _), "protocol_command " + method + " " + value.GetRawText());
            return value.GetProperty("result").Clone();
        }

        private async Task Read()
        {
            Exception? failure = null;
            try
            {
                byte[] buffer = new byte[65536];
                while (!stop.IsCancellationRequested)
                {
                    using MemoryStream message = new();
                    ValueWebSocketReceiveResult part;
                    lock (gate)
                    {
                        messageBytes = 0;
                        fragmentInProgress = false;
                    }

                    do
                    {
                        part = await socket.ReceiveAsync(buffer.AsMemory(), stop.Token);
                        if (part.MessageType == WebSocketMessageType.Close)
                        {
                            Need(message.Length == 0 && !fragmentInProgress, "protocol_closed_mid_message");
                            return;
                        }

                        Need(part.MessageType == WebSocketMessageType.Text && message.Length + part.Count <= 16 * 1024 * 1024, "protocol_message_capacity");
                        message.Write(buffer, 0, part.Count);
                        lock (gate)
                        {
                            messageBytes = message.Length;
                            fragmentInProgress = !part.EndOfMessage;
                        }
                    }
                    while (!part.EndOfMessage);
                    string text = new UTF8Encoding(false, true).GetString(message.ToArray());
                    Log("receive", text);
                    using var document = JsonDocument.Parse(text);
                    var value = document.RootElement;
                    if (value.TryGetProperty("id", out var id))
                    {
                        TaskCompletionSource<JsonElement>? command;
                        lock (gate)
                        {
                            pending.Remove(id.GetInt32(), out command);
                            Need(command is not null, "unknown_command");
                            // Latch the exact successful response in the receive
                            // path, before another receive can observe EOF.
                            if (id.GetInt32() == closeCommandId && !value.TryGetProperty("error", out _) && value.TryGetProperty("result", out var response) && response.ValueKind == JsonValueKind.Object)
                                closeAcknowledged = true;
                        }

                        command!.TrySetResult(value.Clone());
                    }
                    else if (value.TryGetProperty("method", out var method))
                        receive(method.GetString()!, value.GetProperty("params"));
                    lock (gate)
                    {
                        messageBytes = 0;
                        fragmentInProgress = false;
                    }
                }
            }
            catch (Exception error)
            {
                failure = error;
                throw;
            }
            finally
            {
                lock (gate)
                {
                    // Capture the state before fault handling clears commands.
                    pendingAtTermination = pending.Count;
                    partialBytesAtTermination = messageBytes;
                    fragmentAtTermination = fragmentInProgress;
                    stoppedAtTermination = stop.IsCancellationRequested;
                    terminationFailure = failure;
                    foreach (var command in pending.Values)
                        command.TrySetException(failure ?? new IOException("disk_browser_debugger_closed"));
                    pending.Clear();
                }
            }
        }

        public async Task Join(ProcessResult completedBrowser, CancellationToken cancellation = default)
        {
            QemuTranscripts.RequireCompleteProcess(completedBrowser, "disk_browser_process_incomplete");
            Need(completedBrowser.Succeeded, "browser_exit");
            cancellation.ThrowIfCancellationRequested();
            Need(pump is not null, "protocol_not_connected");
            bool ExpectedEof(WebSocketException error)
            {
                lock (gate)
                    return error.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely && ReferenceEquals(error, terminationFailure) && closeAcknowledged && pendingAtTermination == 0 && partialBytesAtTermination == 0 && !fragmentAtTermination && !stoppedAtTermination && !cancellation.IsCancellationRequested;
            }

            try
            {
                await pump!.WaitAsync(TimeSpan.FromSeconds(10), cancellation);
            }
            catch (WebSocketException error) when (ExpectedEof(error))
            {
                lock (gate)
                    acceptedExpectedEof = true;
            }

            cancellation.ThrowIfCancellationRequested();
            lock (gate)
                Need(closeAcknowledged && pendingAtTermination == 0 && partialBytesAtTermination == 0 && !fragmentAtTermination && !stoppedAtTermination, "protocol_shutdown_incomplete");
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            socket.Abort();
            if (pump is not null)
                try
                {
                    await pump;
                }
                catch
                {
                }

            socket.Dispose();
            stop.Dispose();
            sender.Dispose();
            await log.DisposeAsync();
        }
    }
}
