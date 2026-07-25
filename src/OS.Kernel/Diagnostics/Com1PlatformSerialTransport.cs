using OS.Kernel.Platform;

namespace OS.Kernel.Diagnostics;

public sealed class Com1PlatformSerialTransport : IPlatformSerialTransport
{
    private const int StatusBits = 8;
    private const int CounterBits = 16;
    private const ulong CounterMask = 0xffff;

    private readonly Com1SerialWriter _writer;
    private readonly bool _requireResolvedBackend;
    private readonly OwnedSerialDiagnosticsReadinessResult _readiness;

    public Com1PlatformSerialTransport(IBytePortBackend portBackend)
        : this(new Com1SerialWriter(portBackend), default, requireResolvedBackend: false)
    {
    }

    public Com1PlatformSerialTransport(
        IBytePortBackend portBackend,
        SerialPortConfiguration configuration,
        int transmitReadyPollLimit)
        : this(
            new Com1SerialWriter(portBackend, configuration, transmitReadyPollLimit),
            default,
            requireResolvedBackend: false)
    {
    }

    public Com1PlatformSerialTransport(
        IBytePortBackend portBackend,
        OwnedSerialDiagnosticsReadinessResult readiness)
        : this(new Com1SerialWriter(portBackend), readiness, requireResolvedBackend: true)
    {
    }

    private Com1PlatformSerialTransport(
        Com1SerialWriter writer,
        OwnedSerialDiagnosticsReadinessResult readiness,
        bool requireResolvedBackend)
    {
        _writer = writer;
        _readiness = readiness;
        _requireResolvedBackend = requireResolvedBackend;
    }

    public PlatformSerialTransportKind Kind => PlatformSerialTransportKind.Owned;

    public PlatformSerialTransportResult Write(ReadOnlySpan<byte> bytes)
    {
        if (_requireResolvedBackend && !_readiness.HasResolvedBackendAddresses)
        {
            return PlatformSerialTransportResult.Unavailable();
        }

        if (!_writer.IsInitialized)
        {
            Com1SerialWriterResult initialize = _writer.Initialize();
            if (!initialize.Succeeded)
            {
                return PlatformSerialTransportResult.BackendFailure(
                    EncodeBackendStatus(initialize));
            }
        }

        Com1SerialWriterResult write = _writer.WriteAscii(bytes);
        if (write.Succeeded)
        {
            return PlatformSerialTransportResult.Written(
                write.BytesWritten,
                EncodeBackendStatus(write));
        }

        if (write.BytesWritten > 0)
        {
            return PlatformSerialTransportResult.Written(
                write.BytesWritten,
                EncodeBackendStatus(write));
        }

        return PlatformSerialTransportResult.BackendFailure(
            EncodeBackendStatus(write));
    }

    public static ulong EncodeBackendStatus(Com1SerialWriterResult result)
    {
        ulong writerStatus = (ulong)result.Status & 0xff;
        ulong configurationStatus = ((ulong)result.ConfigurationStatus & 0xff) << StatusBits;
        ulong bytesWritten = ClampCounter(result.BytesWritten) << (StatusBits * 2);
        ulong portWrites = ClampCounter(result.PortWritesEmitted) << (StatusBits * 2 + CounterBits);
        ulong linePolls = ClampCounter(result.LineStatusPolls) << (StatusBits * 2 + CounterBits * 2);

        return writerStatus
            | configurationStatus
            | bytesWritten
            | portWrites
            | linePolls;
    }

    private static ulong ClampCounter(int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        ulong unsigned = (ulong)value;
        return unsigned > CounterMask ? CounterMask : unsigned;
    }
}
