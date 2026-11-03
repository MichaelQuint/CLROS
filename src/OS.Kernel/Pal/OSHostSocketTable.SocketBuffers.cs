namespace OS.Pal;

public sealed partial class OSHostSocketTable
{
    private sealed class SocketBuffers
    {
        private readonly byte[] inbound;
        private readonly byte[] outbound;
        private int inboundHead;
        private int inboundLength;
        private int outboundHead;
        private int outboundLength;
        public SocketBuffers(int capacity)
        {
            inbound = new byte[capacity];
            outbound = new byte[capacity];
        }

        public int InboundLength => inboundLength;
        public int InboundAvailable => inbound.Length - inboundLength;
        public int OutboundLength => outboundLength;
        public int OutboundAvailable => outbound.Length - outboundLength;

        public int WriteInbound(ReadOnlySpan<byte> source)
        {
            return Write(source, inbound, ref inboundHead, ref inboundLength);
        }

        public int ReadInbound(Span<byte> destination)
        {
            return Read(destination, inbound, ref inboundHead, ref inboundLength);
        }

        public int WriteOutbound(ReadOnlySpan<byte> source)
        {
            return Write(source, outbound, ref outboundHead, ref outboundLength);
        }

        public int ReadOutbound(Span<byte> destination)
        {
            return Read(destination, outbound, ref outboundHead, ref outboundLength);
        }

        private static int Write(ReadOnlySpan<byte> source, byte[] buffer, ref int head, ref int length)
        {
            int count = Math.Min(source.Length, buffer.Length - length);
            for (int i = 0; i < count; i++)
            {
                int tail = (head + length) % buffer.Length;
                buffer[tail] = source[i];
                length++;
            }

            return count;
        }

        private static int Read(Span<byte> destination, byte[] buffer, ref int head, ref int length)
        {
            int count = Math.Min(destination.Length, length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = buffer[head];
                buffer[head] = 0;
                head = (head + 1) % buffer.Length;
                length--;
            }

            if (length == 0)
            {
                head = 0;
            }

            return count;
        }
    }
}
