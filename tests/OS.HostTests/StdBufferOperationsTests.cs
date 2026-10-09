using System.Runtime.CompilerServices;
using OS.Std.Memory;

internal static class StdBufferOperationsTests
{

    [HostTest("std.buffer memorycopy exact bytes", HostLane.Fast, 0)]
    private static bool TestMemoryCopyExactBytes()
    {
        byte[] source = [1, 2, 3, 4];
        byte[] destination = [0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc];

        BufferOperationResult result = BufferOperations.MemoryCopy(source, destination);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == BufferOperationStatus.Success
            && result.SourceLength == source.Length
            && result.DestinationLength == destination.Length
            && result.RequiredBytes == source.Length
            && result.BytesCopied == source.Length
            && destination[0] == 1
            && destination[1] == 2
            && destination[2] == 3
            && destination[3] == 4
            && destination[4] == 0xcc
            && destination[5] == 0xcc);
    }

    [HostTest("std.buffer memorycopy overlap forward", HostLane.Fast, 1)]
    private static bool TestMemoryCopyOverlapForward()
    {
        byte[] buffer = [(byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9'];

        BufferOperationResult result = BufferOperations.MemoryCopy(
            buffer.AsSpan(0, 6),
            buffer.AsSpan(2, 6));

        return HostCheck.Result(result.Succeeded
            && AsciiEquals(buffer, "0101234589"));
    }

    [HostTest("std.buffer memorycopy overlap backward", HostLane.Fast, 2)]
    private static bool TestMemoryCopyOverlapBackward()
    {
        byte[] buffer = [(byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9'];

        BufferOperationResult result = BufferOperations.MemoryCopy(
            buffer.AsSpan(2, 6),
            buffer.AsSpan(0, 6));

        return HostCheck.Result(result.Succeeded
            && AsciiEquals(buffer, "2345676789"));
    }

    [HostTest("std.buffer memorycopy rejects short destination", HostLane.Fast, 3)]
    private static bool TestMemoryCopyRejectsShortDestination()
    {
        byte[] source = [1, 2, 3, 4];
        byte[] destination = [0xcc, 0xcc, 0xcc];

        BufferOperationResult result = BufferOperations.MemoryCopy(source, destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == BufferOperationStatus.DestinationTooSmall
            && result.SourceLength == source.Length
            && result.DestinationLength == destination.Length
            && result.RequiredBytes == source.Length
            && result.BytesCopied == 0
            && destination[0] == 0xcc
            && destination[1] == 0xcc
            && destination[2] == 0xcc);
    }

    [HostTest("std.buffer memorycopy zero length and layout", HostLane.Fast, 4)]
    private static bool TestMemoryCopyZeroLengthAndLayout()
    {
        byte[] source = [];
        byte[] destination = [0x10, 0x20];

        BufferOperationResult result = BufferOperations.MemoryCopy(source, destination);
        BufferOperationResult defaultResult = default;

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == BufferOperationStatus.Success
            && result.SourceLength == 0
            && result.DestinationLength == destination.Length
            && result.RequiredBytes == 0
            && result.BytesCopied == 0
            && destination[0] == 0x10
            && destination[1] == 0x20
            && !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == BufferOperationStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<BufferOperationResult>());
    }

    private static bool AsciiEquals(byte[] bytes, string expected)
    {
        if (bytes.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }
}
