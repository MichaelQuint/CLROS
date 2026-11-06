using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Serialization;

namespace OS.Tools.QemuRunner;
/// <summary>One same-user, single-use rendezvous between an owned runner and
/// its HTTP producer. READY follows ordinary controls; DONE follows producer
/// cleanup. These copied values coordinate lifetimes only and grant no file,
/// process, packet, clock or guest authority. No IPC starts during validation.</summary>

public static partial class WebRaceRendezvous
{
    private const string Prefix = "clros-web-race-";
    private const string EventPrefix = "clros-web-race-v3-";
    private const int MaximumFrameBytes = 512;
    /// <summary>Validate exactly the prefix and 32 lowercase hexadecimal
    /// characters. Throws on malformed names without opening a pipe.</summary>

    public static void ValidatePipeName(string name) => ValidateName(name, false);
    /// <summary>Validate only the explicit v3 prefix and 32 lowercase hex
    /// characters, without IPC. Legacy pipe names cannot select this profile.</summary>

    public static void ValidateEventOwnedPipeName(string name) => ValidateName(name, true);
    private static void ValidateName(string name, bool eventOwned)
    {
        string prefix = eventOwned ? EventPrefix : Prefix;
        if (name is null || name.Length != prefix.Length + 32 || !name.StartsWith(prefix, StringComparison.Ordinal) || name.AsSpan(prefix.Length).ContainsAnyExcept("0123456789abcdef"))
            throw new ArgumentException("Invalid web race pipe name.", nameof(name));
    }

    /// <summary>Create the fresh single-instance asynchronous server before
    /// launching its runner. The caller must asynchronously dispose the owner,
    /// including on cancellation; disposal closes and joins its pending I/O.</summary>

    public static Producer CreateProducer() => new(Prefix + Guid.NewGuid().ToString("N"), false);
    /// <summary>Create a fresh v3-only server. Its owner must dispose and join
    /// it on every path as for the historical producer; profile identity grants
    /// no independent file, process, clock or execution authority.</summary>

    public static Producer CreateEventOwnedProducer() => new(EventPrefix + Guid.NewGuid().ToString("N"), true);
    /// <summary>After ordinary controls, connect, publish READY and keep the
    /// runner alive until exact DONE. A positive duration at most 45 seconds
    /// bounds all I/O; the caller clamps it to its remaining absolute deadline.
    /// Health is polled at most 50ms apart while awaiting I/O. On every failure,
    /// close/cancel and join the actual pending operation before rethrowing.</summary>

    public static Task<Report> WaitInRunnerAsync(string name, int port, int pid, TimeSpan maximumDuration, Func<bool>? isHealthy = null, CancellationToken cancellation = default) => WaitInRunnerCore(name, port, pid, maximumDuration, false, null, isHealthy, cancellation);
    /// <summary>Explicit v3 rendezvous with a positive at most165s whole IPC
    /// lifetime, starting before connect. Borrow the owning runner's original
    /// absolute-clock remaining callback; require at least165s at entry and
    /// immediately after connect before emitting READY. Refuse insufficient
    /// budget without publishing READY or refreshing either clock. The callback
    /// and cancellation state must outlive this joined call. All I/O, health,
    /// cancellation and failure cleanup follow the historical owner contract.
    /// This admission policy supplies no measured runtime or authenticated clock.</summary>

    public static Task<Report> WaitEventOwnedInRunnerAsync(string name, int port, int pid, TimeSpan maximumDuration, Func<TimeSpan> remainingAbsolute, Func<bool>? isHealthy = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(remainingAbsolute);
        return WaitInRunnerCore(name, port, pid, maximumDuration, true, remainingAbsolute, isHealthy, cancellation);
    }

    private static async Task<Report> WaitInRunnerCore(string name, int port, int pid, TimeSpan maximumDuration, bool eventOwned, Func<TimeSpan>? remainingAbsolute, Func<bool>? isHealthy, CancellationToken cancellation)
    {
        ValidateName(name, eventOwned);
        ValidatePort(port);
        if (pid <= 0)
            throw new ArgumentOutOfRangeException(nameof(pid));
        if (maximumDuration <= TimeSpan.Zero || maximumDuration > TimeSpan.FromSeconds(eventOwned ? 165 : 45))
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        lifetime.CancelAfter(maximumDuration);
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Task? pending = null;
        try
        {
            CheckHealth();
            lifetime.Token.ThrowIfCancellationRequested();
            CheckBudget();
            pending = pipe.ConnectAsync(lifetime.Token);
            await Monitored(pending).ConfigureAwait(false);
            CheckBudget();
            pending = WriteFrame(pipe, $"{ReadyToken(eventOwned)} {port.ToString(CultureInfo.InvariantCulture)} {pid.ToString(CultureInfo.InvariantCulture)}", lifetime.Token);
            await Monitored(pending).ConfigureAwait(false);
            Task<string> reading = ReadFrame(pipe, lifetime.Token);
            pending = reading;
            await Monitored(reading).ConfigureAwait(false);
            string[] fields = reading.Result.Split(' ');
            if (fields.Length != 4 || fields[0] != DoneToken(eventOwned) || !Hash(fields[1]) || !Hash(fields[2]) || !Hash(fields[3]))
                throw new InvalidDataException("Invalid web race DONE.");
            return new(port, pid, new(fields[1], fields[2], fields[3]), eventOwned);
        }
        catch
        {
            try
            {
                lifetime.Cancel();
            }
            catch
            {
            }

            try
            {
                pipe.Dispose();
            }
            catch
            {
            }

            if (pending is not null)
                try
                {
                    await pending.ConfigureAwait(false);
                }
                catch
                {
                }

            throw;
        }

        void CheckHealth()
        {
            if (isHealthy?.Invoke() == false)
                throw new InvalidDataException("Web race runner health failed.");
        }

        void CheckBudget()
        {
            if (eventOwned && remainingAbsolute!() < TimeSpan.FromSeconds(165))
                throw new InvalidDataException("web_race_insufficient_absolute_budget");
        }

        async Task Monitored(Task operation)
        {
            while (!operation.IsCompleted)
            {
                CheckHealth();
                await Task.Delay(50, lifetime.Token).ConfigureAwait(false);
            }

            await operation.ConfigureAwait(false);
            lifetime.Token.ThrowIfCancellationRequested();
            CheckHealth();
        }
    }

    private static string ReadyToken(bool eventOwned) => eventOwned ? "READY_V3" : "READY_V1";
    private static string DoneToken(bool eventOwned) => eventOwned ? "DONE_V3" : "DONE_V1";
    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
    }

    private static bool Positive(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0 && text == value.ToString(CultureInfo.InvariantCulture);
    private static bool Hash(string? value) => value is { Length: 64 } && !value.AsSpan().ContainsAnyExcept("0123456789ABCDEF");
    private static void ValidateDigests(Digests digests)
    {
        if (digests is null || !Hash(digests.SelectedSha256) || !Hash(digests.AttemptsSha256) || !Hash(digests.WireSha256))
            throw new ArgumentException("Invalid web race report digests.", nameof(digests));
    }

    private static async Task WriteFrame(Stream pipe, string text, CancellationToken token)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text + "\r\n");
        if (bytes.Length > MaximumFrameBytes)
            throw new InvalidDataException("Web race frame too long.");
        await pipe.WriteAsync(bytes, token).ConfigureAwait(false);
        await pipe.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task<string> ReadFrame(Stream pipe, CancellationToken token)
    {
        byte[] bytes = new byte[MaximumFrameBytes];
        int count = 0;
        while (count < bytes.Length)
        {
            if (await pipe.ReadAsync(bytes.AsMemory(count, 1), token).ConfigureAwait(false) == 0)
                throw new EndOfStreamException("Web race frame EOF.");
            byte current = bytes[count++];
            if (count > 1 && bytes[count - 2] == (byte)'\r')
            {
                if (current != (byte)'\n')
                    throw new InvalidDataException("Web race frame requires CRLF.");
                return Encoding.ASCII.GetString(bytes, 0, count - 2);
            }

            if (current != (byte)'\r' && (current < 32 || current > 126))
                throw new InvalidDataException("Web race frame requires ASCII CRLF.");
        }

        throw new InvalidDataException("Web race frame too long.");
    }
}
