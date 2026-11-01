using OS.Pal;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppExitLogObserverResult
{
    public const uint NoRecordIndex = uint.MaxValue;
    private NativeAppExitLogObserverResult(NativeAppExitLogObserverStatus status, bool isInitialized, int recordsScanned, uint recordIndex, uint streamId, uint flags, uint sequence, uint bytesRead, uint requiredBytes, OSHostStatus logStatus, bool parseAttempted, NativeAppExitParseStatus parseStatus, int exitCode, OSHostStatus exitStatus, bool launcherFailure)
    {
        Status = status;
        IsInitialized = isInitialized;
        RecordsScanned = recordsScanned;
        RecordIndex = recordIndex;
        StreamId = streamId;
        Flags = flags;
        Sequence = sequence;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        LogStatus = logStatus;
        ParseAttempted = parseAttempted;
        ParseStatus = parseStatus;
        ExitCode = exitCode;
        ExitStatus = exitStatus;
        LauncherFailure = launcherFailure;
    }

    public NativeAppExitLogObserverStatus Status { get; }
    public bool IsInitialized { get; }
    public int RecordsScanned { get; }
    public uint RecordIndex { get; }
    public uint StreamId { get; }
    public uint Flags { get; }
    public uint Sequence { get; }
    public uint BytesRead { get; }
    public uint RequiredBytes { get; }
    public OSHostStatus LogStatus { get; }
    public bool ParseAttempted { get; }
    public NativeAppExitParseStatus ParseStatus { get; }
    public int ExitCode { get; }
    public OSHostStatus ExitStatus { get; }
    public bool LauncherFailure { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppExitLogObserverStatus.Success;

    public static NativeAppExitLogObserverResult Success(int recordsScanned, uint recordIndex, OSHostAppLogReadResult read, NativeAppExitParseResult parse)
    {
        return new NativeAppExitLogObserverResult(NativeAppExitLogObserverStatus.Success, isInitialized: true, recordsScanned, recordIndex, read.StreamId, read.Flags, read.Sequence, read.BytesCopied, read.RequiredBytes, read.Status, parseAttempted: true, parse.Status, parse.Record.ExitCode, parse.Record.Status, parse.Record.LauncherFailure);
    }

    public static NativeAppExitLogObserverResult NoExitMarker(int recordsScanned)
    {
        return new NativeAppExitLogObserverResult(NativeAppExitLogObserverStatus.NoExitMarker, isInitialized: true, recordsScanned, NoRecordIndex, streamId: 0, flags: 0, sequence: 0, bytesRead: 0, requiredBytes: 0, OSHostStatus.NotFound, parseAttempted: false, NativeAppExitParseStatus.InvalidPrefix, exitCode: 0, OSHostStatus.NotFound, launcherFailure: false);
    }

    public static NativeAppExitLogObserverResult InvalidLogTable()
    {
        return new NativeAppExitLogObserverResult(NativeAppExitLogObserverStatus.InvalidLogTable, isInitialized: true, recordsScanned: 0, NoRecordIndex, streamId: 0, flags: 0, sequence: 0, bytesRead: 0, requiredBytes: 0, OSHostStatus.InvalidArgument, parseAttempted: false, NativeAppExitParseStatus.InvalidPrefix, exitCode: 0, OSHostStatus.InvalidArgument, launcherFailure: false);
    }

    public static NativeAppExitLogObserverResult FromEntryFailure(int recordsScanned, uint recordIndex, OSHostStatus logStatus)
    {
        return new NativeAppExitLogObserverResult(NativeAppExitLogObserverStatus.InvalidRecord, isInitialized: true, recordsScanned, recordIndex, streamId: 0, flags: 0, sequence: 0, bytesRead: 0, requiredBytes: 0, logStatus, parseAttempted: false, NativeAppExitParseStatus.InvalidPrefix, exitCode: 0, logStatus, launcherFailure: false);
    }

    public static NativeAppExitLogObserverResult FromReadFailure(NativeAppExitLogObserverStatus status, int recordsScanned, uint recordIndex, OSHostAppLogReadResult read)
    {
        return new NativeAppExitLogObserverResult(status, isInitialized: true, recordsScanned, recordIndex, read.StreamId, read.Flags, read.Sequence, read.BytesCopied, read.RequiredBytes, read.Status, parseAttempted: false, NativeAppExitParseStatus.InvalidPrefix, exitCode: 0, read.Status, launcherFailure: false);
    }

    public static NativeAppExitLogObserverResult FromMalformedMarker(int recordsScanned, uint recordIndex, OSHostAppLogReadResult read, NativeAppExitParseResult parse)
    {
        return new NativeAppExitLogObserverResult(NativeAppExitLogObserverStatus.MalformedExitMarker, isInitialized: true, recordsScanned, recordIndex, read.StreamId, read.Flags, read.Sequence, read.BytesCopied, read.RequiredBytes, read.Status, parseAttempted: true, parse.Status, exitCode: 0, OSHostStatus.InvalidArgument, launcherFailure: false);
    }
}
