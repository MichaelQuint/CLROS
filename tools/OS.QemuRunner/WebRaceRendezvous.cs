using System.Globalization;
using System.IO.Pipes;
using System.Text;

namespace OS.Tools.QemuRunner;

/// <summary>One same-user, single-use rendezvous between an owned runner and
/// its HTTP producer. READY follows ordinary controls; DONE follows producer
/// cleanup. These copied values coordinate lifetimes only and grant no file,
/// process, packet, clock or guest authority. No IPC starts during validation.</summary>
public static class WebRaceRendezvous
{
    private const string Prefix = "clros-web-race-";
    private const int MaximumFrameBytes = 512;

    /// <summary>Copied runner-supplied forward and PID; neither is authenticated by IPC.</summary>
    public sealed record Ready(int Port, int QemuProcessId);
    /// <summary>Canonical uppercase report hashes to recheck against independently held originals.</summary>
    public sealed record Digests(string SelectedSha256, string AttemptsSha256, string WireSha256);
    /// <summary>Completed coordination values, without execution or file authority.</summary>
    public sealed record Report(int Port, int QemuProcessId, Digests Digests);

    /// <summary>Validate exactly the prefix and 32 lowercase hexadecimal
    /// characters. Throws on malformed names without opening a pipe.</summary>
    public static void ValidatePipeName(string name)
    {
        if (name is null || name.Length != Prefix.Length + 32 || !name.StartsWith(Prefix, StringComparison.Ordinal) ||
            name.AsSpan(Prefix.Length).ContainsAnyExcept("0123456789abcdef"))
            throw new ArgumentException("Invalid web race pipe name.", nameof(name));
    }

    /// <summary>Create the fresh single-instance asynchronous server before
    /// launching its runner. The caller must asynchronously dispose the owner,
    /// including on cancellation; disposal closes and joins its pending I/O.</summary>
    public static Producer CreateProducer() => new(Prefix + Guid.NewGuid().ToString("N"));

    /// <summary>Own one server and strictly ordered READY then DONE. Methods
    /// reject duplicate/concurrent phases. Failure closes the pipe; disposal
    /// joins the active accept/read/write before releasing cancellation state.</summary>
    public sealed class Producer : IAsyncDisposable
    {
        private readonly object gate = new();
        private readonly CancellationTokenSource stop = new();
        private readonly NamedPipeServerStream pipe;
        private Task? active;
        private Task? disposal;
        private int phase;
        /// <summary>Fresh same-user endpoint name to pass explicitly to the owned runner.</summary>
        public string PipeName { get; }

        internal Producer(string name)
        {
            ValidatePipeName(name); PipeName = name;
            pipe = new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance, MaximumFrameBytes, MaximumFrameBytes);
        }

        /// <summary>Accept one runner and require canonical READY with the
        /// expected forward port and positive QEMU PID. Does not authenticate
        /// that PID. Cancellation or invalid bytes retire this rendezvous.</summary>
        public Task<Ready> WaitForReadyAsync(int expectedPort, CancellationToken cancellation = default)
        {
            ValidatePort(expectedPort);
            lock (gate)
            {
                if (phase != 0) throw new InvalidOperationException("Web race READY out of order.");
                phase = 1;
                Task<Ready> task = WaitCore(expectedPort, cancellation); active = task; return task;
            }
        }

        private async Task<Ready> WaitCore(int port, CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, cancellation);
            try
            {
                await pipe.WaitForConnectionAsync(linked.Token).ConfigureAwait(false);
                string[] fields = (await ReadFrame(pipe, linked.Token).ConfigureAwait(false)).Split(' ');
                if (fields.Length != 3 || fields[0] != "READY_V1" || !Positive(fields[1], out int actualPort) ||
                    actualPort != port || !Positive(fields[2], out int pid)) throw new InvalidDataException("Invalid web race READY.");
                lock (gate)
                {
                    if (phase != 1) throw new ObjectDisposedException(nameof(Producer));
                    phase = 2;
                }
                return new(port, pid);
            }
            catch { Retire(); throw; }
        }

        /// <summary>Send one bounded DONE after the caller has joined its HTTP
        /// producer and retained the original reports. Digests are copied text,
        /// not verified file identities. Invalid inputs do not advance state.</summary>
        public Task CompleteAsync(Digests digests, CancellationToken cancellation = default)
        {
            ValidateDigests(digests);
            lock (gate)
            {
                if (phase != 2) throw new InvalidOperationException("Web race DONE out of order.");
                phase = 3; Task task = CompleteCore(digests, cancellation); active = task; return task;
            }
        }

        private async Task CompleteCore(Digests digests, CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, cancellation);
            try
            {
                await WriteFrame(pipe, $"DONE_V1 {digests.SelectedSha256} {digests.AttemptsSha256} {digests.WireSha256}", linked.Token).ConfigureAwait(false);
                lock (gate)
                {
                    if (phase != 3) throw new ObjectDisposedException(nameof(Producer));
                    phase = 4;
                }
            }
            catch { Retire(); throw; }
        }

        private void Retire()
        {
            lock (gate) { if (phase != 6) phase = 5; }
            try { stop.Cancel(); } catch { }
            try { pipe.Dispose(); } catch { }
        }

        /// <summary>Close and join outstanding I/O. Keep this owner alive until
        /// the runner has consumed DONE and joined; a successful write alone
        /// does not acknowledge that the peer has read its bytes.</summary>
        public ValueTask DisposeAsync()
        {
            lock (gate)
            {
                if (disposal is null)
                {
                    phase = 6; stop.Cancel(); pipe.Dispose(); disposal = DisposeCore(active);
                }
                return new(disposal);
            }
        }

        private async Task DisposeCore(Task? pending)
        {
            try { if (pending is not null) await pending.ConfigureAwait(false); }
            catch { /* The original operation retains its failure. */ }
            finally { stop.Dispose(); }
        }
    }

    /// <summary>After ordinary controls, connect, publish READY and keep the
    /// runner alive until exact DONE. A positive duration at most 45 seconds
    /// bounds all I/O; the caller clamps it to its remaining absolute deadline.
    /// Health is polled at most 50ms apart while awaiting I/O. On every failure,
    /// close/cancel and join the actual pending operation before rethrowing.</summary>
    public static async Task<Report> WaitInRunnerAsync(string name, int port, int pid, TimeSpan maximumDuration,
        Func<bool>? isHealthy = null, CancellationToken cancellation = default)
    {
        ValidatePipeName(name); ValidatePort(port);
        if (pid <= 0) throw new ArgumentOutOfRangeException(nameof(pid));
        if (maximumDuration <= TimeSpan.Zero || maximumDuration > TimeSpan.FromSeconds(45)) throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation); lifetime.CancelAfter(maximumDuration);
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Task? pending = null;
        try
        {
            CheckHealth(); lifetime.Token.ThrowIfCancellationRequested();
            pending = pipe.ConnectAsync(lifetime.Token); await Monitored(pending).ConfigureAwait(false);
            pending = WriteFrame(pipe, $"READY_V1 {port.ToString(CultureInfo.InvariantCulture)} {pid.ToString(CultureInfo.InvariantCulture)}", lifetime.Token);
            await Monitored(pending).ConfigureAwait(false);
            Task<string> reading = ReadFrame(pipe, lifetime.Token); pending = reading; await Monitored(reading).ConfigureAwait(false);
            string[] fields = reading.Result.Split(' ');
            if (fields.Length != 4 || fields[0] != "DONE_V1" || !Hash(fields[1]) || !Hash(fields[2]) || !Hash(fields[3]))
                throw new InvalidDataException("Invalid web race DONE.");
            return new(port, pid, new(fields[1], fields[2], fields[3]));
        }
        catch
        {
            try { lifetime.Cancel(); } catch { }
            try { pipe.Dispose(); } catch { }
            if (pending is not null) try { await pending.ConfigureAwait(false); } catch { }
            throw;
        }

        void CheckHealth() { if (isHealthy?.Invoke() == false) throw new InvalidDataException("Web race runner health failed."); }
        async Task Monitored(Task operation)
        {
            while (!operation.IsCompleted)
            {
                CheckHealth(); await Task.Delay(50, lifetime.Token).ConfigureAwait(false);
            }
            await operation.ConfigureAwait(false); lifetime.Token.ThrowIfCancellationRequested(); CheckHealth();
        }
    }

    private static void ValidatePort(int port) { if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port)); }
    private static bool Positive(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
        value > 0 && text == value.ToString(CultureInfo.InvariantCulture);
    private static bool Hash(string? value) => value is { Length: 64 } && !value.AsSpan().ContainsAnyExcept("0123456789ABCDEF");
    private static void ValidateDigests(Digests digests)
    {
        if (digests is null || !Hash(digests.SelectedSha256) || !Hash(digests.AttemptsSha256) || !Hash(digests.WireSha256))
            throw new ArgumentException("Invalid web race report digests.", nameof(digests));
    }
    private static async Task WriteFrame(Stream pipe, string text, CancellationToken token)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text + "\r\n");
        if (bytes.Length > MaximumFrameBytes) throw new InvalidDataException("Web race frame too long.");
        await pipe.WriteAsync(bytes, token).ConfigureAwait(false); await pipe.FlushAsync(token).ConfigureAwait(false);
    }
    private static async Task<string> ReadFrame(Stream pipe, CancellationToken token)
    {
        byte[] bytes = new byte[MaximumFrameBytes]; int count = 0;
        while (count < bytes.Length)
        {
            if (await pipe.ReadAsync(bytes.AsMemory(count, 1), token).ConfigureAwait(false) == 0) throw new EndOfStreamException("Web race frame EOF.");
            byte current = bytes[count++];
            if (count > 1 && bytes[count - 2] == (byte)'\r')
            {
                if (current != (byte)'\n') throw new InvalidDataException("Web race frame requires CRLF.");
                return Encoding.ASCII.GetString(bytes, 0, count - 2);
            }
            if (current != (byte)'\r' && (current < 32 || current > 126)) throw new InvalidDataException("Web race frame requires ASCII CRLF.");
        }
        throw new InvalidDataException("Web race frame too long.");
    }
}
