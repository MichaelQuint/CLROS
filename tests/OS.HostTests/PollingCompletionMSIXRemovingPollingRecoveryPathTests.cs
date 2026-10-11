using OS.Drivers;
using OS.Storage;

internal static unsafe class PollingCompletionMSIXRemovingPollingRecoveryPathTests
{
    private const ulong Code = 0x100000, Original = 0x200000, Temporary = 0x201000, Apic = 0xfee00000;
    private const uint Limit = 73;

    [HostTest("m06.msix classifies one interrupt and bounded restored polling recovery")]
    private static void Routes()
    {
        MsixInterruptContext c;
        Prepare(&c);
        HostCheck.True(Select(&c) == BlockCompletionRoute.PollingRecovery);
        c.Count = 1; c.BeforeEoiIsr = 1; c.Iterations = 1;
        c.InterruptedFlags = 0x202; c.InterruptedRip = Code + X64MsixInterrupt.InterruptStart;
        HostCheck.True(Select(&c, returned: 1) == BlockCompletionRoute.Interrupt);
        c.InterruptedRip = Code + X64MsixInterrupt.InterruptEnd;
        HostCheck.True(Select(&c, returned: 1) == BlockCompletionRoute.Interrupt);
        HostCheck.True(Select(&c, returned: 1, masked: false) == BlockCompletionRoute.InvalidWait);
        c.Count = 2;
        HostCheck.True(Select(&c, returned: 1) == BlockCompletionRoute.InvalidWait);
    }

    [HostTest("m06.msix rejects failed admission unrestored state and unexpected interrupt receipts")]
    private static void Receipts()
    {
        MsixInterruptContext c;
        for (int field = 0; field < 16; field++)
        {
            Prepare(&c);
            // IF in the two flags words; every other word includes a checked
            // counter, address, IDTR field or padding byte.
            ((ulong*)&c)[field] ^= field is 4 or 5 ? 0x200UL : 1UL;
            HostCheck.True(Select(&c) == BlockCompletionRoute.InvalidWait, field.ToString());
        }
        Prepare(&c); c.Iterations = 0;
        HostCheck.True(Select(&c) == BlockCompletionRoute.InvalidWait);
        Prepare(&c); c.Iterations = Limit - 1;
        HostCheck.True(Select(&c) == BlockCompletionRoute.InvalidWait);
        Prepare(&c);
        HostCheck.True(Select(&c, returned: 1) == BlockCompletionRoute.InvalidWait);
        HostCheck.True(Select(&c, returned: 2) == BlockCompletionRoute.InvalidWait);
        HostCheck.True(Select(&c, masked: false) == BlockCompletionRoute.InvalidWait);
        c = default;
        HostCheck.True(Select(&c) == BlockCompletionRoute.InvalidWait);
    }

    [HostTest("m06.msix rejects invalid wait storage limits and retained address ranges")]
    private static void Bounds()
    {
        MsixInterruptContext c; Prepare(&c);
        HostCheck.True(Select(null) == BlockCompletionRoute.InvalidWait);
        HostCheck.True(Select((MsixInterruptContext*)((byte*)&c + 1)) == BlockCompletionRoute.InvalidWait);
        HostCheck.True(Select((MsixInterruptContext*)(~0UL - 7)) == BlockCompletionRoute.InvalidWait);
        foreach (uint bad in new[] { 0u, X64MsixInterrupt.MaximumIterations + 1 })
            HostCheck.True(PollingCompletionMSIXRemovingPollingRecoveryPath.Select(&c, Code, Original, Temporary, Apic, bad, 0, true) == BlockCompletionRoute.InvalidWait);
        foreach (ulong bad in new[] { 0UL, ~0UL, 0x0000800000000000UL })
            HostCheck.True(PollingCompletionMSIXRemovingPollingRecoveryPath.Select(&c, bad, Original, Temporary, Apic, Limit, 0, true) == BlockCompletionRoute.InvalidWait);
        HostCheck.True(PollingCompletionMSIXRemovingPollingRecoveryPath.Select(&c, Code, Original, Original, Apic, Limit, 0, true) == BlockCompletionRoute.InvalidWait);
        HostCheck.True(PollingCompletionMSIXRemovingPollingRecoveryPath.Select(&c, Code, Original, Temporary, Apic + 1, Limit, 0, true) == BlockCompletionRoute.InvalidWait);
        foreach (uint boundary in new[] { 1u, X64MsixInterrupt.MaximumIterations })
        {
            Prepare(&c); c.Iterations = boundary;
            HostCheck.True(PollingCompletionMSIXRemovingPollingRecoveryPath.Select(&c, Code, Original, Temporary, Apic, boundary, 0, true) == BlockCompletionRoute.PollingRecovery);
        }
    }

    private static BlockCompletionRoute Select(MsixInterruptContext* c, uint returned = 0, bool masked = true) =>
        PollingCompletionMSIXRemovingPollingRecoveryPath.Select(c, Code, Original, Temporary, Apic, Limit, returned, masked);

    private static void Prepare(MsixInterruptContext* c)
    {
        *c = default; c->EoiAddress = Apic + 0xb0; c->EntryFlags = c->ExitFlags = 2; c->Iterations = Limit;
        Idtr(c->OriginalIdtr, Original); Idtr(c->TemporaryIdtr, Temporary); Idtr(c->RestoredIdtr, Original);
    }

    private static void Idtr(byte* bytes, ulong address)
    {
        bytes[0] = 0xff; bytes[1] = 0x0f;
        for (int i = 0; i < 8; i++) bytes[i + 2] = (byte)(address >> (i * 8));
    }
}
