using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Serialization;

namespace OS.Tools.QemuRunner;
public static partial class WebRaceRendezvous
{
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
        /// <summary>Immutable protocol profile selected before the pipe opens.</summary>

        public bool EventOwned { get; }

        internal Producer(string name, bool eventOwned)
        {
            ValidateName(name, eventOwned);
            PipeName = name;
            EventOwned = eventOwned;
            pipe = new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance, MaximumFrameBytes, MaximumFrameBytes);
        }

        /// <summary>Accept one runner and require canonical READY with the
        /// expected forward port and positive QEMU PID. Does not authenticate
        /// that PID. Cancellation or invalid bytes retire this rendezvous.</summary>

        public Task<Ready> WaitForReadyAsync(int expectedPort, CancellationToken cancellation = default)
        {
            ValidatePort(expectedPort);
            lock (gate)
            {
                if (phase != 0)
                    throw new InvalidOperationException("Web race READY out of order.");
                phase = 1;
                Task<Ready> task = WaitCore(expectedPort, cancellation);
                active = task;
                return task;
            }
        }

        private async Task<Ready> WaitCore(int port, CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, cancellation);
            try
            {
                await pipe.WaitForConnectionAsync(linked.Token).ConfigureAwait(false);
                string[] fields = (await ReadFrame(pipe, linked.Token).ConfigureAwait(false)).Split(' ');
                if (fields.Length != 3 || fields[0] != ReadyToken(EventOwned) || !Positive(fields[1], out int actualPort) || actualPort != port || !Positive(fields[2], out int pid))
                    throw new InvalidDataException("Invalid web race READY.");
                lock (gate)
                {
                    if (phase != 1)
                        throw new ObjectDisposedException(nameof(Producer));
                    phase = 2;
                }

                return new(port, pid, EventOwned);
            }
            catch
            {
                Retire();
                throw;
            }
        }

        /// <summary>Send one bounded DONE after the caller has joined its HTTP
        /// producer and retained the original reports. Digests are copied text,
        /// not verified file identities. Invalid inputs do not advance state.</summary>

        public Task CompleteAsync(Digests digests, CancellationToken cancellation = default)
        {
            ValidateDigests(digests);
            lock (gate)
            {
                if (phase != 2)
                    throw new InvalidOperationException("Web race DONE out of order.");
                phase = 3;
                Task task = CompleteCore(digests, cancellation);
                active = task;
                return task;
            }
        }

        private async Task CompleteCore(Digests digests, CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, cancellation);
            try
            {
                await WriteFrame(pipe, $"{DoneToken(EventOwned)} {digests.SelectedSha256} {digests.AttemptsSha256} {digests.WireSha256}", linked.Token).ConfigureAwait(false);
                lock (gate)
                {
                    if (phase != 3)
                        throw new ObjectDisposedException(nameof(Producer));
                    phase = 4;
                }
            }
            catch
            {
                Retire();
                throw;
            }
        }

        private void Retire()
        {
            lock (gate)
            {
                if (phase != 6)
                    phase = 5;
            }

            try
            {
                stop.Cancel();
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
                    phase = 6;
                    stop.Cancel();
                    pipe.Dispose();
                    disposal = DisposeCore(active);
                }

                return new(disposal);
            }
        }

        private async Task DisposeCore(Task? pending)
        {
            try
            {
                if (pending is not null)
                    await pending.ConfigureAwait(false);
            }
            catch
            { /* The original operation retains its failure. */
            }
            finally
            {
                stop.Dispose();
            }
        }
    }
}
