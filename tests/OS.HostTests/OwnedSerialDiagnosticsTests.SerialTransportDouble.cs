using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;

internal static partial class OwnedSerialDiagnosticsTests
{
    private sealed class SerialTransportDouble : IPlatformSerialTransport
    {
        private readonly PlatformSerialTransportStatus _status;
        private readonly int _missingBytes;
        private readonly ulong _backendStatus;
        private SerialTransportDouble(PlatformSerialTransportKind kind, PlatformSerialTransportStatus status, int missingBytes, ulong backendStatus)
        {
            Kind = kind;
            _status = status;
            _missingBytes = missingBytes < 0 ? 0 : missingBytes;
            _backendStatus = backendStatus;
        }

        public PlatformSerialTransportKind Kind
        {
            get;
        }
        public int WriteCount
        {
            get; private set;
        }
        public byte[] LastBytes { get; private set; } = Array.Empty<byte>();

        public static SerialTransportDouble Success(PlatformSerialTransportKind kind)
        {
            return new SerialTransportDouble(kind, PlatformSerialTransportStatus.Success, 0, 0);
        }

        public static SerialTransportDouble ShortWrite(PlatformSerialTransportKind kind, int missingBytes)
        {
            return new SerialTransportDouble(kind, PlatformSerialTransportStatus.Success, missingBytes, 0);
        }

        public static SerialTransportDouble Unavailable(PlatformSerialTransportKind kind)
        {
            return new SerialTransportDouble(kind, PlatformSerialTransportStatus.Unavailable, 0, 0);
        }

        public PlatformSerialTransportResult Write(ReadOnlySpan<byte> bytes)
        {
            WriteCount++;
            if (_status == PlatformSerialTransportStatus.Unavailable)
            {
                return PlatformSerialTransportResult.Unavailable();
            }

            if (_status == PlatformSerialTransportStatus.BackendFailure)
            {
                return PlatformSerialTransportResult.BackendFailure(_backendStatus);
            }

            int bytesWritten = bytes.Length - _missingBytes;
            if (bytesWritten < 0)
            {
                bytesWritten = 0;
            }

            LastBytes = bytes.Slice(0, bytesWritten).ToArray();
            return PlatformSerialTransportResult.Written(bytesWritten);
        }
    }
}
