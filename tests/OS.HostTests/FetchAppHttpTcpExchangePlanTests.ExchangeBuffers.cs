using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Net;

internal static partial class FetchAppHttpTcpExchangePlanTests
{
    private sealed class ExchangeBuffers
    {
        public ExchangeBuffers(byte[] host, byte[] path, byte[] synAckFrame, byte[] responseFrame, byte[] streamReceiveBuffer, byte[] synFrame, byte[] finalAckFrame, byte[] requestScratch, byte[] requestFrame, byte[] responseDestination, byte[] closeFrame)
        {
            Host = host;
            Path = path;
            SynAckFrame = synAckFrame;
            ResponseFrame = responseFrame;
            StreamReceiveBuffer = streamReceiveBuffer;
            SynFrame = synFrame;
            FinalAckFrame = finalAckFrame;
            RequestScratch = requestScratch;
            RequestFrame = requestFrame;
            ResponseDestination = responseDestination;
            CloseFrame = closeFrame;
        }

        public byte[] Host { get; }
        public byte[] Path { get; }
        public byte[] SynAckFrame { get; }
        public byte[] ResponseFrame { get; }
        public byte[] StreamReceiveBuffer { get; }
        public byte[] SynFrame { get; }
        public byte[] FinalAckFrame { get; }
        public byte[] RequestScratch { get; }
        public byte[] RequestFrame { get; }
        public byte[] ResponseDestination { get; }
        public byte[] CloseFrame { get; }
    }
}
