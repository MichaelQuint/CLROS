using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
/// <summary>
/// Bind the two entries from the original x64 EFI tables, never caller-supplied
/// replacement callbacks. The adapter supplies the actual EfiMain table/handle,
/// valid readable extents and an owned firmware-map snapshot. Table CRCs detect
/// corruption, not malicious firmware or forged provenance. Memory-map code
/// classification is not a page-table execute-permission check. Actual firmware
/// CR3/mapping authority remains the adapter's obligation before the final call.
/// No firmware function is called here. Consume rechecks table identity/content
/// before the final window and clears this binding before exposing its entries.
/// Retain one original state exclusively; do not copy/reset it or alias outputs.
/// </summary>

public static unsafe class FirstExitFirmwareBinding
{
    private const ulong LowEnd = 0x0000800000000000;
    private const ulong SystemSignature = 0x5453595320494249;
    private const ulong BootSignature = 0x56524553544f4f42;
    public static FirstExitBindingStatus Bind(ref FirstExitFirmwareBindingState state, FirmwareServicesMode mode, void* imageHandle, byte* systemTable, ulong systemBytes, byte* bootServices, ulong bootBytes, FirmwareMemoryMapRecord ownedMap)
    {
        if (state.Phase != 0)
            return FirstExitBindingStatus.AlreadyBound;
        if (mode != FirmwareServicesMode.BootServicesAvailable)
            return FirstExitBindingStatus.FirmwareUnavailable;
        if (imageHandle == null || !Extent((ulong)systemTable, systemBytes) || systemBytes < 120 || systemBytes > 4096 || !Extent((ulong)bootServices, bootBytes) || bootBytes < 240 || bootBytes > 4096 || ((ulong)systemTable & 7) != 0 || ((ulong)bootServices & 7) != 0 || FirstExitDescriptors.Overlaps((ulong)systemTable, systemBytes, (ulong)bootServices, bootBytes))
            return FirstExitBindingStatus.InvalidStorage;
        if (!ownedMap.HasOwnedCopy() || ownedMap.DescriptorVersion != 1 || !UefiMemoryMapCopyPlanResult.Create(ownedMap, ownedMap.MapSize).Succeeded || !Extent(ownedMap.BufferAddress, ownedMap.MapSize))
            return FirstExitBindingStatus.InvalidMap;
        fixed (FirstExitFirmwareBindingState* address = &state)
        {
            ulong start = (ulong)address, bytes = (ulong)sizeof(FirstExitFirmwareBindingState);
            if (!Extent(start, bytes) || FirstExitDescriptors.Overlaps(start, bytes, (ulong)systemTable, systemBytes) || FirstExitDescriptors.Overlaps(start, bytes, (ulong)bootServices, bootBytes) || FirstExitDescriptors.Overlaps(start, bytes, ownedMap.BufferAddress, ownedMap.MapSize) || FirstExitDescriptors.Overlaps(ownedMap.BufferAddress, ownedMap.MapSize, (ulong)systemTable, systemBytes) || FirstExitDescriptors.Overlaps(ownedMap.BufferAddress, ownedMap.MapSize, (ulong)bootServices, bootBytes))
                return FirstExitBindingStatus.InvalidStorage;
        }

        if (!Header(systemTable, systemBytes, 120, SystemSignature) || !Header(bootServices, bootBytes, 240, BootSignature))
            return FirstExitBindingStatus.InvalidTable;
        if (*(ulong*)(systemTable + 96) != (ulong)bootServices)
            return FirstExitBindingStatus.WrongBootServicesTable;
        ulong map = *(ulong*)(bootServices + 56), exit = *(ulong*)(bootServices + 232);
        if (!Extent(map, 1) || !Extent(exit, 1) || map == exit)
            return FirstExitBindingStatus.InvalidFunction;
        uint mapMatches = 0, exitMatches = 0;
        bool mapCode = false, exitCode = false;
        for (ulong i = 0; i < ownedMap.DescriptorCount; i++)
        {
            byte* descriptor = (byte*)(ownedMap.BufferAddress + i * ownedMap.DescriptorSize);
            ulong start = *(ulong*)(descriptor + 8), pages = *(ulong*)(descriptor + 24);
            if ((start & 4095) != 0 || pages == 0 || pages > (UefiMemoryMapCopyPlan.MaximumUnsignedLong - start) / 4096)
                return FirstExitBindingStatus.InvalidMap;
            ulong end = start + pages * 4096;
            uint type = *(uint*)descriptor;
            bool code = type == 3 || type == 5; // EfiBootServicesCode / EfiRuntimeServicesCode
            if (start <= map && map < end)
            {
                mapMatches++;
                mapCode = code;
            }

            if (start <= exit && exit < end)
            {
                exitMatches++;
                exitCode = code;
            }
        }

        if (mapMatches != 1 || exitMatches != 1 || !mapCode || !exitCode)
            return FirstExitBindingStatus.NotFirmwareCode;
        state.SystemTable = systemTable;
        state.SystemBytes = systemBytes;
        state.BootServices = bootServices;
        state.BootBytes = bootBytes;
        state.SystemCrc = *(uint*)(systemTable + 16);
        state.BootCrc = *(uint*)(bootServices + 16);
        state.MapFunction = map;
        state.ExitFunction = exit;
        state.ImageHandle = imageHandle;
        state.Phase = 1;
        return FirstExitBindingStatus.Ready;
    }

    public static FirstExitBindingStatus Consume(ref FirstExitFirmwareBindingState state, FirmwareServicesMode mode, out delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap, out delegate* unmanaged<void*, ulong, ulong> exitBootServices, out void* imageHandle)
    {
        getMemoryMap = null;
        exitBootServices = null;
        imageHandle = null;
        if (!state.IsReady)
            return FirstExitBindingStatus.NotReady;
        // Do not dereference any firmware storage when services are unavailable.
        if (mode != FirmwareServicesMode.BootServicesAvailable)
        {
            state = default;
            state.Phase = 2;
            return FirstExitBindingStatus.FirmwareUnavailable;
        }

        bool unchanged = Header(state.SystemTable, state.SystemBytes, 120, SystemSignature) && Header(state.BootServices, state.BootBytes, 240, BootSignature) && *(uint*)(state.SystemTable + 16) == state.SystemCrc && *(uint*)(state.BootServices + 16) == state.BootCrc && *(ulong*)(state.SystemTable + 96) == (ulong)state.BootServices && *(ulong*)(state.BootServices + 56) == state.MapFunction && *(ulong*)(state.BootServices + 232) == state.ExitFunction;
        if (unchanged)
        {
            getMemoryMap = (delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong>)state.MapFunction;
            exitBootServices = (delegate* unmanaged<void*, ulong, ulong>)state.ExitFunction;
            imageHandle = state.ImageHandle;
        }

        state = default;
        state.Phase = 2;
        return unchanged ? FirstExitBindingStatus.Consumed : FirstExitBindingStatus.Changed;
    }

    private static bool Header(byte* table, ulong capacity, uint minimum, ulong signature)
    {
        uint size = *(uint*)(table + 12);
        if (*(ulong*)table != signature || *(uint*)(table + 8) < 0x20000 || size < minimum || size > capacity || *(uint*)(table + 20) != 0)
            return false;
        uint crc = Crc32.InitialState;
        for (uint i = 0; i < size; i++)
            crc = Crc32.UpdateByte(crc, i >= 16 && i < 20 ? (byte)0 : table[i]);
        return Crc32.Finish(crc) == *(uint*)(table + 16);
    }

    private static bool Extent(ulong start, ulong bytes) => start != 0 && start < LowEnd && bytes != 0 && bytes < LowEnd - start;
}
