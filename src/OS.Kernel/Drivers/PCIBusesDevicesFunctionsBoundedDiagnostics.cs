namespace OS.Drivers;
public static unsafe class PCIBusesDevicesFunctionsBoundedDiagnostics
{
    public static BoundedPciScanResult Scan(ref BoundedPciAccess access, BoundedPciFunction* output, uint capacity, uint readBudget)
    {
        BoundedPciScanResult result = default;
        result.Status = BoundedPciStatus.InvalidAccess;
        if (!access.IsActive || output == null || capacity == 0 || capacity > 1024 || ((ulong)output & 7) != 0 || (ulong)output > ~0UL - capacity * (ulong)sizeof(BoundedPciFunction) || readBudget == 0 || readBudget > 1024)
            return result;
        var window = access.Window;
        result.CoversAllocation = window.FirstBus == window.AllocationFirst && window.LastBus == window.AllocationLast;
        for (uint bus = window.FirstBus; bus <= window.LastBus; bus++)
            for (uint device = 0; device < 32; device++)
            {
                uint functions = 1;
                for (uint function = 0; function < functions; function++)
                {
                    if (result.Visited == readBudget)
                    {
                        result.Status = BoundedPciStatus.ReadBudget;
                        return result;
                    }

                    result.Visited++;
                    result.HasLast = true;
                    result.LastSegment = window.Segment;
                    result.LastBus = (byte)bus;
                    result.LastDevice = (byte)device;
                    result.LastFunction = (byte)function;
                    var status = ECAMPageVendorDeviceHeader.ReadHeader(ref access, (byte)bus, (byte)device, (byte)function, out var header);
                    if (status == BoundedPciStatus.AbsentDevice)
                        continue;
                    if (status != BoundedPciStatus.Success)
                    {
                        result.Status = status;
                        return result;
                    }

                    if (function == 0 && (header.HeaderType & 0x80) != 0)
                        functions = 8;
                    if ((header.HeaderType & 0x7f) == 1)
                    {
                        uint primary = header.BusNumbers & 255, secondary = (header.BusNumbers >> 8) & 255, subordinate = (header.BusNumbers >> 16) & 255;
                        bool disabled = secondary == 0 && subordinate == 0;
                        if ((header.ClassRevision >> 16) != 0x0604 || (disabled ? primary != 0 && primary != bus : primary != bus || secondary <= bus || subordinate < secondary || secondary < window.AllocationFirst || subordinate > window.AllocationLast))
                        {
                            result.Status = BoundedPciStatus.InvalidBridge;
                            return result;
                        }

                        if (!disabled && (secondary < window.FirstBus || subordinate > window.LastBus))
                            result.HasUnscannedBridge = true;
                    }

                    if (result.Count == capacity)
                    {
                        result.RequiredCapacity = result.Count + 1;
                        result.Status = BoundedPciStatus.Capacity;
                        return result;
                    }

                    output[result.Count++] = new BoundedPciFunction
                    {
                        Segment = window.Segment,
                        Bus = (byte)bus,
                        Device = (byte)device,
                        Function = (byte)function,
                        Header = header
                    };
                }
            }

        result.RequiredCapacity = result.Count;
        result.Status = BoundedPciStatus.Success;
        return result;
    }
}
