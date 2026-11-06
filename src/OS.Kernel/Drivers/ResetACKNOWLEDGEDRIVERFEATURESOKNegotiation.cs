namespace OS.Drivers;
/// <summary>Exclusive modern PCI common-config negotiation. Adapter authenticates
/// mapped UC/RW/NX storage and non-throwing callbacks (1=success). No DRIVER_OK,
/// queue, interrupt or DMA action. Access faults are outside this callback contract.</summary>

public static unsafe class ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation
{
    public static VirtioNegotiationResult Run(VirtioMappedRegion common, ulong requested, uint resetReads, void* context, delegate* unmanaged<void*, ulong, uint*, byte> read32, delegate* unmanaged<void*, ulong, uint, byte> write32, delegate* unmanaged<void*, ulong, byte*, byte> read8, delegate* unmanaged<void*, ulong, byte, byte> write8) => RunCore(common, requested, resetReads, context, read32, write32, read8, write8, false);
    /// <summary>Explicit block-only feature profile. Retains RO if offered; never
    /// enables writes, DRIVER_OK, interrupts or queues.</summary>

    public static VirtioNegotiationResult RunBlock(VirtioMappedRegion common, uint resetReads, void* context, delegate* unmanaged<void*, ulong, uint*, byte> read32, delegate* unmanaged<void*, ulong, uint, byte> write32, delegate* unmanaged<void*, ulong, byte*, byte> read8, delegate* unmanaged<void*, ulong, byte, byte> write8) => RunCore(common, BoundedVirtioFeatures.Version1, resetReads, context, read32, write32, read8, write8, true);
    /// <summary>Separate one-buffer net receive profile; does not relax Run or
    /// RunBlock admission. No DRIVER_OK, queue or DMA action.</summary>

    public static VirtioNegotiationResult RunNetReceive(VirtioMappedRegion common, uint resetReads, void* context, delegate* unmanaged<void*, ulong, uint*, byte> read32, delegate* unmanaged<void*, ulong, uint, byte> write32, delegate* unmanaged<void*, ulong, byte*, byte> read8, delegate* unmanaged<void*, ulong, byte, byte> write8) => RunCore(common, BoundedVirtioFeatures.NetSingleBufferReceive, resetReads, context, read32, write32, read8, write8, false, true);
    private static VirtioNegotiationResult RunCore(VirtioMappedRegion common, ulong requested, uint resetReads, void* context, delegate* unmanaged<void*, ulong, uint*, byte> read32, delegate* unmanaged<void*, ulong, uint, byte> write32, delegate* unmanaged<void*, ulong, byte*, byte> read8, delegate* unmanaged<void*, ulong, byte, byte> write8, bool block, bool receive = false)
    {
        if (!common.IsValid || common.Bytes < VirtioPciModernRegisterMap.CommonConfiguration.FirstLiveQueueSetupLength || (common.VirtualStart & 3) != 0 || resetReads == 0 || resetReads > 1024 || read32 == null || write32 == null || read8 == null || write8 == null)
            return new(VirtioNegotiationStatus.InvalidAccess, 0, 0, 0, false, false);
        // Reject an unsupported caller profile before touching the device.
        if (requested != (receive ? BoundedVirtioFeatures.NetSingleBufferReceive : BoundedVirtioFeatures.Version1))
            return new(VirtioNegotiationStatus.UnsupportedFeatures, 0, 0, 0, false, false);
        ulong address = common.VirtualStart;
        byte status = 0;
        ulong offered = 0, selected = 0;
        var failure = write8(context, address + 20, 0) == 1 ? VirtioNegotiationStatus.Success : VirtioNegotiationStatus.WriteFailed;
        bool reset = false;
        for (uint i = 0; failure == VirtioNegotiationStatus.Success && i < resetReads; i++)
        {
            if (read8(context, address + 20, &status) != 1)
            {
                failure = VirtioNegotiationStatus.ReadFailed;
                break;
            }

            if (status == 0)
            {
                reset = true;
                break;
            }
        }

        if (failure == VirtioNegotiationStatus.Success && !reset)
            failure = VirtioNegotiationStatus.ResetTimeout;
        if (failure == VirtioNegotiationStatus.Success)
            failure = SetStatus(address, 1, context, read8, write8, out status);
        if (failure == VirtioNegotiationStatus.Success)
            failure = SetStatus(address, 3, context, read8, write8, out status);
        for (uint i = 0; failure == VirtioNegotiationStatus.Success && i < 2; i++)
        {
            uint value = 0;
            if (write32(context, address, i) != 1)
                failure = VirtioNegotiationStatus.WriteFailed;
            else if (read32(context, address + 4, &value) != 1)
                failure = VirtioNegotiationStatus.ReadFailed;
            else
                offered |= (ulong)value << (int)(i * 32);
        }

        if (failure == VirtioNegotiationStatus.Success)
        {
            var choice = receive ? BoundedVirtioFeatures.SelectNetReceive(offered, out selected) : block ? BoundedVirtioFeatures.SelectBlock(offered, out selected) : BoundedVirtioFeatures.Select(offered, requested, out selected);
            if (choice != VirtioFeatureStatus.Success)
                failure = choice == VirtioFeatureStatus.MissingReceiveBuffers ? VirtioNegotiationStatus.UnsupportedFeatures : VirtioNegotiationStatus.MissingVersion1;
        }

        for (uint i = 0; failure == VirtioNegotiationStatus.Success && i < 2; i++)
            if (write32(context, address + 8, i) != 1 || write32(context, address + 12, (uint)(selected >> (int)(i * 32))) != 1)
                failure = VirtioNegotiationStatus.WriteFailed;
        if (failure == VirtioNegotiationStatus.Success)
        {
            failure = SetStatus(address, 11, context, read8, write8, out status);
            if (failure == VirtioNegotiationStatus.StatusRejected)
                failure = status == 3 ? VirtioNegotiationStatus.FeaturesRejected : failure;
        }

        if (failure == VirtioNegotiationStatus.Success)
            return new(failure, offered, selected, status, false, false);
        // Signal FAILED only after reset completed and a fresh status read
        // permits adding that bit without clearing bits or setting DRIVER_OK.
        // Preserve the original failure if cleanup access also fails.
        byte current = 0;
        bool attempted = reset && read8(context, address + 20, &current) == 1 && (current & 0x34) == 0;
        bool written = attempted && write8(context, address + 20, (byte)(current | 0x80)) == 1;
        return new(failure, offered, selected, status, attempted, written);
    }

    private static VirtioNegotiationStatus SetStatus(ulong address, byte expected, void* context, delegate* unmanaged<void*, ulong, byte*, byte> read, delegate* unmanaged<void*, ulong, byte, byte> write, out byte observed)
    {
        observed = 0;
        byte prior = 0;
        if (read(context, address + 20, &prior) != 1)
            return VirtioNegotiationStatus.ReadFailed;
        observed = prior;
        if ((prior & 0xc0) != 0)
            return VirtioNegotiationStatus.DeviceFailed;
        byte required = expected == 1 ? (byte)0 : expected == 3 ? (byte)1 : (byte)3;
        if (prior != required)
            return VirtioNegotiationStatus.StatusRejected;
        // Read before each advance: a device-side failure between earlier
        // stages must not be cleared by replacing status with a cached value.
        if (write(context, address + 20, (byte)(prior | expected)) != 1)
            return VirtioNegotiationStatus.WriteFailed;
        byte value = 0;
        if (read(context, address + 20, &value) != 1)
            return VirtioNegotiationStatus.ReadFailed;
        observed = value;
        if (expected == 11)
        {
            var result = BoundedVirtioFeatures.ValidateFeaturesOk(value);
            if (result == VirtioFeatureStatus.Success)
                return VirtioNegotiationStatus.Success;
            if (result == VirtioFeatureStatus.FeaturesRejected)
                return VirtioNegotiationStatus.FeaturesRejected;
            if (result == VirtioFeatureStatus.DeviceFailed)
                return VirtioNegotiationStatus.DeviceFailed;
            return VirtioNegotiationStatus.StatusRejected;
        }

        if ((value & 0xc0) != 0)
            return VirtioNegotiationStatus.DeviceFailed;
        return value == expected ? VirtioNegotiationStatus.Success : VirtioNegotiationStatus.StatusRejected;
    }
}
