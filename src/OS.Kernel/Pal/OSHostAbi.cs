using System.Runtime.InteropServices;

namespace OS.Pal;

public enum OSHostStatus : int
{
    Ok = 0,
    InvalidArgument = 1,
    NotSupported = 2,
    NotFound = 3,
    AccessDenied = 4,
    BufferTooSmall = 5,
    OutOfMemory = 6,
    WouldBlock = 7,
    TimedOut = 8,
    Interrupted = 9,
    EndOfFile = 10,
    ConnectionReset = 11,
    ConnectionRefused = 12,
    AddressInUse = 13,
    AddressNotAvailable = 14,
    NetworkDown = 15,
    TryAgain = 16,
    IoError = 17,
    InternalError = 18,
    Closed = 19
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostHandle
{
    public readonly ulong Value;

    public OSHostHandle(ulong value)
    {
        Value = value;
    }

    public bool IsValid => Value != 0;
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostByteSpan
{
    public readonly ulong Address;

    public readonly ulong Length;

    public OSHostByteSpan(ulong address, ulong length)
    {
        Address = address;
        Length = length;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostMemoryRequest
{
    public readonly ulong PageCount;

    public readonly ulong AlignmentBytes;

    public readonly ulong ProtectionFlags;

    public OSHostMemoryRequest(ulong pageCount, ulong alignmentBytes, ulong protectionFlags)
    {
        PageCount = pageCount;
        AlignmentBytes = alignmentBytes;
        ProtectionFlags = protectionFlags;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostMemoryRange
{
    public readonly ulong BaseAddress;

    public readonly ulong ByteLength;

    public readonly ulong ProtectionFlags;

    public OSHostMemoryRange(ulong baseAddress, ulong byteLength, ulong protectionFlags)
    {
        BaseAddress = baseAddress;
        ByteLength = byteLength;
        ProtectionFlags = protectionFlags;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostMemoryQueryResult
{
    public readonly OSHostMemoryRange Range;

    public readonly ulong PageSizeBytes;

    public readonly ulong StateFlags;

    public OSHostMemoryQueryResult(OSHostMemoryRange range, ulong pageSizeBytes, ulong stateFlags)
    {
        Range = range;
        PageSizeBytes = pageSizeBytes;
        StateFlags = stateFlags;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostTimeSnapshot
{
    public readonly ulong MonotonicTicks;

    public readonly ulong TickFrequency;

    public readonly long WallClockUnixNanoseconds;

    public OSHostTimeSnapshot(ulong monotonicTicks, ulong tickFrequency, long wallClockUnixNanoseconds)
    {
        MonotonicTicks = monotonicTicks;
        TickFrequency = tickFrequency;
        WallClockUnixNanoseconds = wallClockUnixNanoseconds;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostLogRecord
{
    public readonly uint Level;

    public readonly uint Flags;

    public readonly OSHostByteSpan Subsystem;

    public readonly OSHostByteSpan Message;

    public OSHostLogRecord(uint level, uint flags, OSHostByteSpan subsystem, OSHostByteSpan message)
    {
        Level = level;
        Flags = flags;
        Subsystem = subsystem;
        Message = message;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostWriteLogBytesRequest
{
    public readonly uint StreamId;

    public readonly uint Flags;

    public readonly OSHostByteSpan Buffer;

    public readonly ulong Reserved;

    public OSHostWriteLogBytesRequest(uint streamId, uint flags, OSHostByteSpan buffer, ulong reserved)
    {
        StreamId = streamId;
        Flags = flags;
        Buffer = buffer;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostRandomBytesRequest
{
    public readonly OSHostByteSpan Destination;

    public readonly uint Flags;

    public readonly uint Reserved;

    public OSHostRandomBytesRequest(OSHostByteSpan destination, uint flags, uint reserved)
    {
        Destination = destination;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostFileInfo
{
    public readonly ulong ByteLength;

    public readonly ulong Attributes;

    public readonly long ModifiedUnixNanoseconds;

    public OSHostFileInfo(ulong byteLength, ulong attributes, long modifiedUnixNanoseconds)
    {
        ByteLength = byteLength;
        Attributes = attributes;
        ModifiedUnixNanoseconds = modifiedUnixNanoseconds;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostBlockReadRequest
{
    public readonly OSHostHandle Device;

    public readonly ulong FirstBlock;

    public readonly uint BlockCount;

    public readonly uint BlockSizeBytes;

    public readonly OSHostByteSpan Destination;

    public OSHostBlockReadRequest(OSHostHandle device, ulong firstBlock, uint blockCount, uint blockSizeBytes, OSHostByteSpan destination)
    {
        Device = device;
        FirstBlock = firstBlock;
        BlockCount = blockCount;
        BlockSizeBytes = blockSizeBytes;
        Destination = destination;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostPackagePath
{
    public readonly OSHostByteSpan PathUtf8;

    public readonly ulong Flags;

    public OSHostPackagePath(OSHostByteSpan pathUtf8, ulong flags)
    {
        PathUtf8 = pathUtf8;
        Flags = flags;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostConfigQuery
{
    public readonly OSHostByteSpan KeyUtf8;

    public readonly OSHostByteSpan Destination;

    public OSHostConfigQuery(OSHostByteSpan keyUtf8, OSHostByteSpan destination)
    {
        KeyUtf8 = keyUtf8;
        Destination = destination;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostEnvironmentEnumerationQuery
{
    public readonly uint Index;

    public readonly uint Reserved;

    public readonly OSHostByteSpan KeyDestination;

    public readonly OSHostByteSpan ValueDestination;

    public OSHostEnvironmentEnumerationQuery(
        uint index,
        uint reserved,
        OSHostByteSpan keyDestination,
        OSHostByteSpan valueDestination)
    {
        Index = index;
        Reserved = reserved;
        KeyDestination = keyDestination;
        ValueDestination = valueDestination;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketAddress
{
    public readonly ushort Family;

    public readonly ushort PortNetworkOrder;

    public readonly uint FlowInfo;

    public readonly ulong ScopeId;

    public readonly ulong AddressHigh;

    public readonly ulong AddressLow;

    public OSHostSocketAddress(ushort family, ushort portNetworkOrder, uint flowInfo, ulong scopeId, ulong addressHigh, ulong addressLow)
    {
        Family = family;
        PortNetworkOrder = portNetworkOrder;
        FlowInfo = flowInfo;
        ScopeId = scopeId;
        AddressHigh = addressHigh;
        AddressLow = addressLow;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketCreateOptions
{
    public readonly uint Domain;

    public readonly uint Type;

    public readonly uint Protocol;

    public readonly uint Flags;

    public readonly uint Version;

    public readonly uint Reserved;

    public OSHostSocketCreateOptions(uint domain, uint type, uint protocol, uint flags, uint version, uint reserved)
    {
        Domain = domain;
        Type = type;
        Protocol = protocol;
        Flags = flags;
        Version = version;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketIoRequest
{
    public readonly OSHostHandle Socket;

    public readonly OSHostByteSpan Buffer;

    public readonly uint Flags;

    public readonly uint Reserved;

    public OSHostSocketIoRequest(OSHostHandle socket, OSHostByteSpan buffer, uint flags, uint reserved)
    {
        Socket = socket;
        Buffer = buffer;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketPollEntry
{
    public readonly OSHostHandle Socket;

    public readonly uint RequestedEvents;

    public readonly uint ObservedEvents;

    public readonly int StatusCode;

    public readonly uint Flags;

    public OSHostSocketPollEntry(OSHostHandle socket, uint requestedEvents, uint observedEvents, int statusCode, uint flags)
    {
        Socket = socket;
        RequestedEvents = requestedEvents;
        ObservedEvents = observedEvents;
        StatusCode = statusCode;
        Flags = flags;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostTimeout
{
    public readonly uint Kind;

    public readonly uint Flags;

    public readonly ulong Ticks;

    public readonly ulong TickFrequency;

    public OSHostTimeout(uint kind, uint flags, ulong ticks, ulong tickFrequency)
    {
        Kind = kind;
        Flags = flags;
        Ticks = ticks;
        TickFrequency = tickFrequency;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostTimerOptions
{
    public readonly uint Version;

    public readonly uint Flags;

    public readonly ulong DueTicks;

    public readonly ulong PeriodTicks;

    public readonly OSHostHandle CompletionWaitHandle;

    public OSHostTimerOptions(uint version, uint flags, ulong dueTicks, ulong periodTicks, OSHostHandle completionWaitHandle)
    {
        Version = version;
        Flags = flags;
        DueTicks = dueTicks;
        PeriodTicks = periodTicks;
        CompletionWaitHandle = completionWaitHandle;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostThreadStart
{
    public readonly ulong EntryPoint;

    public readonly ulong Context;

    public OSHostThreadStart(ulong entryPoint, ulong context)
    {
        EntryPoint = entryPoint;
        Context = context;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostThreadCreateOptions
{
    public readonly uint Version;

    public readonly uint Flags;

    public readonly ulong StackSizeBytes;

    public readonly ulong GuardSizeBytes;

    public readonly OSHostByteSpan NameUtf8;

    public readonly OSHostThreadStart Start;

    public OSHostThreadCreateOptions(uint version, uint flags, ulong stackSizeBytes, ulong guardSizeBytes, OSHostByteSpan nameUtf8, OSHostThreadStart start)
    {
        Version = version;
        Flags = flags;
        StackSizeBytes = stackSizeBytes;
        GuardSizeBytes = guardSizeBytes;
        NameUtf8 = nameUtf8;
        Start = start;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostWaitOneRequest
{
    public readonly OSHostHandle Waitable;

    public readonly OSHostTimeout Timeout;

    public readonly uint Flags;

    public readonly uint Reserved;

    public OSHostWaitOneRequest(OSHostHandle waitable, OSHostTimeout timeout, uint flags, uint reserved)
    {
        Waitable = waitable;
        Timeout = timeout;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostWaitManyRequest
{
    public readonly OSHostByteSpan Waitables;

    public readonly OSHostTimeout Timeout;

    public readonly uint Flags;

    public readonly uint Reserved;

    public OSHostWaitManyRequest(OSHostByteSpan waitables, OSHostTimeout timeout, uint flags, uint reserved)
    {
        Waitables = waitables;
        Timeout = timeout;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostWaitResult
{
    public readonly uint Kind;

    public readonly uint SignaledIndex;

    public readonly int StatusCode;

    public readonly uint Flags;

    public readonly OSHostHandle SignaledHandle;

    public OSHostWaitResult(uint kind, uint signaledIndex, int statusCode, uint flags, OSHostHandle signaledHandle)
    {
        Kind = kind;
        SignaledIndex = signaledIndex;
        StatusCode = statusCode;
        Flags = flags;
        SignaledHandle = signaledHandle;
    }
}
