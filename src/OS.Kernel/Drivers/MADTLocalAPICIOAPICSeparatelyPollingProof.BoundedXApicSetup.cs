using OS.Std.Binary;

namespace OS.Drivers;
/// <summary>Bounded integrated xAPIC and 82093-compatible IOAPIC setup. All LVT/IO
/// sources stay masked, timer stopped, TPR blocks classes 0..13. Only the later
/// owned MSI-X vector and spurious return gate may run with IF enabled. This code
/// never enables CPU interrupts, acknowledges an interrupt or changes APIC mode.
/// Any failure after writes requires the caller to keep IF clear and stop.
/// Register authority: Intel SDM Vol3A June2024 chapter11 and 82093AA section3.</summary>

public static unsafe class BoundedXApicSetup
{
    public const byte CompletionVector = 0xe0, SpuriousVector = 0xff;
    public const uint TaskPriority = 0xd0, SpuriousValue = 0x1ff, MaskedVector = 0x10051;
    public static XApicSetupResult Run(MadtPollingTopology t, uint cpuidFeatures, uint actualBspId, ulong apicBaseMsr, ulong rflags, XApicSetupAccess a)
    {
        XApicSetupResult r = default;
        r.Status = XApicSetupStatus.InvalidAccess;
        if (a.Read32 == null || a.Write32 == null || ((t.HeaderFlags & 1) != 0 && (a.ReadPort8 == null || a.WritePort8 == null)))
            return r;
        r.Status = XApicSetupStatus.InvalidTopology;
        if (t.RecordCount < 2 || t.BspApicId == 255 || t.IoApicId > 15 || (t.HeaderFlags & ~1U) != 0 || !Page(t.LocalApicAddress) || !Page(t.IoApicAddress) || t.LocalApicAddress == t.IoApicAddress)
            return r;
        r.Status = XApicSetupStatus.InvalidCpu;
        // MSR/APIC feature bits; BSP, globally enabled xAPIC with an exact below-4G base.
        if ((cpuidFeatures & 0x220) != 0x220 || actualBspId != t.BspApicId || apicBaseMsr != (t.LocalApicAddress | 0x900) || (rflags & 0x202) != 2)
            return r;
        r.Status = Configure(t, a, ref r);
        return r;
    }

    private static XApicSetupStatus Configure(MadtPollingTopology t, XApicSetupAccess a, ref XApicSetupResult r)
    {
        uint localId = 0, localVersion = 0, ioId = 0, ioVersion = 0;
        if (!Read(a, t.LocalApicAddress + 0x20, &localId) || !Read(a, t.LocalApicAddress + 0x30, &localVersion))
            return XApicSetupStatus.ReadFailed;
        var status = IoRead(t, a, 0, &ioId);
        if (status != XApicSetupStatus.Success)
            return status;
        status = IoRead(t, a, 1, &ioVersion);
        if (status != XApicSetupStatus.Success)
            return status;
        r.LocalVersion = localVersion;
        r.IoVersion = ioVersion;
        uint maxLvt = (localVersion >> 16) & 255, maxIo = (ioVersion >> 16) & 255;
        // Conventional LVT layout, up to 24 IO pins, and the reviewed 82093AA
        // (0x11) or QEMU (0x20) IOREGSEL/IOWIN interface. No direct IO EOI use.
        if (localId != (uint)t.BspApicId << 24 || ioId != (uint)t.IoApicId << 24 || (localVersion & 255) < 0x10 || (localVersion & 255) > 0x15 || maxLvt < 3 || maxLvt > 6 || (localVersion & 0xfe00ff00) != 0 || (ioVersion & 0xff00ff00) != 0 || ((ioVersion & 255) != 0x11 && (ioVersion & 255) != 0x20) || maxIo > 23 || t.IoApicGsiBase > ~0U - maxIo || (t.IsoCount != 0 && t.MaximumIsoGsi > t.IoApicGsiBase + maxIo))
            return XApicSetupStatus.UnsupportedHardware;
        if ((t.HeaderFlags & 1) != 0)
        {
            byte master = 0, slave = 0;
            if (a.WritePort8(a.Context, 0x21, 255) != 1 || a.WritePort8(a.Context, 0xa1, 255) != 1)
                return XApicSetupStatus.WriteFailed;
            if (a.ReadPort8(a.Context, 0x21, &master) != 1 || a.ReadPort8(a.Context, 0xa1, &slave) != 1)
                return XApicSetupStatus.ReadFailed;
            if (master != 255 || slave != 255)
                return XApicSetupStatus.ReadBackFailed;
            r.LegacyPicMasked = true;
        }

        // Mask each old route before touching its destination; no source is enabled.
        for (uint i = 0; i <= maxIo; i++)
        {
            status = IoWriteChecked(t, a, 0x10 + 2 * i, MaskedVector);
            if (status != XApicSetupStatus.Success)
                return status;
            status = IoWriteChecked(t, a, 0x11 + 2 * i, (uint)t.BspApicId << 24);
            if (status != XApicSetupStatus.Success)
                return status;
            r.MaskedIoEntries++;
        }

        // Timer, LINT0/1 and error are mandatory; performance/thermal/CMCI follow maxLVT.
        for (uint i = 0; i <= maxLvt; i++)
        {
            uint offset = i == 0 ? 0x320U : i == 1 ? 0x350U : i == 2 ? 0x360U : i == 3 ? 0x370U : i == 4 ? 0x340U : i == 5 ? 0x330U : 0x2f0U;
            status = WriteChecked(a, t.LocalApicAddress + offset, MaskedVector);
            if (status != XApicSetupStatus.Success)
                return status;
            r.MaskedLocalEntries++;
        }

        status = WriteChecked(a, t.LocalApicAddress + 0x380, 0);
        if (status != XApicSetupStatus.Success)
            return status;
        status = WriteChecked(a, t.LocalApicAddress + 0x80, TaskPriority);
        if (status != XApicSetupStatus.Success)
            return status;
        r.TaskPriority = TaskPriority;
        status = WriteChecked(a, t.LocalApicAddress + 0xf0, SpuriousValue);
        if (status != XApicSetupStatus.Success)
            return status;
        r.Spurious = SpuriousValue;
        return CheckQuiescent(t.LocalApicAddress, a);
    }

    // Recheck immediately before enabling the owned MSI-X table entry. A pending
    // lower-priority firmware vector remains blocked by TPR; no existing ISR is ours.
    public static XApicSetupStatus CheckQuiescent(ulong localAddress, XApicSetupAccess a)
    {
        if (!Page(localAddress) || a.Read32 == null)
            return XApicSetupStatus.InvalidAccess;
        uint value = 0;
        for (uint i = 0; i < 8; i++)
        {
            if (!Read(a, localAddress + 0x100 + i * 16, &value))
                return XApicSetupStatus.ReadFailed;
            if (value != 0)
                return XApicSetupStatus.PendingInterrupt;
        }

        if (!Read(a, localAddress + 0x270, &value))
            return XApicSetupStatus.ReadFailed;
        if (value != 0)
            return XApicSetupStatus.PendingInterrupt;
        return XApicSetupStatus.Success;
    }

    private static bool Page(ulong v) => v >= 0x100000 && v <= 0xfffff000 && (v & 4095) == 0;
    private static bool Read(XApicSetupAccess a, ulong address, uint* value) => a.Read32(a.Context, address, value) == 1;
    private static XApicSetupStatus WriteChecked(XApicSetupAccess a, ulong address, uint value)
    {
        if (a.Write32(a.Context, address, value) != 1)
            return XApicSetupStatus.WriteFailed;
        uint observed = 0;
        if (!Read(a, address, &observed))
            return XApicSetupStatus.ReadFailed;
        return observed == value ? XApicSetupStatus.Success : XApicSetupStatus.ReadBackFailed;
    }

    private static XApicSetupStatus IoRead(MadtPollingTopology t, XApicSetupAccess a, uint index, uint* value)
    {
        if (a.Write32(a.Context, t.IoApicAddress, index) != 1)
            return XApicSetupStatus.WriteFailed;
        return Read(a, t.IoApicAddress + 16, value) ? XApicSetupStatus.Success : XApicSetupStatus.ReadFailed;
    }

    private static XApicSetupStatus IoWriteChecked(MadtPollingTopology t, XApicSetupAccess a, uint index, uint value)
    {
        if (a.Write32(a.Context, t.IoApicAddress, index) != 1 || a.Write32(a.Context, t.IoApicAddress + 16, value) != 1)
            return XApicSetupStatus.WriteFailed;
        uint observed = 0;
        if (!Read(a, t.IoApicAddress + 16, &observed))
            return XApicSetupStatus.ReadFailed;
        return observed == value ? XApicSetupStatus.Success : XApicSetupStatus.ReadBackFailed;
    }
}
