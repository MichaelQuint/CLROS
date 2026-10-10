using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe class N32BitN64BitBARsWhileAlwaysRestoringTests
{
    [HostTest("m05.008 native leaves preserve access widths and authenticate padding")]
    private static void NativeLeaves()
    {
        byte* code = stackalloc byte[48];
        byte[] expected =
        [
            0xf3,0x0f,0x1e,0xfa, 0x8b,0x02, 0x41,0x89,0x00, 0xb8,0x01,0,0,0, 0xc3,0,
            0xf3,0x0f,0x1e,0xfa, 0x44,0x89,0x02, 0xb8,0x01,0,0,0, 0xc3,0,0,0,
            0xf3,0x0f,0x1e,0xfa, 0x66,0x44,0x89,0x02, 0xb8,0x01,0,0,0, 0xc3,0,0
        ];
        HostCheck.True(X64PciBarAccess.Emit(code, 48));
        HostCheck.True(new ReadOnlySpan<byte>(code, 48).SequenceEqual(expected));
        for (int i = 0; i < 48; i++) { code[i] ^= 1; HostCheck.True(!X64PciBarAccess.Validate(code, 48)); code[i] ^= 1; }
        HostCheck.True(!X64PciBarAccess.Emit(code, 47) && !X64PciBarAccess.Validate(null, 48));
    }

    private struct Device
    {
        public fixed uint Registers[64];
        public fixed uint Masks[6];
        public uint Calls, FailAt, Writes, Violation;
        public bool RefuseDisable, RefuseRestore;
    }

    [HostTest("m05.008 sizes both widths and restores command without status writes")]
    private static void Normal()
    {
        Device device = Create(); PciSizedBar* bars = stackalloc PciSizedBar[6];
        var result = Run(&device, bars);
        HostCheck.True(result.Succeeded && result.Restored && result.RestoreAttempted);
        HostCheck.True(bars[0].Kind == PciSizedBarKind.Memory32 && bars[0].Bytes == 4096);
        HostCheck.True(bars[1].Kind == PciSizedBarKind.Io32 && bars[1].Bytes == 256);
        HostCheck.True(bars[2].Kind == PciSizedBarKind.Memory64 && bars[2].Base == 0x240000000 && bars[2].Bytes == 0x200000 && bars[2].Prefetchable);
        HostCheck.True(bars[3].Kind == PciSizedBarKind.UpperHalf && bars[4].Kind == PciSizedBarKind.Absent);
        Original(&device);
    }

    [HostTest("m05.008 every failed access attempts cleanup after any write")]
    private static void FailureMatrix()
    {
        Device baseline = Create(); PciSizedBar* bars = stackalloc PciSizedBar[6];
        HostCheck.True(Run(&baseline, bars).Succeeded);
        for (uint failure = 1; failure <= baseline.Calls; failure++)
        {
            Device device = Create(); device.FailAt = failure;
            for (int i = 0; i < 6; i++) bars[i] = default;
            var result = Run(&device, bars);
            HostCheck.True(!result.Succeeded && device.Calls >= failure, "failure=" + failure);
            HostCheck.True(result.RestoreAttempted == (device.Writes != 0), "cleanup=" + failure);
            HostCheck.True(bars[0].Kind == PciSizedBarKind.Uninitialized, "no partial output=" + failure);
            // Failed callbacks may already have written. All registers are nevertheless
            // restored in this one-shot transport-failure model; report uncertain reads.
            Original(&device, result.RestoreAttempted && !result.Restored);
        }
    }

    [HostTest("m05.008 rejects unsafe admission and leaves decode off on failed restore")]
    private static void Guards()
    {
        PciSizedBar* bars = stackalloc PciSizedBar[6]; Device d = Create();
        d.Registers[1] |= 4;
        HostCheck.True(Run(&d, bars).Status == PciBarProbeStatus.BusMasterEnabled && d.Writes == 0);
        d = Create(); d.Registers[3] = 0x10000;
        HostCheck.True(Run(&d, bars).Status == PciBarProbeStatus.UnsupportedHeader && d.Writes == 0);
        d = Create(); d.Registers[0] = uint.MaxValue;
        HostCheck.True(Run(&d, bars).Status == PciBarProbeStatus.AbsentDevice && d.Writes == 0);
        d = Create(); d.Registers[9] = 4;
        HostCheck.True(Run(&d, bars).Status == PciBarProbeStatus.UnsupportedType && d.Writes == 0);
        d = Create(); d.RefuseDisable = true;
        HostCheck.True(Run(&d, bars).Status == PciBarProbeStatus.DecodeNotDisabled);
        Original(&d);
        d = Create(); d.RefuseRestore = true;
        var result = Run(&d, bars);
        HostCheck.True(result.Status == PciBarProbeStatus.RestoreFailed && !result.Restored && (d.Registers[1] & 7) == 0);
        d = Create(); d.Masks[0] = 0xffff5000;
        HostCheck.True(Run(&d, bars).Status == PciBarProbeStatus.InvalidMask);
        Original(&d);
        HostCheck.True(!default(PciBarProbeResult).Succeeded);
        HostCheck.True(N32BitN64BitBARsWhileAlwaysRestoring.Probe(default, 0, 0, 0, &d, &Read, &Write32, &Write16, bars, 6).Status == PciBarProbeStatus.InvalidInput);
        BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 0, 0, 0, 0, out var window);
        uint calls = d.Calls;
        foreach (nuint address in new nuint[] { 0, 0xe0000000, 0xe00ff000, 1, nuint.MaxValue - 7 })
            HostCheck.True(N32BitN64BitBARsWhileAlwaysRestoring.Probe(window, 0, 0, 0, &d, &Read, &Write32, &Write16, (PciSizedBar*)address, 6).Status == PciBarProbeStatus.InvalidInput);
        HostCheck.True(N32BitN64BitBARsWhileAlwaysRestoring.Probe(window, 0, 0, 0, &d, &Read, &Write32, &Write16, bars, 5).Status == PciBarProbeStatus.InvalidInput && d.Calls == calls);
    }

    private static PciBarProbeResult Run(Device* device, PciSizedBar* bars)
    {
        BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 0, 0, 0, 0, out var window);
        return N32BitN64BitBARsWhileAlwaysRestoring.Probe(window, 0, 0, 0, device, &Read, &Write32, &Write16, bars, 6);
    }
    private static Device Create()
    {
        Device d = default; d.Registers[0] = 0x00051b36; d.Registers[1] = 0xf9000003;
        d.Registers[4] = 0x80000000; d.Registers[5] = 0xc001;
        d.Registers[6] = 0x4000000c; d.Registers[7] = 2;
        d.Masks[0] = 0xfffff000; d.Masks[1] = 0xffffff01;
        d.Masks[2] = 0xffe0000c; d.Masks[3] = uint.MaxValue;
        return d;
    }
    private static void Original(Device* d, bool mayBeDisabled = false)
    {
        HostCheck.True(d->Violation == 0 && (d->Registers[1] == 0xf9000003 || (mayBeDisabled && d->Registers[1] == 0xf9000000)));
        HostCheck.True(d->Registers[4] == 0x80000000 && d->Registers[5] == 0xc001 && d->Registers[6] == 0x4000000c && d->Registers[7] == 2 && d->Registers[8] == 0 && d->Registers[9] == 0);
    }
    [UnmanagedCallersOnly]
    private static byte Read(void* context, ulong address, uint* value)
    {
        Device* d = (Device*)context; d->Calls++;
        if (address < 0xe0000000 || address >= 0xe0000100 || (address & 3) != 0) { d->Violation++; return 0; }
        if (d->Calls == d->FailAt) return 0;
        uint index = (uint)(address - 0xe0000000) / 4;
        *value = d->Registers[index];
        if (index >= 4 && index <= 9 && *value == uint.MaxValue) *value = d->Masks[index - 4];
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write32(void* context, ulong address, uint value)
    {
        Device* d = (Device*)context; d->Calls++; d->Writes++;
        if (address < 0xe0000010 || address > 0xe0000024 || (address & 3) != 0) { d->Violation++; return 0; }
        uint index = (uint)(address - 0xe0000000) / 4;
        if (!(d->RefuseRestore && index == 4 && value == 0x80000000)) d->Registers[index] = value;
        return d->Calls == d->FailAt ? (byte)0 : (byte)1;
    }
    [UnmanagedCallersOnly]
    private static byte Write16(void* context, ulong address, uint value)
    {
        Device* d = (Device*)context; d->Calls++; d->Writes++;
        if (address != 0xe0000004 || value > 0xffff) { d->Violation++; return 0; }
        if (!d->RefuseDisable || (value & 3) != 0) d->Registers[1] = (d->Registers[1] & 0xffff0000) | value;
        return d->Calls == d->FailAt ? (byte)0 : (byte)1;
    }
}
