#if M10_004_PROBE
using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;

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
            ulong sourceStatus = getTime(&snapshot, null);
            if (sourceStatus != 0 || !OwnedMonotonicClock.Read(ref state->Clock) ||
                state->Clock.Ticks < before || state->Clock.Ticks - before > state->Clock.FrequencyHz) return false;
#if M16_019_UNAVAILABLE_PROBE || M16_019_INVALID_PROBE
            if (!OwnedRealtimeClock.TryFirmwareUtc(snapshot, unspecifiedTimezoneIsUtc: true,out _,out _)) return false;
#endif
#if M16_019_UNAVAILABLE_PROBE
            // Fault only the copied result after a successful actual GetTime.
            // Ordinary admission must reject; do not manufacture a terminal.
            sourceStatus = 0x8000000000000003UL;
#elif M16_019_INVALID_PROBE
            snapshot.Month = 0;
#endif
            bool admitted = AdmitRealtime(state, sourceStatus, snapshot);
#if M16_019_UNAVAILABLE_PROBE || M16_019_INVALID_PROBE
            if (!admitted && !state->Realtime.IsAvailable)
            {
                state->RealtimeAdmissionControl = Probes.EnableM16019UnavailableProbe ? 1U : 2U;
                state->RealtimeRejectedStatus = sourceStatus; state->RealtimeRejectedMonth = snapshot.Month;
            }
#endif
            if (!admitted) return false;
            state->RealtimeCaptureTicks = state->Clock.Ticks - before;
            return true;
        }

        private static bool AdmitRealtime(Storage* state, ulong sourceStatus, OwnedRealtimeClock.FirmwareTime snapshot)
        {
            if (sourceStatus != 0 || !OwnedRealtimeClock.TryFirmwareUtc(snapshot, unspecifiedTimezoneIsUtc: true,
                out long seconds, out uint nanoseconds) || !OwnedRealtimeClock.Begin(ref state->Realtime, seconds, nanoseconds,
                    state->Clock.Ticks, state->Clock.FrequencyHz, maximumElapsedSeconds: 600)) return false;
            state->RealtimeSnapshot = snapshot;
            return true;
        }

#if M16_019_UNAVAILABLE_PROBE || M16_019_INVALID_PROBE
        // Firmware output is valid only on the existing pre-entry rejection
        // branch. No post-exit kernel or workload entry occurred.
        private static void WriteRealtimeAdmissionRejection(Storage* state, EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
        {
            if (state->PreparationCheckpoint != 96 || state->RealtimeAdmissionControl == 0 || state->Realtime.IsAvailable) return;
            string start = "TEST_START m16.019\r\nM16_019_UTC_REJECT case=";
            for (int i = 0; i < start.Length; i++) Write(console, serial, (byte)start[i]);
            WriteUnsignedDecimal(console, serial, state->RealtimeAdmissionControl);
            string status = " sourceStatus=";
            for (int i = 0; i < status.Length; i++) Write(console, serial, (byte)status[i]);
            WriteUnsignedDecimal(console, serial, state->RealtimeRejectedStatus);
            string month = " month=";
            for (int i = 0; i < month.Length; i++) Write(console, serial, (byte)month[i]);
            WriteUnsignedDecimal(console, serial, state->RealtimeRejectedMonth);
            string end = " available=0 firmware=1 entered=0 ready=0\r\nTEST_FAIL m16.019 admission\r\n";
            for (int i = 0; i < end.Length; i++) Write(console, serial, (byte)end[i]);
        }
#endif

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
