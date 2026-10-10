using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe class ResetACKNOWLEDGEDRIVERFEATURESOKNegotiationTests
{
    private struct Device
    {
        public ulong Offered, Selected;
        public uint DeviceSelector, DriverSelector, Calls, FailCall, ResetDelay, ResetReads;
        public byte Status, RejectStatus, RejectValue;
        public bool RejectFeatures, InvalidAccess, DriverOkWritten, ResetPending, NeedsResetAfterFeatures;
        public uint StatusWrites;
        public fixed byte History[8];
    }

    private const ulong Virtual = 0x400000;
    private static VirtioMappedRegion Common
    {
        get
        {
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            for (int i = 0; i < 6; i++) bars[i] = default;
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(
                0x0000000c, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var capabilities = VirtioPciCapabilitySet.Create(
                new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 4096, 0),
                new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 4096, 4),
                new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 4096, 0),
                new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 4096, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(capabilities, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Virtual, out var mapped));
            return mapped;
        }
    }
    private static Device Fresh() => new() { Offered = 0x8000000100000001UL };
    private static VirtioNegotiationResult Run(Device* device, uint resetReads = 4, bool block = false) =>
        block ? OS.Storage.MinimalVirtioBlkFeatureSet.Negotiate(Common, resetReads,
            device, &Read32, &Write32, &Read8, &Write8) :
            ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.Run(Common, BoundedVirtioFeatures.Version1, resetReads,
                device, &Read32, &Write32, &Read8, &Write8);
    private static bool Fail(Device* device) => ++device->Calls == device->FailCall;

    [UnmanagedCallersOnly]
    private static byte Read32(void* context, ulong address, uint* output)
    {
        Device* device = (Device*)context;
        if (Fail(device)) return 0;
        if (address != Virtual + 4 || device->DeviceSelector > 1 || device->Status != 3)
        { device->InvalidAccess = true; return 0; }
        *output = (uint)(device->Offered >> (int)(device->DeviceSelector * 32));
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Write32(void* context, ulong address, uint value)
    {
        Device* device = (Device*)context;
        if (Fail(device)) return 0;
        if (device->Status != 3) { device->InvalidAccess = true; return 0; }
        if (address == Virtual && value <= 1) device->DeviceSelector = value;
        else if (address == Virtual + 8 && value <= 1) device->DriverSelector = value;
        else if (address == Virtual + 12 && device->DriverSelector <= 1)
        {
            int shift = (int)(device->DriverSelector * 32);
            device->Selected = (device->Selected & ~(0xffffffffUL << shift)) | ((ulong)value << shift);
            if (device->DriverSelector == 1 && device->NeedsResetAfterFeatures) device->Status |= 0x40;
        }
        else { device->InvalidAccess = true; return 0; }
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Read8(void* context, ulong address, byte* output)
    {
        Device* device = (Device*)context;
        if (Fail(device)) return 0;
        if (address != Virtual + 20) { device->InvalidAccess = true; return 0; }
        if (device->ResetPending && ++device->ResetReads > device->ResetDelay)
        { device->Status = 0; device->ResetPending = false; }
        *output = device->Status;
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Write8(void* context, ulong address, byte value)
    {
        Device* device = (Device*)context;
        if (Fail(device)) return 0;
        if (address != Virtual + 20) { device->InvalidAccess = true; return 0; }
        if ((value & 4) != 0) device->DriverOkWritten = true;
        if (device->StatusWrites < 8) device->History[device->StatusWrites] = value;
        device->StatusWrites++;
        if (value == 0)
        {
            device->Status = 15; device->ResetPending = true; device->Selected = 0;
        }
        else if (value == device->RejectStatus) device->Status = device->RejectValue;
        else if (value == 11 && device->RejectFeatures) device->Status = 3;
        else device->Status = value;
        return 1;
    }

    [HostTest("virtio.negotiation checks reset and ordered version1 transaction on virtual addresses", HostLane.Fast, 0)]
    private static bool OrderedSuccess()
    {
        Device device = Fresh(); device.ResetDelay = 2;
        var result = Run(&device);
        HostCheck.True(result.Succeeded);
        HostCheck.Equal(3u, device.ResetReads);
        HostCheck.Equal(0x8000000100000001UL, result.Offered);
        HostCheck.Equal(0x100000000UL, result.Selected);
        HostCheck.Equal(result.Selected, device.Selected);
        HostCheck.Equal((byte)11, result.DeviceStatus);
        HostCheck.Equal(4u, device.StatusWrites);
        byte[] expected = [0, 1, 3, 11];
        for (int i = 0; i < 4; i++) HostCheck.Equal(expected[i], device.History[i]);
        return HostCheck.Result(!device.InvalidAccess && !device.DriverOkWritten && !result.FailureStatusAttempted);
    }

    [HostTest("virtio.negotiation bounds reset and rejects caller before access", HostLane.Fast, 1)]
    private static bool ResetAndAdmission()
    {
        Device device = Fresh(); device.ResetDelay = 100;
        var result = Run(&device, 3);
        HostCheck.Equal(VirtioNegotiationStatus.ResetTimeout, result.Status);
        HostCheck.Equal(3u, device.ResetReads);
        HostCheck.Equal(1u, device.StatusWrites);
        HostCheck.True(!result.FailureStatusAttempted);
        device = Fresh();
        result = ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.Run(Common, 0, 4, &device, &Read32, &Write32, &Read8, &Write8);
        HostCheck.Equal(VirtioNegotiationStatus.UnsupportedFeatures, result.Status);
        HostCheck.Equal(0u, device.Calls);
        foreach (uint budget in new uint[] { 0, 1025 }) HostCheck.Equal(VirtioNegotiationStatus.InvalidAccess, Run(&device, budget).Status);
        result = ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.Run(default, BoundedVirtioFeatures.Version1, 4, &device, &Read32, &Write32, &Read8, &Write8);
        HostCheck.Equal(VirtioNegotiationStatus.InvalidAccess, result.Status);
        HostCheck.Equal(0u, device.Calls);
        return HostCheck.Result(default(VirtioNegotiationResult).Succeeded == false);
    }

    [HostTest("virtio.negotiation rejects missing required features failed FEATURES_OK and device status", HostLane.Fast, 2)]
    private static bool Rejection()
    {
        Device device = Fresh(); device.Offered = 0xffffffff;
        var result = Run(&device);
        HostCheck.Equal(VirtioNegotiationStatus.MissingVersion1, result.Status);
        HostCheck.Equal(0UL, result.Selected);
        HostCheck.Equal((byte)0x83, device.Status);
        HostCheck.True(result.FailureStatusAttempted && result.FailureStatusWritten);
        device = Fresh(); device.RejectFeatures = true;
        result = Run(&device);
        HostCheck.Equal(VirtioNegotiationStatus.FeaturesRejected, result.Status);
        HostCheck.Equal((byte)3, result.DeviceStatus);
        HostCheck.Equal((byte)0x83, device.Status);
        foreach (byte bad in new byte[] { 0x43, 0x83, 7, 0x23 })
        {
            device = Fresh(); device.RejectStatus = 3; device.RejectValue = bad;
            result = Run(&device);
            HostCheck.Equal((bad & 0xc0) != 0 ? VirtioNegotiationStatus.DeviceFailed : VirtioNegotiationStatus.StatusRejected, result.Status);
            HostCheck.True(!result.Succeeded && !device.DriverOkWritten);
            HostCheck.Equal(0UL, result.Offered);
        }
        return true;
    }

    [HostTest("virtio.negotiation stops at each failed callback and reports failed cleanup", HostLane.Fast, 3)]
    private static bool AccessFailures()
    {
        Device baseline = Fresh(); HostCheck.True(Run(&baseline).Succeeded);
        for (uint fail = 1; fail <= baseline.Calls; fail++)
        {
            Device device = Fresh(); device.FailCall = fail;
            var result = Run(&device);
            HostCheck.True(result.Status == VirtioNegotiationStatus.ReadFailed || result.Status == VirtioNegotiationStatus.WriteFailed);
            HostCheck.True(!device.InvalidAccess && !device.DriverOkWritten && !result.Succeeded);
            HostCheck.True(device.Calls <= fail + 2);
        }
        Device rejected = Fresh(); rejected.RejectFeatures = true;
        var failure = Run(&rejected); HostCheck.True(failure.FailureStatusWritten);
        uint finalWrite = rejected.Calls;
        rejected = Fresh(); rejected.RejectFeatures = true; rejected.FailCall = finalWrite;
        failure = Run(&rejected);
        HostCheck.Equal(VirtioNegotiationStatus.FeaturesRejected, failure.Status);
        return HostCheck.Result(failure.FailureStatusAttempted && !failure.FailureStatusWritten && !rejected.DriverOkWritten);
    }

    [HostTest("virtio.negotiation does not erase device needs reset before advancing features status", HostLane.Fast, 4)]
    private static bool AsynchronousDeviceFailure()
    {
        Device device = Fresh(); device.NeedsResetAfterFeatures = true;
        var result = Run(&device);
        HostCheck.Equal(VirtioNegotiationStatus.DeviceFailed, result.Status);
        HostCheck.Equal((byte)0x43, result.DeviceStatus);
        HostCheck.Equal((byte)0xc3, device.Status);
        HostCheck.True(result.FailureStatusAttempted && result.FailureStatusWritten && !device.DriverOkWritten);
        for (uint i = 0; i < device.StatusWrites; i++) HostCheck.True(device.History[i] != 11);
        return true;
    }

    [HostTest("virtio.block.negotiation preserves read-only and rejects failed feature status and every callback")]
    private static void BlockNegotiation()
    {
        foreach (ulong offered in new ulong[] { 1UL << 32, (1UL << 32) | 32, ulong.MaxValue })
        {
            Device d = Fresh(); d.Offered = offered;
            var r = Run(&d, block: true);
            HostCheck.True(r.Succeeded && !d.InvalidAccess && !d.DriverOkWritten);
            HostCheck.Equal((1UL << 32) | (offered & 32), r.Selected);
            HostCheck.Equal(r.Selected, d.Selected);
            HostCheck.Equal((byte)11, d.Status);
            uint calls = d.Calls;
            for (uint fail = 1; fail <= calls; fail++)
            {
                d = Fresh(); d.Offered = offered; d.FailCall = fail;
                r = Run(&d, block: true);
                HostCheck.True(!r.Succeeded && !d.DriverOkWritten && !d.InvalidAccess);
                HostCheck.True(r.Status == VirtioNegotiationStatus.ReadFailed || r.Status == VirtioNegotiationStatus.WriteFailed);
            }
        }
        Device absent = Fresh(); absent.Offered = 32;
        HostCheck.Equal(VirtioNegotiationStatus.MissingVersion1, Run(&absent, block: true).Status);
        Device rejected = Fresh(); rejected.RejectFeatures = true;
        HostCheck.Equal(VirtioNegotiationStatus.FeaturesRejected, Run(&rejected, block: true).Status);
        Device broken = Fresh(); broken.NeedsResetAfterFeatures = true;
        HostCheck.Equal(VirtioNegotiationStatus.DeviceFailed, Run(&broken, block: true).Status);
        Device stuck = Fresh(); stuck.ResetDelay = 5;
        HostCheck.Equal(VirtioNegotiationStatus.ResetTimeout, Run(&stuck, 2, true).Status);
        HostCheck.True(!absent.DriverOkWritten && !rejected.DriverOkWritten && !broken.DriverOkWritten && !stuck.DriverOkWritten);
    }
}
