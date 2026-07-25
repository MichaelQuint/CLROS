using System.Runtime.CompilerServices;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static class NativeAppPayloadStagingPlanTests
{
    private const ulong PayloadOffsetInPackage = 16;
    private const ulong HeaderLength = 32;
    private const ulong ExecutableLength = 8;
    private const ulong ExecutableOffsetInPackage = PayloadOffsetInPackage + HeaderLength;
    private const ulong PayloadLength = HeaderLength + ExecutableLength;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel payload staging copies executable bytes", TestCopiesExecutableBytes, ref failures);
        Run("appmodel payload staging rejects short destination", TestRejectsShortDestination, ref failures);
        Run("appmodel payload staging rejects package range", TestRejectsPackageRange, ref failures);
        Run("appmodel payload staging rejects mapping failure", TestRejectsMappingFailure, ref failures);
        Run("appmodel payload staging preserves no-execute boundary", TestPreservesNoExecuteBoundary, ref failures);
        Run("appmodel payload staging uses shared buffer operations", TestUsesSharedBufferOperations, ref failures);
        Run("appmodel payload staging default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app payload staging host tests failed: " + failures);
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

    private static bool TestCopiesExecutableBytes()
    {
        byte[] package = CreatePackage();
        byte[] destination = FilledDestination(ExecutableLength + 3);

        NativeAppPayloadStagingResult result =
            NativeAppPayloadStagingPlanner.CopyExecutableToStaging(
                package,
                CreateMappingPlan(),
                destination);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadStagingStatus.Success
            && result.MappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success
            && result.PayloadOffsetInPackage == PayloadOffsetInPackage
            && result.ExecutableOffsetInPackage == ExecutableOffsetInPackage
            && result.ExecutableBytes == ExecutableLength
            && result.BytesWritten == ExecutableLength
            && result.RequiredBytes == ExecutableLength
            && SpanEquals(destination.AsSpan(0, (int)ExecutableLength), package.AsSpan((int)ExecutableOffsetInPackage, (int)ExecutableLength))
            && destination[(int)ExecutableLength] == 0xEE
            && destination[(int)ExecutableLength + 1] == 0xEE
            && destination[(int)ExecutableLength + 2] == 0xEE;
    }

    private static bool TestRejectsShortDestination()
    {
        byte[] package = CreatePackage();
        byte[] destination = FilledDestination(ExecutableLength - 1);

        NativeAppPayloadStagingResult result =
            NativeAppPayloadStagingPlanner.CopyExecutableToStaging(
                package,
                CreateMappingPlan(),
                destination);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadStagingStatus.DestinationTooSmall
            && result.MappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.RequiredBytes == ExecutableLength
            && AllBytesEqual(destination, 0xEE);
    }

    private static bool TestRejectsPackageRange()
    {
        byte[] package = new byte[(int)ExecutableOffsetInPackage + 2];
        byte[] destination = FilledDestination(ExecutableLength);

        NativeAppPayloadStagingResult result =
            NativeAppPayloadStagingPlanner.CopyExecutableToStaging(
                package,
                CreateMappingPlan(),
                destination);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadStagingStatus.PackageRangeOutOfBounds
            && result.RequiredBytes == ExecutableLength
            && result.ExecutableOffsetInPackage == ExecutableOffsetInPackage
            && AllBytesEqual(destination, 0xEE);
    }

    private static bool TestRejectsMappingFailure()
    {
        byte[] package = CreatePackage();
        byte[] destination = FilledDestination(ExecutableLength);
        NativeAppPayloadMappingPlanResult mapping = NativeAppPayloadMappingPlanResult.Fail(
            NativeAppPayloadMappingPlanStatus.DescriptorFailed,
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
            NativeAppPayloadHeaderStatus.InvalidMagic);

        NativeAppPayloadStagingResult result =
            NativeAppPayloadStagingPlanner.CopyExecutableToStaging(package, mapping, destination);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadStagingStatus.MappingPlanFailed
            && result.MappingStatus == NativeAppPayloadMappingPlanStatus.DescriptorFailed
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.InvalidPayloadHeader
            && result.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.InvalidMagic
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && AllBytesEqual(destination, 0xEE);
    }

    private static bool TestPreservesNoExecuteBoundary()
    {
        return NativeAppPayloadStagingPlanner.CopiesExecutableBytes
            && !NativeAppPayloadStagingPlanner.ReadsLiveStorage
            && !NativeAppPayloadStagingPlanner.MapsExecutableMemory
            && !NativeAppPayloadStagingPlanner.ExecutesApp;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppPayloadStagingResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPayloadStagingStatus.Success
            && result.MappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success
            && result.PayloadOffsetInPackage == 0
            && result.ExecutableOffsetInPackage == 0
            && result.ExecutableBytes == 0
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPayloadStagingResult>();
    }

    private static bool TestUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Runtime",
            "NativeAppPayloadStagingPlan.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(source, destination)", StringComparison.Ordinal)
            && source.Contains("if (!copy.Succeeded)", StringComparison.Ordinal)
            && source.Contains("NativeAppPayloadStagingStatus.DestinationTooSmall", StringComparison.Ordinal)
            && !source.Contains(".CopyTo(destination)", StringComparison.Ordinal);
    }

    private static NativeAppPayloadMappingPlanResult CreateMappingPlan()
    {
        NativeAppPayloadMappingPlan plan = new(
            PayloadOffsetInPackage,
            PayloadLength,
            HeaderLength,
            HeaderLength,
            ExecutableOffsetInPackage,
            ExecutableLength,
            HeaderLength,
            0,
            ExecutableOffsetInPackage);

        return NativeAppPayloadMappingPlanResult.Success(plan);
    }

    private static byte[] CreatePackage()
    {
        byte[] package = new byte[(int)(ExecutableOffsetInPackage + ExecutableLength + 4)];

        for (int index = 0; index < (int)HeaderLength; index++)
        {
            package[(int)PayloadOffsetInPackage + index] = 0xA5;
        }

        for (int index = 0; index < (int)ExecutableLength; index++)
        {
            package[(int)ExecutableOffsetInPackage + index] = (byte)(0x41 + index);
        }

        return package;
    }

    private static byte[] FilledDestination(ulong length)
    {
        byte[] destination = new byte[(int)length];
        Array.Fill(destination, (byte)0xEE);
        return destination;
    }

    private static bool SpanEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllBytesEqual(byte[] bytes, byte expected)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected)
            {
                return false;
            }
        }

        return true;
    }
}
