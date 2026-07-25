using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Net;

internal static class HttpStaticTcpResponseBootSequenceTests
{
    private const int TcpSegmentOffset = 16;
    private const ushort LocalPort = 8080;
    private const ushort RemotePort = 49152;
    private const uint LocalNextSequence = 0x1020_3040;
    private const uint RemoteNextSequence = 0x5060_7080;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "boot.sequence http static tcp response emits ready marker",
            TestEmitsReadyMarker,
            ref failures);
        Run(
            "boot.sequence http static tcp response emits blocked marker",
            TestEmitsBlockedMarker,
            ref failures);
        Run(
            "boot.sequence http static tcp response reports formatter failure",
            TestReportsFormatterFailure,
            ref failures);
        Run(
            "boot.sequence http static tcp response preserves default markers",
            TestPreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "HTTP static TCP response BootSequence host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestEmitsReadyMarker()
    {
        HttpStaticTcpResponsePlanResult response = CreateSuccessfulResponsePlan();
        string diagnostic = FormatDiagnostic(response);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult boot = BootSequence.RunHttpStaticTcpResponseReadiness(response, serial, events);
        string[] snapshot = events.Snapshot();

        return response.Succeeded
            && boot.Succeeded
            && serial.Count == 2
            && serial.LineAt(0) == diagnostic
            && serial.LineAt(0).StartsWith("HTTP_STATIC_TCP_RESPONSE status=Success", StringComparison.Ordinal)
            && serial.LineAt(0).Contains(" opensSocket=false", StringComparison.Ordinal)
            && serial.LineAt(0).Contains(" writesTcpFrame=false", StringComparison.Ordinal)
            && serial.LineAt(0).Contains(" performsNicIo=false", StringComparison.Ordinal)
            && serial.LineAt(0).Contains(" startsKestrel=false", StringComparison.Ordinal)
            && serial.LineAt(1) == BootSequence.HttpStaticTcpResponseReady
            && snapshot.Length == 2
            && snapshot[0] == serial.LineAt(0)
            && snapshot[1] == BootSequence.HttpStaticTcpResponseReady;
    }

    private static bool TestEmitsBlockedMarker()
    {
        HttpStaticTcpResponsePlanResult response = CreateUninitializedTcpDispatchResponsePlan();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult boot = BootSequence.RunHttpStaticTcpResponseReadiness(response, serial, events);

        return response.IsInitialized
            && !response.Succeeded
            && response.Status == HttpStaticTcpResponsePlanStatus.TcpDispatchNotInitialized
            && !boot.Succeeded
            && boot.Reason.Contains(
                nameof(HttpStaticTcpResponseReadinessStatus.ResponsePlanFailed),
                StringComparison.Ordinal)
            && boot.Reason.Contains(
                nameof(HttpStaticTcpResponsePlanStatus.TcpDispatchNotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0).StartsWith(
                BootSequence.HttpStaticTcpResponseNotReadyPrefix,
                StringComparison.Ordinal)
            && serial.LineAt(0).Contains("status=ResponsePlanFailed", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("plan=TcpDispatchNotInitialized", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("tcp=Success", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("http=Success", StringComparison.Ordinal)
            && events.Snapshot()[0] == serial.LineAt(0);
    }

    private static bool TestReportsFormatterFailure()
    {
        HttpStaticTcpResponsePlanResult response = HttpStaticTcpResponsePlanResult.Fail(
            HttpStaticTcpResponsePlanStatus.Success,
            CreatePayloadDispatch(requestLength: 8),
            default,
            requestPayloadOffset: TcpSegmentOffset + TcpHeader.MinHeaderLength,
            requestPayloadLength: 8);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult boot = BootSequence.RunHttpStaticTcpResponseReadiness(response, serial, events);

        return response.IsInitialized
            && response.Succeeded
            && !boot.Succeeded
            && boot.Reason.Contains(
                nameof(HttpStaticTcpResponseReadinessStatus.DiagnosticFormattingFailure),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0).StartsWith(
                BootSequence.HttpStaticTcpResponseNotReadyPrefix,
                StringComparison.Ordinal)
            && serial.LineAt(0).Contains("status=DiagnosticFormattingFailure", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("plan=Success", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("format=InvalidResult", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("httpStatus=0", StringComparison.Ordinal)
            && events.Snapshot()[0] == serial.LineAt(0);
    }

    private static bool TestPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.HttpStaticTcpResponseReady)
            && !serial.ContainsPrefix(BootSequence.HttpStaticTcpResponseNotReadyPrefix)
            && !serial.ContainsPrefix("HTTP_STATIC_TCP_RESPONSE status=");
    }

    private static HttpStaticTcpResponsePlanResult CreateSuccessfulResponsePlan()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\nConnection: close\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        TcpReceiveDispatchResult dispatch = CreatePayloadDispatch(request.Length);
        byte[] responseDestination = new byte[256];

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            receive,
            dispatch,
            "/healthz"u8,
            "OS_MINIMAL_API_OK"u8,
            "missing"u8,
            "bad method"u8,
            "text/plain"u8,
            responseDestination,
            out HttpStaticTcpResponsePlanResult result);
        if (status != HttpStaticTcpResponsePlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create successful HTTP static TCP response plan.");
        }

        return result;
    }

    private static HttpStaticTcpResponsePlanResult CreateUninitializedTcpDispatchResponsePlan()
    {
        Span<byte> responseDestination = stackalloc byte[128];
        HttpStaticTcpResponsePlan.TryHandle(
            ReadOnlySpan<byte>.Empty,
            default,
            "/healthz"u8,
            "OK"u8,
            "missing"u8,
            "bad method"u8,
            "text/plain"u8,
            responseDestination,
            out HttpStaticTcpResponsePlanResult result);
        return result;
    }

    private static TcpReceiveDispatchResult CreatePayloadDispatch(int requestLength)
    {
        return TcpReceiveDispatchResult.Success(
            TcpReceiveDispatchAction.PayloadAccepted,
            default,
            CreateTcpHeader(),
            TcpSegmentOffset,
            TcpHeader.MinHeaderLength + requestLength,
            requestLength,
            ackBytesWritten: 0,
            TcpConnectionCloseState.Established,
            LocalNextSequence,
            RemoteNextSequence + (uint)requestLength);
    }

    private static TcpHeader CreateTcpHeader()
    {
        return new TcpHeader(
            RemotePort,
            LocalPort,
            RemoteNextSequence,
            LocalNextSequence,
            TcpHeader.MinHeaderLength,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            windowSize: 0x4000,
            checksum: 0,
            urgentPointer: 0);
    }

    private static byte[] CreateReceiveBuffer(ReadOnlySpan<byte> request)
    {
        byte[] receive = new byte[TcpSegmentOffset + TcpHeader.MinHeaderLength + request.Length];
        request.CopyTo(receive.AsSpan(TcpSegmentOffset + TcpHeader.MinHeaderLength));
        return receive;
    }

    private static string FormatDiagnostic(HttpStaticTcpResponsePlanResult response)
    {
        HttpStaticTcpResponsePlanDiagnosticFormatResult query =
            HttpStaticTcpResponsePlanDiagnosticFormatter.QueryRequiredBytes(response);
        byte[] destination = new byte[query.RequiredBytes];
        HttpStaticTcpResponsePlanDiagnosticFormatResult format =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(response, destination);
        if (!format.Succeeded)
        {
            throw new InvalidOperationException("Failed to format HTTP static TCP response marker.");
        }

        return Encoding.ASCII.GetString(destination, 0, format.BytesWritten);
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool Contains(string line)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index] == line)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsPrefix(string prefix)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
