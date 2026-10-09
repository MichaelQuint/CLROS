using System.Runtime.CompilerServices;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Std.Binary;
using OS.Storage;

internal static class NativeAppPayloadMappingPlanTests
{
    private const ulong PayloadOffsetInPackage = 128;
    private const ulong PayloadLength = 48;

    [HostTest("appmodel payload mapping plans executable range", HostLane.Fast, 0)]
    private static bool TestPlansExecutableRange()
    {
        NativeAppLaunchDescriptorResult descriptor = CreateDescriptorResult(
            PayloadOffsetInPackage,
            PayloadLength,
            NativeAppPayloadHeader.HeaderSize + 4);

        NativeAppPayloadMappingPlanResult result =
            NativeAppPayloadMappingPlanner.Create(descriptor);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadMappingPlanStatus.Success
            && result.Plan.PayloadOffsetInPackage == PayloadOffsetInPackage
            && result.Plan.PayloadLength == PayloadLength
            && result.Plan.HeaderLength == NativeAppPayloadHeader.HeaderSize
            && result.Plan.ExecutableOffsetInPayload == NativeAppPayloadHeader.HeaderSize
            && result.Plan.ExecutableOffsetInPackage == PayloadOffsetInPackage + NativeAppPayloadHeader.HeaderSize
            && result.Plan.ExecutableLength == PayloadLength - NativeAppPayloadHeader.HeaderSize
            && result.Plan.EntryOffsetInPayload == NativeAppPayloadHeader.HeaderSize + 4
            && result.Plan.EntryOffsetInExecutable == 4
            && result.Plan.EntryOffsetInPackage == PayloadOffsetInPackage + NativeAppPayloadHeader.HeaderSize + 4);
    }

    [HostTest("appmodel payload mapping rejects descriptor failure", HostLane.Fast, 1)]
    private static bool TestRejectsDescriptorFailure()
    {
        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorResult.Fail(
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
            NativeAppLaunchStatus.Success,
            OsPackageFileSystemStatus.Success,
            OsPackageStatus.Success,
            NativeAppPayloadHeaderStatus.InvalidMagic);

        NativeAppPayloadMappingPlanResult result =
            NativeAppPayloadMappingPlanner.Create(descriptor);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadMappingPlanStatus.DescriptorFailed
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.InvalidPayloadHeader
            && result.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.InvalidMagic);
    }

    [HostTest("appmodel payload mapping rejects zero executable bytes", HostLane.Fast, 2)]
    private static bool TestRejectsZeroExecutableBytes()
    {
        NativeAppPayloadHeader header = ParsePayloadHeader(40, NativeAppPayloadHeader.HeaderSize);
        NativeAppLaunchDescriptorResult descriptor = CreateDescriptorResult(
            PayloadOffsetInPackage,
            NativeAppPayloadHeader.HeaderSize,
            header);

        NativeAppPayloadMappingPlanResult result =
            NativeAppPayloadMappingPlanner.Create(descriptor);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadMappingPlanStatus.ZeroExecutableContent);
    }

    [HostTest("appmodel payload mapping rejects invalid entry range", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidEntryRange()
    {
        NativeAppPayloadHeader header = ParsePayloadHeader((int)PayloadLength, NativeAppPayloadHeader.HeaderSize + 8);
        NativeAppLaunchDescriptorResult descriptor = CreateDescriptorResult(
            PayloadOffsetInPackage,
            NativeAppPayloadHeader.HeaderSize + 4,
            header);

        NativeAppPayloadMappingPlanResult result =
            NativeAppPayloadMappingPlanner.Create(descriptor);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppPayloadMappingPlanStatus.InvalidEntryOffset);
    }

    [HostTest("appmodel payload mapping default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppPayloadMappingPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppPayloadMappingPlanStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PayloadHeaderStatus == NativeAppPayloadHeaderStatus.Success
            && result.Plan.PayloadOffsetInPackage == 0
            && result.Plan.PayloadLength == 0
            && result.Plan.HeaderLength == 0
            && result.Plan.ExecutableOffsetInPayload == 0
            && result.Plan.ExecutableOffsetInPackage == 0
            && result.Plan.ExecutableLength == 0
            && result.Plan.EntryOffsetInPayload == 0
            && result.Plan.EntryOffsetInExecutable == 0
            && result.Plan.EntryOffsetInPackage == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPayloadMappingPlanResult>());
    }

    private static NativeAppLaunchDescriptorResult CreateDescriptorResult(
        ulong payloadOffsetInPackage,
        ulong payloadLength,
        ulong entryOffsetInPayload)
    {
        NativeAppPayloadHeader header = ParsePayloadHeader((int)payloadLength, entryOffsetInPayload);
        return CreateDescriptorResult(payloadOffsetInPackage, payloadLength, header);
    }

    private static NativeAppLaunchDescriptorResult CreateDescriptorResult(
        ulong payloadOffsetInPackage,
        ulong payloadLength,
        NativeAppPayloadHeader header)
    {
        NativeAppLaunchDescriptor descriptor = new NativeAppLaunchDescriptor(
            default,
            new OsPackageFileEntry(
                index: 0,
                pathOffset: 0,
                pathLength: 12,
                contentOffset: payloadOffsetInPackage,
                contentLength: payloadLength),
            payloadLength,
            NativeManifestNetworkMode.Dhcp,
            NativeManifestLogMode.Serial,
            argumentByteLength: 0,
            header);
        return NativeAppLaunchDescriptorResult.Success(descriptor);
    }

    private static NativeAppPayloadHeader ParsePayloadHeader(int payloadLength, ulong entryOffset)
    {
        byte[] payload = new byte[payloadLength];
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

        NativeAppPayloadHeaderResult result = NativeAppPayloadHeader.Parse(payload);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Test payload header did not parse: " + result.Status);
        }

        return result.Header;
    }
}
