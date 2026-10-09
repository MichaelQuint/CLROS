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

    [HostTest("appmodel payload staging copies executable bytes", HostLane.Fast, 0)]
    private static bool TestCopiesExecutableBytes()
    {
        byte[] package = CreatePackage();
        byte[] destination = FilledDestination(ExecutableLength + 3);

        NativeAppPayloadStagingResult result =
            NativeAppPayloadStagingPlanner.CopyExecutableToStaging(
                package,
                CreateMappingPlan(),
                destination);

        return HostCheck.Result(result.Succeeded
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
            && destination[(int)ExecutableLength + 2] == 0xEE);
    }

    [HostTest("appmodel payload staging rejects short destination", HostLane.Fast, 1)]
    private static bool TestRejectsShortDestination()
    {
        byte[] package = CreatePackage();
        byte[] destination = FilledDestination(ExecutableLength - 1);

        NativeAppPayloadStagingResult result =
            NativeAppPayloadStagingPlanner.CopyExecutableToStaging(
                package,
                CreateMappingPlan(),
                destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadStagingStatus.DestinationTooSmall
            && result.MappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.RequiredBytes == ExecutableLength
            && AllBytesEqual(destination, 0xEE));
    }

    [HostTest("appmodel payload staging rejects package range", HostLane.Fast, 2)]
    private static bool TestRejectsPackageRange()
    {
        byte[] package = new byte[(int)ExecutableOffsetInPackage + 2];
        byte[] destination = FilledDestination(ExecutableLength);

        NativeAppPayloadStagingResult result =
            NativeAppPayloadStagingPlanner.CopyExecutableToStaging(
                package,
                CreateMappingPlan(),
                destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadStagingStatus.PackageRangeOutOfBounds
            && result.RequiredBytes == ExecutableLength
            && result.ExecutableOffsetInPackage == ExecutableOffsetInPackage
            && AllBytesEqual(destination, 0xEE));
    }

    [HostTest("appmodel payload staging rejects mapping failure", HostLane.Fast, 3)]
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

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadStagingStatus.MappingPlanFailed
            && result.MappingStatus == NativeAppPayloadMappingPlanStatus.DescriptorFailed
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.InvalidPayloadHeader
            && result.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.InvalidMagic
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && AllBytesEqual(destination, 0xEE));
    }

    [HostTest("appmodel payload staging preserves no-execute boundary", HostLane.Fast, 4)]
    private static bool TestPreservesNoExecuteBoundary()
    {
        return HostCheck.Result(NativeAppPayloadStagingPlanner.CopiesExecutableBytes
            && !NativeAppPayloadStagingPlanner.ReadsLiveStorage
            && !NativeAppPayloadStagingPlanner.MapsExecutableMemory
            && !NativeAppPayloadStagingPlanner.ExecutesApp);
    }

    [HostTest("appmodel payload staging default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppPayloadStagingResult result = default;

        return HostCheck.Result(!result.IsInitialized
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
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPayloadStagingResult>());
    }

    [HostTest("appmodel payload staging uses shared buffer operations", HostLane.Fast, 5)]
    private static bool TestUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Runtime",
            "NativeAppPayloadStagingPlan.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(source, destination)", StringComparison.Ordinal)
            && source.Contains("if (!copy.Succeeded)", StringComparison.Ordinal)
            && source.Contains("NativeAppPayloadStagingStatus.DestinationTooSmall", StringComparison.Ordinal)
            && !source.Contains(".CopyTo(destination)", StringComparison.Ordinal));
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
