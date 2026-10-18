#if M10_004_PROBE
using OS.Kernel.Platform;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // This default-off web fixture declares QEMU's UTC RTC contract, made
        // explicit by QemuCommandBuilder. Unspecified firmware timezone is not
        // a general UTC authority. No firmware function/table is retained here.
        private static bool PrepareRealtime(void* context)
        {
            Storage* state = (Storage*)context;
            if (state == null || state->Phase != 2 || state->SystemTable == null ||
                state->Clock.Status != OwnedClockStatus.Uninitialized || state->Realtime.IsAvailable) return false;
            byte* runtime = (byte*)state->SystemTable->RuntimeServices;
            var map = s_bootHandoff.MemoryMap;
            if (!TableExtent(runtime, 136, map, out ulong bytes, out _) ||
                *(ulong*)runtime != 0x56524553544e5552UL || *(uint*)(runtime + 8) < 0x20000 ||
                *(uint*)(runtime + 20) != 0) return false;
            uint crc = OS.Std.Hash.Crc32.InitialState;
            for (uint i = 0; i < bytes; i++)
                crc = OS.Std.Hash.Crc32.UpdateByte(crc, i >= 16 && i < 20 ? (byte)0 : runtime[i]);
            if (OS.Std.Hash.Crc32.Finish(crc) != *(uint*)(runtime + 16)) return false;
            ulong function = *(ulong*)(runtime + 24);
            if (!RealtimeFirmwareFunction(function, map)) return false;

            byte* access = state->Memory.Code + FirstExitCodeBundle.ClockAccessOffset;
            if (!X64OwnedClockAccess.Validate(access, X64OwnedClockAccess.Bytes)) return false;
            var hpet = new OwnedHpetAccess
            {
                Context = (void*)state->HpetDescription.Address, HardwareId = state->HpetDescription.HardwareId,
                Read = (delegate* unmanaged<void*, uint, ulong>)(access + X64OwnedClockAccess.ReadOffset),
                Write = (delegate* unmanaged<void*, uint, ulong, void>)(access + X64OwnedClockAccess.WriteOffset)
            };
            // Start exactly once while firmware still exists, bracket GetTime,
            // and preserve this same HPET owner/epoch through the exit handoff.
            if (!OwnedMonotonicClock.Start(ref state->Clock, hpet, ReadClockCpu(access)) ||
                state->Clock.Source != OwnedClockSource.Hpet || !OwnedMonotonicClock.Read(ref state->Clock)) return false;
            ulong before = state->Clock.Ticks;
            OwnedRealtimeClock.FirmwareTime snapshot = default;
            var getTime = (delegate* unmanaged<OwnedRealtimeClock.FirmwareTime*, void*, ulong>)function;
            if (getTime(&snapshot, null) != 0 || !OwnedMonotonicClock.Read(ref state->Clock) ||
                state->Clock.Ticks < before || state->Clock.Ticks - before > state->Clock.FrequencyHz ||
                !OwnedRealtimeClock.TryFirmwareUtc(snapshot, unspecifiedTimezoneIsUtc: true,
                    out long seconds, out uint nanoseconds) ||
                !OwnedRealtimeClock.Begin(ref state->Realtime, seconds, nanoseconds,
                    state->Clock.Ticks, state->Clock.FrequencyHz, maximumElapsedSeconds: 600)) return false;
            state->RealtimeSnapshot = snapshot;
            state->RealtimeCaptureTicks = state->Clock.Ticks - before;
            return true;
        }

        // Read only the copied map. GetTime must reside in an original runtime
        // code descriptor without EFI_MEMORY_XP; no callback survives preparation.
        private static bool RealtimeFirmwareFunction(ulong function, FirmwareMemoryMapRecord map)
        {
            if (function == 0 || !map.HasOwnedCopy()) return false;
            uint matches = 0;
            for (ulong i = 0; i < map.DescriptorCount; i++)
            {
                byte* row = (byte*)(map.BufferAddress + i * map.DescriptorSize);
                ulong start = *(ulong*)(row + 8), pages = *(ulong*)(row + 24);
                if (pages == 0 || pages > (ulong.MaxValue - start) / Page) return false;
                if (function < start || function >= start + pages * Page) continue;
                if (*(uint*)row != 5 || (*(ulong*)(row + 32) & 0x4000) != 0) return false;
                matches++;
            }
            return matches == 1;
        }
    }
}
#endif
