using OS.Std.Binary;

namespace OS.Drivers;
/// <summary>Fixed Microsoft x64 leaves, separately authenticated by the adapter.
/// Wait ABI: uint (MsixInterruptContext*, uint limit). The caller must prove CPL0,
/// IF=0, exclusive admitted context/stack, two intact owned IDTs, RX code and UC
/// LAPIC EOI/ISR registers. Only this leaf runs with IF=1. It preserves R15, swaps
/// IDTR, bounds PAUSE iterations, disables IF and restores IDTR before returning.
/// The returning handler preserves RAX, captures RIP/RFLAGS and LAPIC ISR around
/// EOI, then IRETQ. Its R15 context is valid only inside the wait region. The
/// spurious gate counts and IRETQs without EOI. Never invoke these on the host.</summary>

public static unsafe class X64MsixInterrupt
{
    public const uint Bytes = 192, WaitOffset = 0, HandlerOffset = 112, SpuriousOffset = 176;
    public const uint InterruptStart = 54, InterruptEnd = 70, MaximumIterations = 1000000;
    public static bool Emit(byte* code, ulong capacity)
    {
        if (!Storage(code, capacity))
            return false;
        for (uint i = 0; i < Bytes; i++)
            code[i] = Byte(i);
        return true;
    }

    public static bool Validate(byte* code, ulong capacity)
    {
        if (!Storage(code, capacity))
            return false;
        for (uint i = 0; i < Bytes; i++)
            if (code[i] != Byte(i))
                return false;
        return true;
    }

    public static bool PrepareTemporaryIdt(byte* original, byte* temporary, ulong capacity, ulong originalStubs, ulong code)
    {
        if (!Tables(original, temporary, capacity, originalStubs, code))
            return false;
        for (uint i = 0; i < 4096; i++)
            temporary[i] = original[i];
        HandlerAddress(temporary + BoundedXApicSetup.CompletionVector * 16, code + HandlerOffset);
        HandlerAddress(temporary + BoundedXApicSetup.SpuriousVector * 16, code + SpuriousOffset);
        return ValidateTemporaryIdt(original, temporary, capacity, originalStubs, code);
    }

    public static bool ValidateTemporaryIdt(byte* original, byte* temporary, ulong capacity, ulong originalStubs, ulong code)
    {
        if (!Tables(original, temporary, capacity, originalStubs, code))
            return false;
        for (uint i = 0; i < 4096; i++)
        {
            uint vector = i / 16, offset = i & 15;
            ulong target = vector == BoundedXApicSetup.CompletionVector ? code + HandlerOffset : vector == BoundedXApicSetup.SpuriousVector ? code + SpuriousOffset : 0;
            byte expected = original[i];
            if (target != 0)
            {
                if (offset < 2)
                    expected = (byte)(target >> ((int)offset * 8));
                else if (offset >= 6 && offset < 12)
                    expected = (byte)(target >> ((int)(offset - 4) * 8));
            }

            if (temporary[i] != expected)
                return false;
        }

        return true;
    }

    private static bool Tables(byte* original, byte* temporary, ulong capacity, ulong originalStubs, ulong code)
    {
        if (capacity != 4096 || ((ulong)temporary & 15) != 0 || !Range((ulong)temporary, 4096) || !Range(code, Bytes) || !OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(original, 4096, (ulong)original, originalStubs).Succeeded)
            return false;
        ulong t = (ulong)temporary, o = (ulong)original;
        return !(t < o + 4096 && o < t + 4096) && !(t < code + Bytes && code < t + 4096) && !(t < originalStubs + 4096 && originalStubs < t + 4096) && !(o < code + Bytes && code < o + 4096);
    }

    private static void HandlerAddress(byte* gate, ulong handler)
    {
        gate[0] = (byte)handler;
        gate[1] = (byte)(handler >> 8);
        for (uint i = 0; i < 6; i++)
            gate[6 + i] = (byte)(handler >> ((int)(i + 2) * 8));
    }

    // These numeric observations are necessary but do not authenticate mappings,
    // table contents, code or DMA. The adapter must independently prove those.
    public static bool ValidateCompletion(MsixInterruptContext* c, ulong code, ulong originalIdt, ulong temporaryIdt, ulong localApic, uint limit, uint returned)
    {
        return ValidWait(c, code, originalIdt, temporaryIdt, localApic, limit) && returned == 1 && c->Count == 1 && c->SpuriousCount == 0 && c->BeforeEoiIsr == 1 && c->AfterEoiIsr == 0 && (c->InterruptedFlags & 0x202) == 0x202 && c->InterruptedRip >= code + InterruptStart && c->InterruptedRip <= code + InterruptEnd;
    }

    // A timeout is recoverable only after the leaf ran its entire bounded wait
    // and restored IF/IDTR. A failed admission or unexpected interrupt is not
    // a timeout receipt. The adapter must mask delivery and audit live state.
    public static bool ValidatePollingRecovery(MsixInterruptContext* c, ulong code, ulong originalIdt, ulong temporaryIdt, ulong localApic, uint limit, uint returned)
    {
        return ValidWait(c, code, originalIdt, temporaryIdt, localApic, limit) && returned == 0 && c->Iterations == limit && c->Count == 0 && c->SpuriousCount == 0 && c->InterruptedRip == 0 && c->InterruptedFlags == 0 && c->BeforeEoiIsr == 0 && c->AfterEoiIsr == 0;
    }

    private static bool ValidWait(MsixInterruptContext* c, ulong code, ulong originalIdt, ulong temporaryIdt, ulong localApic, uint limit)
    {
        if (c == null || ((ulong)c & 7) != 0 || (ulong)c > ~0UL - (ulong)sizeof(MsixInterruptContext) || !Range(code, Bytes) || !Range(originalIdt, 4096) || !Range(temporaryIdt, 4096) || (originalIdt < temporaryIdt + 4096 && temporaryIdt < originalIdt + 4096) || localApic < 0x100000 || localApic > 0xfffff000 || (localApic & 4095) != 0 || c->EoiAddress != localApic + 0xb0 || limit == 0 || limit > MaximumIterations || c->Iterations == 0 || c->Iterations > limit || (c->EntryFlags & 0x202) != 2 || (c->ExitFlags & 0x202) != 2)
            return false;
        return Idtr(c->OriginalIdtr, originalIdt) && Idtr(c->RestoredIdtr, originalIdt) && Idtr(c->TemporaryIdtr, temporaryIdt);
    }

    private static bool Idtr(byte* bytes, ulong address)
    {
        if (bytes[0] != 255 || bytes[1] != 15 || EndianPrimitives.ReadUInt64LittleEndian(bytes + 2) != address)
            return false;
        for (uint i = 10; i < 16; i++)
            if (bytes[i] != 0)
                return false;
        return true;
    }

    private static bool Range(ulong start, ulong bytes) => start >= 4096 && start <= 0x0000800000000000UL - bytes;
    private static bool Storage(byte* p, ulong capacity) => p != null && capacity >= Bytes && (ulong)p <= ~0UL - Bytes;
    private static byte Byte(uint i)
    {
        ulong word = (i / 8) switch
        {
            // Wait: admission; preserve R15; SIDT/LIDT; STI; bounded loop;
            // CLI; restore/read IDTR; capture IF; restore R15; return count.
            0 => 0x8948589cfa1e0ff3UL,
            1 => 0x7500000200a92041UL,
            2 => 0x40fa814d74d28551UL,
            3 => 0x7983484577000f42UL,
            4 => 0x894957413e750008UL,
            5 => 0x0f41384f010f41cfUL,
            6 => 0x90f3fbc031485f01UL,
            7 => 0x00087f8349c0ff48UL,
            8 => 0x49faf072d0390475UL,
            9 => 0x385f010f41304789UL,
            10 => 0x49589c584f010f41UL,
            11 => 0x4108478b41284789UL,
            12 => 0x000000c3c031c35fUL,
            13 => 0,
            // Returning vector 0xe0 gate. With RAX pushed, RIP/RFLAGS are at
            // RSP+8/+24 in the 64-bit interrupt frame. ISR7 is EOI+0xc0.
            14 => 0x448b4850fa1e0ff3UL,
            15 => 0x8b48104789490824UL,
            16 => 0x4918478949182444UL,
            17 => 0x000000c0808b078bUL,
            18 => 0xc7078b4970478949UL,
            19 => 0xc0808b0000000000UL,
            20 => 0x4978478949000000UL,
            21 => 0x0000cf48580847ffUL,
            // Spurious vector never writes EOI.
            22 => 0x6847ff49fa1e0ff3UL,
            23 => 0x000000000000cf48UL,
            _ => 0
        };
        return (byte)(word >> ((int)(i & 7) * 8));
    }
}
