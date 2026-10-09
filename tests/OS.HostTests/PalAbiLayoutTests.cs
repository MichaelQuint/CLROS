using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Pal;

internal static class PalAbiLayoutTests
{
    private static readonly Type[] AbiStructTypes =
    [
        typeof(OSHostHandle),
        typeof(OSHostByteSpan),
        typeof(OSHostMemoryRequest),
        typeof(OSHostMemoryRange),
        typeof(OSHostMemoryQueryResult),
        typeof(OSHostTimeSnapshot),
        typeof(OSHostLogRecord),
        typeof(OSHostWriteLogBytesRequest),
        typeof(OSHostRandomBytesRequest),
        typeof(OSHostFileInfo),
        typeof(OSHostBlockReadRequest),
        typeof(OSHostPackagePath),
        typeof(OSHostConfigQuery),
        typeof(OSHostEnvironmentEnumerationQuery),
        typeof(OSHostSocketAddress),
        typeof(OSHostSocketCreateOptions),
        typeof(OSHostSocketIoRequest),
        typeof(OSHostSocketPollEntry),
        typeof(OSHostTimeout),
        typeof(OSHostTimerOptions),
        typeof(OSHostThreadStart),
        typeof(OSHostThreadCreateOptions),
        typeof(OSHostWaitOneRequest),
        typeof(OSHostWaitManyRequest),
        typeof(OSHostWaitResult)
    ];

    [HostTest("pal abi status code values are stable", HostLane.Fast, 0)]
    private static bool TestStatusCodeValues()
    {
        return HostCheck.Result((int)OSHostStatus.Ok == 0
            && (int)OSHostStatus.InvalidArgument == 1
            && (int)OSHostStatus.NotSupported == 2
            && (int)OSHostStatus.NotFound == 3
            && (int)OSHostStatus.AccessDenied == 4
            && (int)OSHostStatus.BufferTooSmall == 5
            && (int)OSHostStatus.OutOfMemory == 6
            && (int)OSHostStatus.WouldBlock == 7
            && (int)OSHostStatus.TimedOut == 8
            && (int)OSHostStatus.Interrupted == 9
            && (int)OSHostStatus.EndOfFile == 10
            && (int)OSHostStatus.ConnectionReset == 11
            && (int)OSHostStatus.ConnectionRefused == 12
            && (int)OSHostStatus.AddressInUse == 13
            && (int)OSHostStatus.AddressNotAvailable == 14
            && (int)OSHostStatus.NetworkDown == 15
            && (int)OSHostStatus.TryAgain == 16
            && (int)OSHostStatus.IoError == 17
            && (int)OSHostStatus.InternalError == 18
            && (int)OSHostStatus.Closed == 19);
    }

    [HostTest("pal abi structs use sequential layout", HostLane.Fast, 1)]
    private static bool TestSequentialLayout()
    {
        foreach (Type type in AbiStructTypes)
        {
            StructLayoutAttribute? layout = type.StructLayoutAttribute;
            if (layout is null || layout.Value != LayoutKind.Sequential)
            {
                Console.WriteLine("PAL ABI struct is not sequential: " + type.FullName);
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }

    [HostTest("pal abi structs contain no managed references", HostLane.Fast, 3)]
    private static bool TestNoManagedReferences()
    {
        return HostCheck.Result(AssertUnmanaged<OSHostHandle>()
            && AssertUnmanaged<OSHostByteSpan>()
            && AssertUnmanaged<OSHostMemoryRequest>()
            && AssertUnmanaged<OSHostMemoryRange>()
            && AssertUnmanaged<OSHostMemoryQueryResult>()
            && AssertUnmanaged<OSHostTimeSnapshot>()
            && AssertUnmanaged<OSHostLogRecord>()
            && AssertUnmanaged<OSHostWriteLogBytesRequest>()
            && AssertUnmanaged<OSHostRandomBytesRequest>()
            && AssertUnmanaged<OSHostFileInfo>()
            && AssertUnmanaged<OSHostBlockReadRequest>()
            && AssertUnmanaged<OSHostPackagePath>()
            && AssertUnmanaged<OSHostConfigQuery>()
            && AssertUnmanaged<OSHostEnvironmentEnumerationQuery>()
            && AssertUnmanaged<OSHostSocketAddress>()
            && AssertUnmanaged<OSHostSocketCreateOptions>()
            && AssertUnmanaged<OSHostSocketIoRequest>()
            && AssertUnmanaged<OSHostSocketPollEntry>()
            && AssertUnmanaged<OSHostTimeout>()
            && AssertUnmanaged<OSHostTimerOptions>()
            && AssertUnmanaged<OSHostThreadStart>()
            && AssertUnmanaged<OSHostThreadCreateOptions>()
            && AssertUnmanaged<OSHostWaitOneRequest>()
            && AssertUnmanaged<OSHostWaitManyRequest>()
            && AssertUnmanaged<OSHostWaitResult>());
    }

    [HostTest("pal abi struct sizes are stable", HostLane.Fast, 4)]
    private static bool TestStableSizes()
    {
        return HostCheck.Result(Marshal.SizeOf<OSHostHandle>() == 8
            && Marshal.SizeOf<OSHostByteSpan>() == 16
            && Marshal.SizeOf<OSHostMemoryRequest>() == 24
            && Marshal.SizeOf<OSHostMemoryRange>() == 24
            && Marshal.SizeOf<OSHostMemoryQueryResult>() == 40
            && Marshal.SizeOf<OSHostTimeSnapshot>() == 24
            && Marshal.SizeOf<OSHostLogRecord>() == 40
            && Marshal.SizeOf<OSHostWriteLogBytesRequest>() == 32
            && Marshal.SizeOf<OSHostRandomBytesRequest>() == 24
            && Marshal.SizeOf<OSHostFileInfo>() == 24
            && Marshal.SizeOf<OSHostBlockReadRequest>() == 40
            && Marshal.SizeOf<OSHostPackagePath>() == 24
            && Marshal.SizeOf<OSHostConfigQuery>() == 32
            && Marshal.SizeOf<OSHostEnvironmentEnumerationQuery>() == 40
            && Marshal.SizeOf<OSHostSocketAddress>() == 32
            && Marshal.SizeOf<OSHostSocketCreateOptions>() == 24
            && Marshal.SizeOf<OSHostSocketIoRequest>() == 32
            && Marshal.SizeOf<OSHostSocketPollEntry>() == 24
            && Marshal.SizeOf<OSHostTimeout>() == 24
            && Marshal.SizeOf<OSHostTimerOptions>() == 32
            && Marshal.SizeOf<OSHostThreadStart>() == 16
            && Marshal.SizeOf<OSHostThreadCreateOptions>() == 56
            && Marshal.SizeOf<OSHostWaitOneRequest>() == 40
            && Marshal.SizeOf<OSHostWaitManyRequest>() == 48
            && Marshal.SizeOf<OSHostWaitResult>() == 24);
    }

    [HostTest("pal abi field offsets are stable", HostLane.Fast, 5)]
    private static bool TestStableOffsets()
    {
        return HostCheck.Result(Offset<OSHostByteSpan>("Address") == 0
            && Offset<OSHostByteSpan>("Length") == 8
            && Offset<OSHostMemoryQueryResult>("Range") == 0
            && Offset<OSHostMemoryQueryResult>("PageSizeBytes") == 24
            && Offset<OSHostMemoryQueryResult>("StateFlags") == 32
            && Offset<OSHostLogRecord>("Level") == 0
            && Offset<OSHostLogRecord>("Flags") == 4
            && Offset<OSHostLogRecord>("Subsystem") == 8
            && Offset<OSHostLogRecord>("Message") == 24
            && Offset<OSHostWriteLogBytesRequest>("StreamId") == 0
            && Offset<OSHostWriteLogBytesRequest>("Flags") == 4
            && Offset<OSHostWriteLogBytesRequest>("Buffer") == 8
            && Offset<OSHostWriteLogBytesRequest>("Reserved") == 24
            && Offset<OSHostRandomBytesRequest>("Destination") == 0
            && Offset<OSHostRandomBytesRequest>("Flags") == 16
            && Offset<OSHostRandomBytesRequest>("Reserved") == 20
            && Offset<OSHostBlockReadRequest>("Device") == 0
            && Offset<OSHostBlockReadRequest>("FirstBlock") == 8
            && Offset<OSHostBlockReadRequest>("BlockCount") == 16
            && Offset<OSHostBlockReadRequest>("BlockSizeBytes") == 20
            && Offset<OSHostBlockReadRequest>("Destination") == 24
            && Offset<OSHostEnvironmentEnumerationQuery>("Index") == 0
            && Offset<OSHostEnvironmentEnumerationQuery>("KeyDestination") == 8
            && Offset<OSHostEnvironmentEnumerationQuery>("ValueDestination") == 24
            && Offset<OSHostSocketAddress>("Family") == 0
            && Offset<OSHostSocketAddress>("PortNetworkOrder") == 2
            && Offset<OSHostSocketAddress>("FlowInfo") == 4
            && Offset<OSHostSocketAddress>("ScopeId") == 8
            && Offset<OSHostSocketAddress>("AddressHigh") == 16
            && Offset<OSHostSocketAddress>("AddressLow") == 24
            && Offset<OSHostSocketIoRequest>("Socket") == 0
            && Offset<OSHostSocketIoRequest>("Buffer") == 8
            && Offset<OSHostSocketIoRequest>("Flags") == 24
            && Offset<OSHostSocketPollEntry>("Socket") == 0
            && Offset<OSHostSocketPollEntry>("RequestedEvents") == 8
            && Offset<OSHostSocketPollEntry>("ObservedEvents") == 12
            && Offset<OSHostSocketPollEntry>("StatusCode") == 16
            && Offset<OSHostTimeout>("Kind") == 0
            && Offset<OSHostTimeout>("Ticks") == 8
            && Offset<OSHostTimeout>("TickFrequency") == 16
            && Offset<OSHostTimerOptions>("DueTicks") == 8
            && Offset<OSHostTimerOptions>("CompletionWaitHandle") == 24
            && Offset<OSHostThreadCreateOptions>("StackSizeBytes") == 8
            && Offset<OSHostThreadCreateOptions>("NameUtf8") == 24
            && Offset<OSHostThreadCreateOptions>("Start") == 40
            && Offset<OSHostWaitOneRequest>("Waitable") == 0
            && Offset<OSHostWaitOneRequest>("Timeout") == 8
            && Offset<OSHostWaitManyRequest>("Waitables") == 0
            && Offset<OSHostWaitManyRequest>("Timeout") == 16
            && Offset<OSHostWaitResult>("StatusCode") == 8
            && Offset<OSHostWaitResult>("SignaledHandle") == 16);
    }

    [HostTest("pal abi fields are public readonly", HostLane.Fast, 2)]
    private static bool TestFieldsArePublicReadonly()
    {
        foreach (Type type in AbiStructTypes)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.IsPublic || !field.IsInitOnly)
                {
                    Console.WriteLine("PAL ABI field is not public readonly: " + type.FullName + "." + field.Name);
                    return HostCheck.Result(false);
                }
            }
        }

        return HostCheck.Result(true);
    }

    private static bool AssertUnmanaged<T>()
        where T : struct
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Console.WriteLine("PAL ABI struct contains managed references: " + typeof(T).FullName);
            return false;
        }

        if (ContainsReferenceField(typeof(T), new HashSet<Type>()))
        {
            Console.WriteLine("PAL ABI struct has reference field: " + typeof(T).FullName);
            return false;
        }

        return true;
    }

    private static bool ContainsReferenceField(Type type, HashSet<Type> seen)
    {
        if (!type.IsValueType)
        {
            return true;
        }

        if (type.IsPrimitive || type.IsEnum || !seen.Add(type))
        {
            return false;
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (ContainsReferenceField(field.FieldType, seen))
            {
                Console.WriteLine("Reference field: " + type.FullName + "." + field.Name + " type=" + field.FieldType.FullName);
                return true;
            }
        }

        return false;
    }

    private static int Offset<T>(string fieldName)
    {
        return (int)Marshal.OffsetOf<T>(fieldName);
    }
}
