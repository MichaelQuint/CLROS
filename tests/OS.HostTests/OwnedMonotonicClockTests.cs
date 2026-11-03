using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Kernel.Platform;

internal static unsafe partial class OwnedMonotonicClockTests
{
    private const uint Id = 0x8086a201;
    private const ulong Capabilities = (100000000UL << 32) | Id;
    private static ulong tscValue;
    [HostTest("m05.clock acpi device authority rejects ram and malformed maps")]
    private static void MapAdmission()
    {
        ulong* rows = stackalloc ulong[10];
        for (int i = 0; i < 10; i++)
            rows[i] = 0;
        rows[0] = 7;
        rows[1] = 0;
        rows[3] = 256;
        rows[5] = 11;
        rows[6] = 0xfed00000;
        rows[8] = 1;
        OS.Boot.FirmwareMemoryMapRecord map = new()
        {
            BufferAddress = (ulong)rows,
            BufferSize = 80,
            MapSize = 80,
            DescriptorSize = 40,
            DescriptorCount = 2,
            DescriptorVersion = 1,
            Flags = 1
        };
        byte* table = stackalloc byte[56];
        new Span<byte>(table, 56).Clear();
        var span = new Span<byte>(table, 56);
        BinaryPrimitives.WriteUInt32LittleEndian(span, 0x54455048);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 56);
        table[8] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(span[36..], Id);
        BinaryPrimitives.WriteUInt64LittleEndian(span[44..], 0xfed00000);
        byte checksum = 0;
        for (int i = 0; i < 56; i++)
            checksum = unchecked((byte)(checksum + table[i]));
        table[9] = unchecked((byte)-checksum);
        HostCheck.True(OwnedMonotonicClock.TryReadDescription(table, 56, out var description));
        HostCheck.True(OwnedMonotonicClock.AdmitDescription(description, map));
        rows[5] = 0;
        HostCheck.True(OwnedMonotonicClock.AdmitDescription(description, map));
        foreach (ulong type in new ulong[]
        {
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            12,
            13,
            ulong.MaxValue
        }

        )
        {
            rows[5] = type;
            HostCheck.True(!OwnedMonotonicClock.AdmitDescription(description, map));
        }

        rows[5] = 11;
        rows[8] = 0;
        HostCheck.True(!OwnedMonotonicClock.AdmitDescription(description, map));
        rows[8] = ulong.MaxValue;
        HostCheck.True(!OwnedMonotonicClock.AdmitDescription(description, map));
        rows[8] = 1;
        rows[6]++;
        HostCheck.True(!OwnedMonotonicClock.AdmitDescription(description, map));
        rows[6]--;
        rows[0] = 11;
        rows[1] = 0xfed00000;
        rows[3] = 1;
        HostCheck.True(!OwnedMonotonicClock.AdmitDescription(description, map));
        rows[0] = 7;
        rows[1] = 0;
        rows[3] = 256;
        map.DescriptorCount = 1;
        map.MapSize = 40;
        HostCheck.True(OwnedMonotonicClock.AdmitDescription(description, map)); // ACPI authority with omitted EFI MMIO
        map.Flags = 0;
        HostCheck.True(!OwnedMonotonicClock.AdmitDescription(description, map));
        map.Flags = 1;
        map.DescriptorSize = 32;
        HostCheck.True(!OwnedMonotonicClock.AdmitDescription(description, map));
        HostCheck.True(!OwnedMonotonicClock.AdmitDescription(default, map));
    }

    [HostTest("m05.clock retained hpet description rejects malformed inputs")]
    private static void Description()
    {
        byte[] table = new byte[56];
        BinaryPrimitives.WriteUInt32LittleEndian(table, 0x54455048);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(4), 56);
        table[8] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(36), Id);
        table[41] = 64;
        BinaryPrimitives.WriteUInt64LittleEndian(table.AsSpan(44), 0xfed00000);
        Checksum(table);
        fixed (byte* p = table)
        {
            HostCheck.True(OwnedMonotonicClock.TryReadDescription(p, 56, out var description));
            HostCheck.Equal(0xfed00000UL, description.Address);
            HostCheck.Equal(Id, description.HardwareId);
            HostCheck.True(!OwnedMonotonicClock.TryReadDescription(p, 55, out _));
            HostCheck.True(!OwnedMonotonicClock.TryReadDescription(null, 56, out _));
            foreach ((int offset, byte value)in new (int, byte)[]
            {
                (0, 0),
                (4, 57),
                (8, 2),
                (36, 0),
                (40, 1),
                (41, 32),
                (42, 1),
                (43, 1),
                (44, 1),
                (48, 1),
                (55, 3)
            }

            )
            {
                byte original = table[offset];
                table[offset] = value;
                Checksum(table);
                HostCheck.True(!OwnedMonotonicClock.TryReadDescription(p, 56, out var rejected));
                HostCheck.Equal(0UL, rejected.Address);
                table[offset] = original;
                Checksum(table);
            }

            table[9]++;
            HostCheck.True(!OwnedMonotonicClock.TryReadDescription(p, 56, out _));
        }
    }

    [HostTest("m05.clock hpet starts once preserves reserved bits and accumulates wrap")]
    private static void Starts()
    {
        Device device = CreateDevice();
        var state = new OwnedMonotonicClockState();
        HostCheck.True(OwnedMonotonicClock.Start(ref state, Access(&device), default));
        HostCheck.Equal(OwnedClockSource.Hpet, state.Source);
        HostCheck.Equal(10000000UL, state.FrequencyHz);
        HostCheck.Equal(100000000U, state.PeriodFemtoseconds);
        HostCheck.Equal(64U, state.CounterWidth);
        HostCheck.Equal(0U, state.RecoveryAttempts);
        HostCheck.Equal(0x5a01UL, device.Config);
        for (int i = 0; i < 3; i++)
            HostCheck.Equal(0x1000000000000030UL, device.Timers[i]);
        HostCheck.True(OwnedMonotonicClock.Read(ref state));
        HostCheck.Equal(7UL, state.Ticks);
        uint operations = device.Reads + device.Writes;
        HostCheck.True(!OwnedMonotonicClock.Start(ref state, Access(&device), default));
        HostCheck.Equal(operations, device.Reads + device.Writes);
        device = CreateDevice();
        device.Capabilities &= ~(1UL << 13);
        device.Counter = uint.MaxValue - 35;
        state = default;
        HostCheck.True(OwnedMonotonicClock.Start(ref state, Access(&device), default));
        for (int i = 0; i < 20; i++)
            HostCheck.True(OwnedMonotonicClock.Read(ref state));
        HostCheck.Equal(140UL, state.Ticks);
        HostCheck.Equal(32U, state.CounterWidth);
        device.Config &= ~1UL;
        HostCheck.True(!OwnedMonotonicClock.Read(ref state));
        HostCheck.Equal(OwnedClockStatus.ConfigurationFailed, state.Status);
        HostCheck.Equal(0U, device.Resets);
    }

    [HostTest("m05.clock halted and enabled-stalled hpet receive one bounded recovery")]
    private static void Recovery()
    {
        for (int scenario = 0; scenario < 2; scenario++)
        {
            Device device = CreateDevice();
            if (scenario == 0)
                device.Config &= ~1UL;
            else
                device.Stalled = true;
            var state = new OwnedMonotonicClockState();
            HostCheck.True(OwnedMonotonicClock.Start(ref state, Access(&device), default));
            HostCheck.Equal(1U, state.RecoveryAttempts);
            HostCheck.Equal(1U, device.Resets);
            HostCheck.True(device.CounterReads <= OwnedMonotonicClock.MaximumMotionSamples + 8);
            HostCheck.True(OwnedMonotonicClock.Read(ref state));
            HostCheck.Equal(7UL, state.Ticks);
        }

        Device changed = CreateDevice();
        changed.Stalled = true;
        changed.ChangeCapabilities = true;
        var rejected = new OwnedMonotonicClockState();
        HostCheck.True(!OwnedMonotonicClock.Start(ref rejected, Access(&changed), default));
        HostCheck.Equal(OwnedClockStatus.ConfigurationFailed, rejected.HpetStatus);
        HostCheck.Equal(1U, changed.Resets);
    }

    [HostTest("m05.clock stalled hpet fails closed or selects qualified tsc once")]
    private static void Fallback()
    {
        Device device = CreateDevice();
        device.Stalled = true;
        device.PermanentStall = true;
        var state = new OwnedMonotonicClockState();
        HostCheck.True(!OwnedMonotonicClock.Start(ref state, Access(&device), default));
        HostCheck.Equal(OwnedClockStatus.Stalled, state.HpetStatus);
        HostCheck.Equal(OwnedClockStatus.TscUnavailable, state.Status);
        HostCheck.Equal(OwnedClockSource.None, state.Source);
        HostCheck.Equal(0UL, state.FrequencyHz);
        HostCheck.Equal(1U, device.Resets);
        HostCheck.True(device.CounterReads <= 2 * OwnedMonotonicClock.MaximumMotionSamples + 3);
        uint writes = device.Writes;
        HostCheck.True(!OwnedMonotonicClock.Start(ref state, Access(&device), Tsc()));
        HostCheck.True(!OwnedMonotonicClock.Read(ref state));
        HostCheck.Equal(writes, device.Writes);
        device = CreateDevice();
        device.Stalled = true;
        device.PermanentStall = true;
        state = default;
        tscValue = 100;
        HostCheck.True(OwnedMonotonicClock.Start(ref state, Access(&device), Tsc()));
        HostCheck.Equal(OwnedClockSource.Tsc, state.Source);
        HostCheck.Equal(OwnedClockStatus.Stalled, state.HpetStatus);
        HostCheck.Equal(2400000000UL, state.FrequencyHz);
        HostCheck.Equal(0U, state.PeriodFemtoseconds);
        HostCheck.True(OwnedMonotonicClock.Read(ref state));
        HostCheck.Equal(11UL, state.Ticks);
        device = CreateDevice();
        device.Capabilities &= 0xffffffff;
        state = default;
        HostCheck.True(!OwnedMonotonicClock.Start(ref state, Access(&device), default));
        HostCheck.Equal(OwnedClockStatus.InvalidCapabilities, state.HpetStatus);
        HostCheck.Equal(0U, device.Writes);
        device = CreateDevice();
        device.IgnoreTimerWrites = true;
        state = default;
        HostCheck.True(!OwnedMonotonicClock.Start(ref state, Access(&device), default));
        HostCheck.Equal(OwnedClockStatus.ConfigurationFailed, state.HpetStatus);
        HostCheck.Equal(0U, device.Resets);
    }

    [HostTest("m05.clock tsc frequency requires complete exact invariant metadata")]
    private static void TscFrequency()
    {
        var valid = Tsc();
        HostCheck.True(OwnedMonotonicClock.TryTscFrequency(valid, out ulong frequency));
        HostCheck.Equal(2400000000UL, frequency);
        for (int i = 0; i < 11; i++)
        {
            var invalid = valid;
            switch (i)
            {
                case 0:
                    invalid.IntelVendor = false;
                    break;
                case 1:
                    invalid.MaximumBasicLeaf = 0x14;
                    break;
                case 2:
                    invalid.MaximumExtendedLeaf = 0x80000006;
                    break;
                case 3:
                    invalid.Leaf1Edx = 0;
                    break;
                case 4:
                    invalid.Leaf80000007Edx = 0;
                    break;
                case 5:
                    invalid.Denominator = 0;
                    break;
                case 6:
                    invalid.Numerator = 0;
                    break;
                case 7:
                    invalid.CrystalHz = 0;
                    break;
                case 8:
                    invalid.Denominator = 7;
                    break;
                case 9:
                    invalid.CrystalHz = 1;
                    break;
                case 10:
                    invalid.Numerator = uint.MaxValue;
                    break;
            }

            HostCheck.True(!OwnedMonotonicClock.TryTscFrequency(invalid, out frequency));
            HostCheck.Equal(0UL, frequency);
        }

        var state = new OwnedMonotonicClockState();
        valid.Read = &StalledTsc;
        HostCheck.True(!OwnedMonotonicClock.Start(ref state, default, valid));
        HostCheck.Equal(OwnedClockStatus.Stalled, state.Status);
        state = default;
        valid.Read = &BackwardsTsc;
        tscValue = 100;
        HostCheck.True(!OwnedMonotonicClock.Start(ref state, default, valid));
        HostCheck.Equal(OwnedClockStatus.InvalidCounter, state.Status);
    }

    private static void Checksum(byte[] table)
    {
        table[9] = 0;
        byte total = 0;
        foreach (byte b in table)
            total = unchecked((byte)(total + b));
        table[9] = unchecked((byte)-total);
    }

    private static Device CreateDevice()
    {
        Device device = new()
        {
            Capabilities = Capabilities,
            Config = 0x5a03,
            Counter = 100
        };
        for (int i = 0; i < 32; i++)
            device.Timers[i] = 0x1000000000004034;
        return device;
    }

    private static OwnedHpetAccess Access(Device* device) => new()
    {
        Context = device,
        Read = &Read,
        Write = &Write,
        HardwareId = (uint)device->Capabilities
    };
    private static OwnedTscAccess Tsc() => new()
    {
        Read = &ReadTsc,
        IntelVendor = true,
        MaximumBasicLeaf = 0x15,
        MaximumExtendedLeaf = 0x80000007,
        Leaf1Edx = 16,
        Leaf80000007Edx = 256,
        Denominator = 1,
        Numerator = 100,
        CrystalHz = 24000000
    };
    [UnmanagedCallersOnly]
    private static ulong ReadTsc()
    {
        tscValue += 11;
        return tscValue;
    }

    [UnmanagedCallersOnly]
    private static ulong StalledTsc() => 100;
    [UnmanagedCallersOnly]
    private static ulong BackwardsTsc() => --tscValue;
    [UnmanagedCallersOnly]
    private static ulong Read(void* context, uint offset)
    {
        var device = (Device*)context;
        device->Reads++;
        if (offset == 0)
            return device->Capabilities;
        if (offset == 0x10)
            return device->Config;
        if (offset == 0xf0)
        {
            device->CounterReads++;
            if ((device->Config & 1) != 0 && !device->Stalled)
            {
                device->Counter += 7;
                if ((device->Capabilities & (1UL << 13)) == 0)
                    device->Counter &= uint.MaxValue;
            }

            return device->Counter;
        }

        return device->Timers[(offset - 0x100) / 0x20];
    }

    [UnmanagedCallersOnly]
    private static void Write(void* context, uint offset, ulong value)
    {
        var device = (Device*)context;
        device->Writes++;
        if (offset == 0x10)
        {
            if ((device->Config & 1) == 0 && (value & 1) != 0 && device->Resets > 0)
            {
                if (!device->PermanentStall)
                    device->Stalled = false;
                if (device->ChangeCapabilities)
                    device->Capabilities ^= 1UL << 32;
            }

            device->Config = value;
        }
        else if (offset == 0xf0)
        {
            if ((device->Config & 1) == 0)
            {
                device->Counter = value;
                device->Resets++;
            }
        }
        else if (!device->IgnoreTimerWrites)
            device->Timers[(offset - 0x100) / 0x20] = value;
    }
}
