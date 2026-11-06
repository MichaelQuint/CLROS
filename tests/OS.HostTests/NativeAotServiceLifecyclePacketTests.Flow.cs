using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotServiceLifecyclePacketTests
{
    private sealed class Flow
    {
        private readonly List<byte[]> frames;
        private readonly ushort port;
        private readonly string mode;
        private uint client, server;
        internal Flow(List<byte[]> frames, ushort port, string mode)
        {
            this.frames = frames;
            this.port = port;
            this.mode = mode;
            client = mode == "wrap" ? uint.MaxValue - 10 : (uint)(1000 + (port - 45000) * 10000);
            server = mode == "wrap" ? uint.MaxValue - 20 : (uint)(2000 + (port - 45000) * 10000);
            Add(false, client, 0, 2);
            Add(true, server, unchecked(client + 1), 0x12);
            client = unchecked(client + 1);
            server = unchecked(server + 1);
            Add(false, client, server, 0x10);
        }

        private void Add(bool outgoing, uint sequence, uint ack, byte flags, ReadOnlySpan<byte> bytes = default) => frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, port, sequence, ack, flags, bytes));
        internal void Send(bool outgoing, ReadOnlySpan<byte> bytes, bool duplicate = false, bool changedRetry = false, bool gap = false)
        {
            for (int at = 0; at < bytes.Length;)
            {
                int count = Math.Min(mode == "segmented" && at == 0 ? 7 : mode == "segmented" ? 317 : 1200, bytes.Length - at);
                uint sequence = outgoing ? server : client, ack = outgoing ? client : server;
                var part = bytes.Slice(at, count);
                if (!(gap && at == 0))
                    Add(outgoing, sequence, ack, 0x18, part);
                if (duplicate && at == 0)
                {
                    byte[] retry = part.ToArray();
                    if (changedRetry)
                        retry[^1] ^= 1;
                    Add(outgoing, sequence, ack, 0x18, retry);
                }

                if (outgoing)
                    server = unchecked(server + (uint)count);
                else
                    client = unchecked(client + (uint)count);
                Add(!outgoing, outgoing ? client : server, outgoing ? server : client, 0x10);
                at += count;
            }
        }

        internal void Close(bool omitFinalAck = false, bool clientFirst = false)
        {
            if (clientFirst)
            {
                Add(false, client, server, 0x11);
                client = unchecked(client + 1);
                Add(true, server, client, 0x10);
                Add(true, server, client, 0x11);
                server = unchecked(server + 1);
                if (!omitFinalAck)
                    Add(false, client, server, 0x10);
                return;
            }

            Add(true, server, client, 0x11);
            server = unchecked(server + 1);
            Add(false, client, server, 0x10);
            Add(false, client, server, 0x11);
            client = unchecked(client + 1);
            if (!omitFinalAck)
                Add(true, server, client, 0x10);
        }

        internal void Reset(bool wrongSequence, bool wrongAck) => Add(false, unchecked(client + (wrongSequence ? 1U : 0)), unchecked(server + (wrongAck ? 1U : 0)), 0x14);
    }
}
