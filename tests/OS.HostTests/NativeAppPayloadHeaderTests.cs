using System.Runtime.CompilerServices;
using OS.Manifest;
using OS.Std.Binary;

internal static class NativeAppPayloadHeaderTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel payload header accepts v0 native payload", TestAcceptsV0NativePayload, ref failures);
        Run("appmodel payload header rejects bad magic", TestRejectsBadMagic, ref failures);
        Run("appmodel payload header rejects unsupported version", TestRejectsUnsupportedVersion, ref failures);
        Run("appmodel payload header rejects invalid entry offset", TestRejectsInvalidEntryOffset, ref failures);
        Run("appmodel payload header default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("appmodel payload header uses shared memory equality helper", TestUsesSharedMemoryEqualityHelper, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app payload header host tests failed: " + failures);
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

    private static bool TestAcceptsV0NativePayload()
    {
        byte[] payload = CreatePayload(NativeAppPayloadHeader.HeaderSize);
        NativeAppPayloadHeaderResult result = NativeAppPayloadHeader.Parse(payload);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadHeaderStatus.Success
            && result.Header.Version == NativeAppPayloadHeader.SupportedVersion
            && result.Header.HeaderSizeValue == NativeAppPayloadHeader.HeaderSize
            && result.Header.Flags == 0
            && result.Header.EntryOffset == NativeAppPayloadHeader.HeaderSize;
    }

    private static bool TestRejectsBadMagic()
    {
        byte[] payload = CreatePayload(NativeAppPayloadHeader.HeaderSize);
        payload[0] = (byte)'X';

        NativeAppPayloadHeaderResult result = NativeAppPayloadHeader.Parse(payload);
        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadHeaderStatus.InvalidMagic;
    }

    private static bool TestRejectsUnsupportedVersion()
    {
        byte[] payload = CreatePayload(NativeAppPayloadHeader.HeaderSize);
        EndianPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8, 2), 1);

        NativeAppPayloadHeaderResult result = NativeAppPayloadHeader.Parse(payload);
        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadHeaderStatus.UnsupportedVersion;
    }

    private static bool TestRejectsInvalidEntryOffset()
    {
        byte[] beforeHeader = CreatePayload(NativeAppPayloadHeader.HeaderSize - 1);
        byte[] endOfPayload = CreatePayload(40);

        NativeAppPayloadHeaderResult beforeHeaderResult = NativeAppPayloadHeader.Parse(beforeHeader);
        NativeAppPayloadHeaderResult endOfPayloadResult = NativeAppPayloadHeader.Parse(endOfPayload);

        return !beforeHeaderResult.Succeeded
            && beforeHeaderResult.IsInitialized
            && beforeHeaderResult.Status == NativeAppPayloadHeaderStatus.InvalidEntryOffset
            && !endOfPayloadResult.Succeeded
            && endOfPayloadResult.IsInitialized
            && endOfPayloadResult.Status == NativeAppPayloadHeaderStatus.InvalidEntryOffset;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppPayloadHeaderResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPayloadHeaderStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPayloadHeaderResult>();
    }

    private static bool TestUsesSharedMemoryEqualityHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Manifest", "NativeAppPayloadHeader.cs"));

        return source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(value, Magic)",
                StringComparison.Ordinal)
            && source.Contains(
                "private static ReadOnlySpan<byte> Magic => \"OSAPPv0\\0\"u8;",
                StringComparison.Ordinal)
            && !source.Contains(
                "value[0] == (byte)'O'",
                StringComparison.Ordinal)
            && !source.Contains(
                "value[1] == (byte)'S'",
                StringComparison.Ordinal)
            && !source.Contains(
                "value[2] == (byte)'A'",
                StringComparison.Ordinal)
            && !source.Contains(
                "value[3] == (byte)'P'",
                StringComparison.Ordinal)
            && !source.Contains(
                "value[4] == (byte)'P'",
                StringComparison.Ordinal)
            && !source.Contains(
                "value[5] == (byte)'v'",
                StringComparison.Ordinal)
            && !source.Contains(
                "value[6] == (byte)'0'",
                StringComparison.Ordinal)
            && !source.Contains(
                "value[7] == 0",
                StringComparison.Ordinal);
    }

    private static byte[] CreatePayload(ulong entryOffset)
    {
        byte[] payload = new byte[40];
        payload[0] = (byte)'O';
        payload[1] = (byte)'S';
        payload[2] = (byte)'A';
        payload[3] = (byte)'P';
        payload[4] = (byte)'P';
        payload[5] = (byte)'v';
        payload[6] = (byte)'0';
        payload[7] = 0;
        EndianPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8, 2), NativeAppPayloadHeader.SupportedVersion);
        EndianPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10, 2), NativeAppPayloadHeader.HeaderSize);
        EndianPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12, 4), 0);
        EndianPrimitives.WriteUInt64LittleEndian(payload.AsSpan(16, 8), entryOffset);
        EndianPrimitives.WriteUInt64LittleEndian(payload.AsSpan(24, 8), 0);
        payload[NativeAppPayloadHeader.HeaderSize] = 0xC3;
        return payload;
    }
}
