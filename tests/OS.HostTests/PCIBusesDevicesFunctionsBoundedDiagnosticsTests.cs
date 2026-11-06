using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class PCIBusesDevicesFunctionsBoundedDiagnosticsTests
{
    [HostTest("m05.006 scan has exact capacity deterministic order and last visited tuple")]
    private static void CompleteAndTruncated()
    {
        Context context = default;
        var access = Open(&context, 0, 0);
        BoundedPciFunction* output = stackalloc BoundedPciFunction[4];
        output[3].Segment = 0xaaaa;
        var result = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 3, 256);
        HostCheck.True(result.Complete && result.Count == 3 && result.Visited == 39 && result.CanEstablishAbsenceInAllocation);
        HostCheck.True(result.HasLast && result.LastSegment == 2 && result.LastBus == 0 && result.LastDevice == 31 && result.LastFunction == 0);
        HostCheck.True(output[0].Device == 0 && output[0].Function == 0 && output[1].Device == 0 && output[1].Function == 3 && output[2].Device == 2);
        HostCheck.True(output[3].Segment == 0xaaaa);
        var again = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 3, 256);
        HostCheck.True(again.Complete && again.Count == result.Count && again.Visited == result.Visited);
        output[2].Segment = 0xbbbb;
        result = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 2, 256);
        HostCheck.True(result.Status == BoundedPciStatus.Capacity && !result.Complete && !result.CanEstablishAbsenceInAllocation);
        HostCheck.True(result.Count == 2 && result.RequiredCapacity == 3 && result.LastDevice == 2 && result.LastFunction == 0 && output[2].Segment == 0xbbbb);
        result = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 3, 1);
        HostCheck.True(result.Status == BoundedPciStatus.ReadBudget && result.Visited == 1 && result.LastDevice == 0 && result.LastFunction == 0);
    }

    [HostTest("m05.006 bridge ranges and limited scope cannot establish broad absence")]
    private static void Bridges()
    {
        Context context = new()
        {
            Bridge = true,
            BusNumbers = 0x00010100
        };
        var access = Open(&context, 0, 1);
        BoundedPciFunction* output = stackalloc BoundedPciFunction[8];
        var result = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 8, 512);
        HostCheck.True(result.Complete && result.Count == 6 && result.CoversAllocation && !result.HasUnscannedBridge);
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 2, 0, 1, 0, 0, out var window));
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryOpen(window, &context, &Read, out access));
        result = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 8, 256);
        HostCheck.True(result.Complete && result.HasUnscannedBridge && !result.CoversAllocation && !result.CanEstablishAbsenceInAllocation);
        foreach (uint buses in new uint[]
        {
            0x00000100,
            0x00010102,
            0x00020100,
            0x00010200
        }

        )
        {
            context.BusNumbers = buses;
            result = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 8, 256);
            HostCheck.True(result.Status == BoundedPciStatus.InvalidBridge && !result.Complete);
        }

        context.BusNumbers = 0;
        HostCheck.True(PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 8, 256).Complete);
    }

    [HostTest("m05.006 scan invalid storage and default sessions make no reads")]
    private static void Invalid()
    {
        Context context = default;
        var access = Open(&context, 0, 0);
        BoundedPciFunction* output = stackalloc BoundedPciFunction[1];
        HostCheck.True(PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 0, 256).Status == BoundedPciStatus.InvalidAccess);
        HostCheck.True(PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, null, 1, 256).Status == BoundedPciStatus.InvalidAccess);
        HostCheck.True(PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 1, 0).Status == BoundedPciStatus.InvalidAccess);
        HostCheck.True(context.Calls == 0 && !default(BoundedPciScanResult).Complete);
        BoundedPCIConfigurationSpaceInterface.Revoke(ref access);
        HostCheck.True(PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, output, 1, 256).Status == BoundedPciStatus.InvalidAccess && context.Calls == 0);
    }

    private static BoundedPciAccess Open(Context* context, byte first, byte last)
    {
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 2, first, last, first, last, out var window));
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryOpen(window, context, &Read, out var access));
        return access;
    }

    [UnmanagedCallersOnly]
    private static uint Read(void* pointer, ulong address)
    {
        var c = (Context*)pointer;
        c->Calls++;
        uint bus = (uint)((address >> 20) & 255), device = (uint)((address >> 15) & 31), function = (uint)((address >> 12) & 7), offset = (uint)(address & 4095);
        bool present = device == 0 && (function == 0 || function == 3) || device == 2 && function == 0;
        if (!present)
            return uint.MaxValue;
        bool bridge = c->Bridge && bus == 0 && device == 2;
        return offset switch
        {
            0 => 0x12348086,
            8 => bridge ? 0x06040000U : 0x02000000U,
            12 => bridge ? 0x00010000U : device == 0 ? 0x00800000U : 0,
            24 => bridge ? c->BusNumbers : 0,
            _ => 0
        };
    }
}
