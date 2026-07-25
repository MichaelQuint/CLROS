using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VirtioPciCapabilityListParserTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const uint CommonRegionOffset = 0x1000;
    private const uint NotifyRegionOffset = 0x2000;
    private const uint IsrRegionOffset = 0x3000;
    private const uint DeviceRegionOffset = 0x4000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "drivers.virtiopci.capability parser records required capabilities",
            TestRecordsRequiredCapabilities,
            ref failures);
        Run(
            "drivers.virtiopci.capability parser rejects truncated capability",
            TestRejectsTruncatedCapability,
            ref failures);
        Run(
            "drivers.virtiopci.capability parser rejects capability loop",
            TestRejectsCapabilityLoop,
            ref failures);
        Run(
            "drivers.virtiopci.capability parser feeds discovery contract",
            TestFeedsDiscoveryContract,
            ref failures);
        Run(
            "drivers.virtiopci.capability parser default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio PCI capability parser host tests failed: " + failures);
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

    private static bool TestRecordsRequiredCapabilities()
    {
        byte[] config = CreateValidConfig();
        VirtioPciCapabilityListParseResult result = VirtioPciCapabilityListParser.Parse(
            config,
            CommonOffset);
        if (!result.Succeeded ||
            result.Status != VirtioPciCapabilityListParseStatus.Success ||
            !result.IsInitialized ||
            result.ParsedVirtioCapabilityCount != VirtioPciCapabilityListParser.RequiredCapabilityCount)
        {
            return false;
        }

        VirtioPciCapabilitySet capabilities = result.Capabilities;
        if (!capabilities.HasCommonConfiguration ||
            !capabilities.HasNotifyConfiguration ||
            !capabilities.HasIsrStatus ||
            !capabilities.HasDeviceConfiguration ||
            capabilities.CommonConfiguration.Type != VirtioPciCapabilityType.CommonConfiguration ||
            capabilities.CommonConfiguration.Bar != 0 ||
            capabilities.CommonConfiguration.Offset != CommonRegionOffset ||
            capabilities.CommonConfiguration.Length != VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength ||
            capabilities.NotifyConfiguration.Type != VirtioPciCapabilityType.NotifyConfiguration ||
            capabilities.NotifyConfiguration.Bar != 1 ||
            capabilities.NotifyConfiguration.Offset != NotifyRegionOffset ||
            capabilities.NotifyConfiguration.Length != 0x100 ||
            capabilities.NotifyConfiguration.NotifyOffsetMultiplier != 4 ||
            capabilities.IsrStatus.Type != VirtioPciCapabilityType.IsrStatus ||
            capabilities.IsrStatus.Bar != 2 ||
            capabilities.IsrStatus.Offset != IsrRegionOffset ||
            capabilities.IsrStatus.Length != VirtioPciDeviceDiscovery.IsrStatusMinimumLength ||
            capabilities.DeviceConfiguration.Type != VirtioPciCapabilityType.DeviceConfiguration ||
            capabilities.DeviceConfiguration.Bar != 3 ||
            capabilities.DeviceConfiguration.Offset != DeviceRegionOffset ||
            capabilities.DeviceConfiguration.Length != VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength)
        {
            return false;
        }

        Span<VirtioPciCapabilityMetadata> copiedCapabilities =
            stackalloc VirtioPciCapabilityMetadata[VirtioPciCapabilityListParser.RequiredCapabilityCount];
        if (!result.TryCopyRequiredCapabilities(copiedCapabilities))
        {
            return false;
        }

        VirtioPciCapabilityListParseResult missingDeviceConfiguration = VirtioPciCapabilityListParser.Parse(
            CreateConfigWithoutDeviceConfiguration(),
            CommonOffset);

        return copiedCapabilities[0].Type == VirtioPciCapabilityType.CommonConfiguration
            && copiedCapabilities[1].Type == VirtioPciCapabilityType.NotifyConfiguration
            && copiedCapabilities[2].Type == VirtioPciCapabilityType.IsrStatus
            && copiedCapabilities[3].Type == VirtioPciCapabilityType.DeviceConfiguration
            && !missingDeviceConfiguration.Succeeded
            && missingDeviceConfiguration.Status
                == VirtioPciCapabilityListParseStatus.MissingDeviceConfigurationCapability;
    }

    private static bool TestRejectsTruncatedCapability()
    {
        byte[] truncatedConfig = CreateValidConfig();
        truncatedConfig[CommonOffset + 2] =
            VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength;

        VirtioPciCapabilityListParseResult truncatedResult = VirtioPciCapabilityListParser.Parse(
            truncatedConfig.AsSpan(0, 0x50),
            CommonOffset);
        if (truncatedResult.Status != VirtioPciCapabilityListParseStatus.VendorCapabilityRecordOutOfRange ||
            truncatedResult.FailingOffset != CommonOffset ||
            truncatedResult.FailingCapabilityId != VirtioPciCapabilityListParser.VendorSpecificCapabilityId)
        {
            return false;
        }

        byte[] unsupportedConfig = CreateValidConfig();
        unsupportedConfig[CommonOffset + 3] = 0x7F;
        VirtioPciCapabilityListParseResult unsupportedResult = VirtioPciCapabilityListParser.Parse(
            unsupportedConfig,
            CommonOffset);
        if (unsupportedResult.Status != VirtioPciCapabilityListParseStatus.UnsupportedVendorCapability)
        {
            return false;
        }

        byte[] shortNotifyConfig = CreateValidConfig();
        shortNotifyConfig[NotifyOffset + 2] = VirtioPciCapabilityListParser.ModernVirtioCapabilityLength;
        VirtioPciCapabilityListParseResult shortNotifyResult = VirtioPciCapabilityListParser.Parse(
            shortNotifyConfig,
            CommonOffset);

        return shortNotifyResult.Status == VirtioPciCapabilityListParseStatus.NotifyCapabilityTooShort;
    }

    private static bool TestRejectsCapabilityLoop()
    {
        byte[] config = CreateValidConfig();
        config[DeviceOffset + 1] = CommonOffset;

        VirtioPciCapabilityListParseResult result = VirtioPciCapabilityListParser.Parse(
            config,
            CommonOffset);
        if (result.Status != VirtioPciCapabilityListParseStatus.CapabilityLoopDetected ||
            result.FailingOffset != CommonOffset)
        {
            return false;
        }

        byte[] unalignedConfig = CreateValidConfig();
        unalignedConfig[CommonOffset + 1] = 0x42;
        VirtioPciCapabilityListParseResult unalignedResult = VirtioPciCapabilityListParser.Parse(
            unalignedConfig,
            CommonOffset);

        return unalignedResult.Status == VirtioPciCapabilityListParseStatus.CapabilityOffsetUnaligned
            && unalignedResult.FailingOffset == 0x42;
    }

    private static bool TestFeedsDiscoveryContract()
    {
        VirtioPciCapabilityListParseResult parserResult = VirtioPciCapabilityListParser.Parse(
            CreateValidConfig(),
            CommonOffset);
        if (!parserResult.Succeeded)
        {
            return false;
        }

        Span<VirtioPciCapabilityMetadata> capabilities =
            stackalloc VirtioPciCapabilityMetadata[VirtioPciCapabilityListParser.RequiredCapabilityCount];
        if (!parserResult.TryCopyRequiredCapabilities(capabilities))
        {
            return false;
        }

        VirtioPciDeviceDiscoveryResult discoveryResult = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.BlockDeviceId),
            capabilities);

        return discoveryResult.Succeeded
            && discoveryResult.Status == VirtioPciDeviceDiscoveryStatus.Success
            && discoveryResult.DeviceKind == VirtioPciDeviceKind.Block
            && discoveryResult.Capabilities.NotifyConfiguration.NotifyOffsetMultiplier == 4;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciCapabilityListParseResult result = default;
        Span<VirtioPciCapabilityMetadata> destination =
            stackalloc VirtioPciCapabilityMetadata[VirtioPciCapabilityListParser.RequiredCapabilityCount];

        return !result.IsInitialized
            && result.Status == VirtioPciCapabilityListParseStatus.Success
            && !result.Succeeded
            && !result.TryCopyRequiredCapabilities(destination)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciCapabilityListParseResult>();
    }

    private static byte[] CreateValidConfig()
    {
        byte[] config = new byte[256];
        WriteVirtioCapability(
            config,
            CommonOffset,
            NotifyOffset,
            VirtioPciCapabilityType.CommonConfiguration,
            bar: 0,
            CommonRegionOffset,
            VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            NotifyOffset,
            IsrOffset,
            VirtioPciCapabilityType.NotifyConfiguration,
            bar: 1,
            NotifyRegionOffset,
            regionLength: 0x100,
            notifyOffsetMultiplier: 4,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(
            config,
            IsrOffset,
            DeviceOffset,
            VirtioPciCapabilityType.IsrStatus,
            bar: 2,
            IsrRegionOffset,
            VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            DeviceRegionOffset,
            VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static byte[] CreateConfigWithoutDeviceConfiguration()
    {
        byte[] config = CreateValidConfig();
        config[IsrOffset + 1] = 0;
        return config;
    }

    private static void WriteVirtioCapability(
        Span<byte> config,
        byte capabilityOffset,
        byte nextOffset,
        VirtioPciCapabilityType type,
        byte bar,
        uint regionOffset,
        uint regionLength,
        uint notifyOffsetMultiplier,
        byte capabilityLength)
    {
        config[capabilityOffset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[capabilityOffset + 1] = nextOffset;
        config[capabilityOffset + 2] = capabilityLength;
        config[capabilityOffset + 3] = (byte)type;
        config[capabilityOffset + 4] = bar;
        EndianPrimitives.WriteUInt32LittleEndian(
            config.Slice(capabilityOffset + 8, sizeof(uint)),
            regionOffset);
        EndianPrimitives.WriteUInt32LittleEndian(
            config.Slice(capabilityOffset + 12, sizeof(uint)),
            regionLength);

        if (capabilityLength >= VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength)
        {
            EndianPrimitives.WriteUInt32LittleEndian(
                config.Slice(capabilityOffset + 16, sizeof(uint)),
                notifyOffsetMultiplier);
        }
    }
}
