namespace OS.Boot;

#pragma warning disable 649

public static unsafe class EfiEntry
{
    private const int OwnedMemoryMapBufferSize = 128 * 1024;
    private const ulong OwnedMemoryRegionCapacity = 4096;
    private const int BootPageAllocatorNodeCapacity = 4096;
    private const int BootPageAllocatorNodeSlotCount = 6;
    private const ulong BootPageAllocatorInvalidNodeIndex = 0xffffffffffffffff;
    private const uint UefiConventionalMemoryType = 7;
    private const int BootPageAllocatorNodeSourceRegionIndexSlot = 0;
    private const int BootPageAllocatorNodePhysicalStartSlot = 1;
    private const int BootPageAllocatorNodePhysicalEndExclusiveSlot = 2;
    private const int BootPageAllocatorNodePageCountSlot = 3;
    private const int BootPageAllocatorNodeByteLengthSlot = 4;
    private const int BootPageAllocatorNodeNextIndexSlot = 5;
#if ACPI_PHYSICAL_DISCOVERY_PROBE
    private const int AcpiRsdpScratchSize = 4096;
    private const int AcpiRootSdtBufferSize = 65536;
    private const int AcpiChildHeaderScratchSize = 36;
    private const int AcpiChildTableBufferSize = 65536;
    private const int AcpiPointerScratchCapacity = 256;
    private const int AcpiEcamSegmentScratchCapacity = 16;
    private const uint UefiAcpiReclaimMemoryType = 9;
    private const uint UefiAcpiMemoryNvsType = 10;
    private const uint AcpiRsdtSignature = 0x54445352;
    private const uint AcpiXsdtSignature = 0x54445358;
    private const uint AcpiMcfgSignature = 0x4746434d;
    private const int AcpiSdtHeaderLength = 36;
    private const int AcpiMcfgAllocationOffset = 44;
    private const int AcpiMcfgAllocationLength = 16;
#endif

    private static EfiMemoryMapSnapshot s_memoryMapSnapshot;
    private static BootHandoffRecord s_bootHandoff;
    private static OwnedMemoryMapBuffer s_ownedMemoryMap;
    private static OwnedMemoryRegionBuffer s_ownedMemoryRegions;
    private static BootPageAllocatorNodeBuffer s_bootPageAllocatorNodes;
    private static EfiMemoryMapSummary s_memoryMapSummary;
    private static BootMemoryRegionMaterializationResult s_memoryRegionMaterialization;
    private static EfiBootPageAllocatorReadinessResult s_bootPageAllocatorReadiness;
    private static EfiConfigurationTableScanResult s_acpiRsdpScanResult;
    private static UefiMemoryMapCopyPlanStatus s_memoryMapCopyStatus;
#if ACPI_PHYSICAL_DISCOVERY_PROBE
    private static AcpiRsdpScratchBuffer s_acpiRsdpScratch;
    private static AcpiRootSdtBuffer s_acpiRootSdt;
    private static AcpiChildHeaderScratchBuffer s_acpiChildHeaderScratch;
    private static AcpiChildTableBuffer s_acpiChildTable;
    private static AcpiPointerScratchBuffer s_acpiPointerScratch;
    private static AcpiEcamSegmentScratchBuffer s_acpiEcamSegmentScratch;
#endif

    [System.Runtime.RuntimeExport("EfiMain")]
    private static long EfiMain(System.IntPtr imageHandle, EfiSystemTable* systemTable)
    {
        EfiSimpleTextOutputProtocol* console = GetFirmwareConsole(systemTable);
        EfiSerialIoProtocol* serial = console == null ? GetFirmwareSerial(systemTable) : null;
        WriteBootMarkers(console, serial, systemTable);

        return 0;
    }

    private static void Main()
    {
    }

    private static EfiSimpleTextOutputProtocol* GetFirmwareConsole(EfiSystemTable* systemTable)
    {
        if (systemTable == null || systemTable->ConOut == null || systemTable->ConOut->OutputString == null)
        {
            return null;
        }

        return systemTable->ConOut;
    }

    private static EfiSerialIoProtocol* GetFirmwareSerial(EfiSystemTable* systemTable)
    {
        if (systemTable == null || systemTable->BootServices == null || systemTable->BootServices->LocateProtocol == null)
        {
            return null;
        }

        EfiGuid serialIoGuid;
        serialIoGuid.Data1 = 0xbb25cf6f;
        serialIoGuid.Data2 = 0xf1d4;
        serialIoGuid.Data3 = 0x11d2;
        serialIoGuid.Data4_0 = 0x9a;
        serialIoGuid.Data4_1 = 0x0c;
        serialIoGuid.Data4_2 = 0x00;
        serialIoGuid.Data4_3 = 0x90;
        serialIoGuid.Data4_4 = 0x27;
        serialIoGuid.Data4_5 = 0x3f;
        serialIoGuid.Data4_6 = 0xc1;
        serialIoGuid.Data4_7 = 0xfd;

        void* serialInterface = null;
        ulong status = systemTable->BootServices->LocateProtocol(&serialIoGuid, null, &serialInterface);
        if (status != 0 || serialInterface == null)
        {
            return null;
        }

        EfiSerialIoProtocol* serial = (EfiSerialIoProtocol*)serialInterface;
        if (serial->Write == null)
        {
            return null;
        }

        return serial;
    }

    private static void WriteBootMarkers(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteEfiMainStart(console, serial);
        WriteEfiConsoleReady(console, serial);
        if (TryCaptureUefiMemoryMap(systemTable))
        {
            WriteUefiMemoryMapReady(console, serial);
            WriteUefiMemoryMapDescriptorCount(console, serial, s_memoryMapSnapshot.DescriptorCount);
            if (s_memoryMapSnapshot.IsOwned)
            {
                WriteUefiMemoryMapOwned(console, serial);
                EfiBootHandoffStatus handoffStatus = EfiBootHandoffValidator.Validate(s_bootHandoff);
                if (handoffStatus != EfiBootHandoffStatus.Success)
                {
                    WriteBootHandoffFail(console, serial, handoffStatus);
                    return;
                }

                WriteBootHandoffReady(console, serial);

                EfiMemoryMapNormalizeResult normalizeResult = EfiMemoryMapNormalizer.Normalize(
                    s_memoryMapSnapshot.Buffer,
                    s_memoryMapSnapshot.MapSize,
                    s_memoryMapSnapshot.DescriptorSize,
                    s_memoryMapSnapshot.DescriptorVersion,
                    s_memoryMapSnapshot.DescriptorCount);
                if (!normalizeResult.Succeeded)
                {
                    WriteUefiMemoryMapNormalizeFail(console, serial, normalizeResult.Status);
                    return;
                }

                s_memoryMapSummary = normalizeResult.Summary;
                WriteUefiMemoryMapNormalized(console, serial);
                BootMemoryMapAcceptanceResult memoryMapAcceptance =
                    BootMemoryMapAcceptance.Validate(s_memoryMapSummary);
                if (!memoryMapAcceptance.Succeeded)
                {
                    WriteBootMemoryMapRejected(console, serial, memoryMapAcceptance.Status);
                    return;
                }

                WriteBootMemoryMapAccepted(console, serial);
                if (!MaterializeAndWriteBootMemoryRegions(console, serial))
                {
                    return;
                }

                if (!ScanAndWriteAcpiRsdp(console, serial, systemTable))
                {
                    return;
                }

#if ACPI_PHYSICAL_DISCOVERY_PROBE
                if (!WriteAcpiPhysicalDiscoveryProbe(console, serial))
                {
                    return;
                }
#endif

#if EXIT_BOOT_SERVICES_FRESH_MAP_PROBE
                if (!WriteExitBootServicesFreshMapProbe(console, serial, systemTable))
                {
                    return;
                }
#endif
            }
        }
        else
        {
            WriteUefiMemoryMapFail(console, serial);
            if (s_memoryMapCopyStatus != UefiMemoryMapCopyPlanStatus.Success)
            {
                WriteUefiMemoryMapCopyFail(console, serial, s_memoryMapCopyStatus);
            }
        }

#if UEFI_GONE_SIMULATION_PROBE
        WriteUefiGoneSimulation(console, serial);
#endif
        WriteBootSequencePlan(console, serial);
    }

    private static void WriteEfiMainStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, 84);
        Write(console, serial, 69);
        Write(console, serial, 83);
        Write(console, serial, 84);
        Write(console, serial, 95);
        Write(console, serial, 83);
        Write(console, serial, 84);
        Write(console, serial, 65);
        Write(console, serial, 82);
        Write(console, serial, 84);
        Write(console, serial, 32);
        Write(console, serial, 101);
        Write(console, serial, 102);
        Write(console, serial, 105);
        Write(console, serial, 46);
        Write(console, serial, 109);
        Write(console, serial, 97);
        Write(console, serial, 105);
        Write(console, serial, 110);
        WriteCrLf(console, serial);
    }

    private static void WriteEfiConsoleReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, 69);
        Write(console, serial, 70);
        Write(console, serial, 73);
        Write(console, serial, 95);
        Write(console, serial, 67);
        Write(console, serial, 79);
        Write(console, serial, 78);
        Write(console, serial, 83);
        Write(console, serial, 79);
        Write(console, serial, 76);
        Write(console, serial, 69);
        Write(console, serial, 95);
        Write(console, serial, 82);
        Write(console, serial, 69);
        Write(console, serial, 65);
        Write(console, serial, 68);
        Write(console, serial, 89);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapFail(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapDescriptorCount(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong descriptorCount)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, descriptorCount);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapOwned(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'W');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapNormalized(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'Z');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryMapAccepted(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryMapRejected(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        BootMemoryMapAcceptanceStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'J');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static bool MaterializeAndWriteBootMemoryRegions(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        {
            s_memoryRegionMaterialization = BootMemoryRegionMaterializer.Materialize(
                s_memoryMapSnapshot.Buffer,
                s_memoryMapSnapshot.MapSize,
                s_memoryMapSnapshot.DescriptorSize,
                s_memoryMapSnapshot.DescriptorVersion,
                s_memoryMapSnapshot.DescriptorCount,
                (BootMemoryRegionRecord*)regionSlots,
                OwnedMemoryRegionCapacity);
        }

        if (!s_memoryRegionMaterialization.Succeeded)
        {
            WriteBootMemoryRegionsFail(
                console,
                serial,
                s_memoryRegionMaterialization.Status);
            return false;
        }

        WriteBootMemoryRegionsReady(console, serial);
        WriteBootMemoryRegionCount(console, serial, s_memoryRegionMaterialization.RegionCount);
        WriteBootPageAllocatorReadiness(console, serial);
        return true;
    }

    private static void WriteBootMemoryRegionsReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryRegionCount(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong regionCount)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, regionCount);
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryRegionsFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        BootMemoryRegionMaterializationStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootPageAllocatorReadiness(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        s_bootPageAllocatorReadiness = PlanBootPageAllocatorReadiness();
        if (s_bootPageAllocatorReadiness.IsReady)
        {
            WriteBootPageAllocatorReadyPrefix(console, serial);
            WriteReadyStatus(console, serial);
        }
        else
        {
            WriteBootPageAllocatorNotReadyPrefix(console, serial);
            if (s_bootPageAllocatorReadiness.Status == EfiBootPageAllocatorReadinessStatus.NoConventionalMemory)
            {
                WriteNoConventionalMemoryStatus(console, serial);
            }
            else
            {
                WriteLiveAllocatorNotInstalledStatus(console, serial);
            }
        }

        WriteBootPageAllocatorReadinessMetadata(console, serial, s_bootPageAllocatorReadiness);
    }

    private static EfiBootPageAllocatorReadinessResult PlanBootPageAllocatorReadiness()
    {
        EfiBootPageAllocatorReadinessResult result = default;
        result.Status = EfiBootPageAllocatorReadinessStatus.MaterializationMissing;
        result.RegionCount = s_memoryRegionMaterialization.RegionCount;
        result.ConventionalPages = s_memoryRegionMaterialization.ConventionalMemoryPages;
        result.ConventionalBytes = s_memoryRegionMaterialization.ConventionalMemoryBytes;
        result.PageSize = BootMemoryRegionRecord.PageSize;

        if (!s_memoryRegionMaterialization.Succeeded)
        {
            return result;
        }

        if (s_memoryRegionMaterialization.RegionCount == 0
            || s_memoryRegionMaterialization.RegionCount > OwnedMemoryRegionCapacity)
        {
            result.Status = EfiBootPageAllocatorReadinessStatus.InvalidRegionCount;
            return result;
        }

        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        fixed (ulong* nodeSlots = &s_bootPageAllocatorNodes.Slots[0])
        {
            BootMemoryRegionRecord* regions = (BootMemoryRegionRecord*)regionSlots;
            ulong previousStart = 0;
            ulong previousEndExclusive = 0;
            int nodeCount = 0;
            ulong freePages = 0;
            ulong freeBytes = 0;

            for (ulong regionIndex = 0; regionIndex < s_memoryRegionMaterialization.RegionCount; regionIndex++)
            {
                BootMemoryRegionRecord region = regions[(int)regionIndex];
                EfiBootPageAllocatorReadinessStatus regionStatus = ValidateBootAllocatorRegion(
                    region,
                    regionIndex,
                    previousStart,
                    previousEndExclusive,
                    out ulong byteLength,
                    out ulong physicalEndExclusive);
                if (regionStatus != EfiBootPageAllocatorReadinessStatus.Ready)
                {
                    result.Status = regionStatus;
                    result.FreeNodeCount = nodeCount;
                    result.FreePages = freePages;
                    result.FreeBytes = freeBytes;
                    return result;
                }

                if (region.UefiType == UefiConventionalMemoryType)
                {
                    if (nodeCount >= BootPageAllocatorNodeCapacity)
                    {
                        result.Status = EfiBootPageAllocatorReadinessStatus.NodeCapacityTooSmall;
                        result.FreeNodeCount = nodeCount;
                        result.FreePages = freePages;
                        result.FreeBytes = freeBytes;
                        return result;
                    }

                    if (nodeCount != 0)
                    {
                        SetBootPageAllocatorNodeNext(nodeSlots, nodeCount - 1, (ulong)nodeCount);
                    }

                    WriteBootPageAllocatorNodeUnchecked(
                        nodeSlots,
                        nodeCount,
                        regionIndex,
                        region.PhysicalStart,
                        region.PhysicalEndExclusive,
                        region.PageCount,
                        byteLength,
                        BootPageAllocatorInvalidNodeIndex);
                    nodeCount++;

                    if (!TryAdd(freePages, region.PageCount, out ulong nextFreePages)
                        || !TryAdd(freeBytes, byteLength, out ulong nextFreeBytes))
                    {
                        result.Status = EfiBootPageAllocatorReadinessStatus.TotalMismatch;
                        result.FreeNodeCount = nodeCount;
                        result.FreePages = freePages;
                        result.FreeBytes = freeBytes;
                        return result;
                    }

                    freePages = nextFreePages;
                    freeBytes = nextFreeBytes;
                }

                previousStart = region.PhysicalStart;
                previousEndExclusive = physicalEndExclusive;
            }

            result.FreeNodeCount = nodeCount;
            result.FreePages = freePages;
            result.FreeBytes = freeBytes;

            if (nodeCount == 0)
            {
                result.Status = EfiBootPageAllocatorReadinessStatus.NoConventionalMemory;
                return result;
            }

            if (freePages == 0)
            {
                result.Status = EfiBootPageAllocatorReadinessStatus.NoFreePages;
                return result;
            }

            result.Status = ValidateBootPageAllocatorNodeChain(nodeSlots, nodeCount, freePages, freeBytes);
            result.IsReady = result.Status == EfiBootPageAllocatorReadinessStatus.Ready;
            return result;
        }
    }

    private static EfiBootPageAllocatorReadinessStatus ValidateBootAllocatorRegion(
        BootMemoryRegionRecord region,
        ulong regionIndex,
        ulong previousStart,
        ulong previousEndExclusive,
        out ulong byteLength,
        out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;

        if (region.PageCount == 0
            || region.PageCount > 0xffffffffffffffff / BootMemoryRegionRecord.PageSize)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        byteLength = region.PageCount * BootMemoryRegionRecord.PageSize;
        if (region.ByteLength != byteLength)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        if (!TryAdd(region.PhysicalStart, byteLength, out physicalEndExclusive)
            || region.PhysicalEndExclusive != physicalEndExclusive)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        if (regionIndex != 0)
        {
            if (region.PhysicalStart < previousStart)
            {
                return EfiBootPageAllocatorReadinessStatus.UnsortedRegion;
            }

            if (region.PhysicalStart < previousEndExclusive)
            {
                return EfiBootPageAllocatorReadinessStatus.OverlappingRegion;
            }
        }

        if (region.UefiType == UefiConventionalMemoryType
            && (region.PhysicalStart & (BootMemoryRegionRecord.PageSize - 1)) != 0)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        return EfiBootPageAllocatorReadinessStatus.Ready;
    }

    private static EfiBootPageAllocatorReadinessStatus ValidateBootPageAllocatorNodeChain(
        ulong* nodeSlots,
        int nodeCount,
        ulong expectedFreePages,
        ulong expectedFreeBytes)
    {
        int currentNodeIndex = 0;
        int visitedNodeCount = 0;
        ulong totalPages = 0;
        ulong totalBytes = 0;

        while (currentNodeIndex != -1)
        {
            if (currentNodeIndex < 0 || currentNodeIndex >= nodeCount || visitedNodeCount >= nodeCount)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            ulong physicalStart = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodePhysicalStartSlot);
            ulong physicalEndExclusive = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodePhysicalEndExclusiveSlot);
            ulong pageCount = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodePageCountSlot);
            ulong byteLength = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodeByteLengthSlot);
            ulong nextNodeIndex = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodeNextIndexSlot);

            if (pageCount == 0
                || (physicalStart & (BootMemoryRegionRecord.PageSize - 1)) != 0
                || pageCount > 0xffffffffffffffff / BootMemoryRegionRecord.PageSize)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            ulong calculatedByteLength = pageCount * BootMemoryRegionRecord.PageSize;
            if (byteLength != calculatedByteLength)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            if (!TryAdd(physicalStart, byteLength, out ulong calculatedEnd)
                || physicalEndExclusive != calculatedEnd)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            if (!TryAdd(totalPages, pageCount, out ulong nextTotalPages)
                || !TryAdd(totalBytes, byteLength, out ulong nextTotalBytes))
            {
                return EfiBootPageAllocatorReadinessStatus.TotalMismatch;
            }

            totalPages = nextTotalPages;
            totalBytes = nextTotalBytes;
            visitedNodeCount++;

            if (nextNodeIndex == BootPageAllocatorInvalidNodeIndex)
            {
                currentNodeIndex = -1;
            }
            else if (nextNodeIndex >= (ulong)nodeCount || nextNodeIndex == (ulong)currentNodeIndex)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }
            else
            {
                currentNodeIndex = (int)nextNodeIndex;
            }
        }

        if (visitedNodeCount != nodeCount)
        {
            return EfiBootPageAllocatorReadinessStatus.NodeCountMismatch;
        }

        return totalPages == expectedFreePages && totalBytes == expectedFreeBytes
            ? EfiBootPageAllocatorReadinessStatus.Ready
            : EfiBootPageAllocatorReadinessStatus.TotalMismatch;
    }

    private static void WriteBootPageAllocatorNodeUnchecked(
        ulong* nodeSlots,
        int nodeIndex,
        ulong sourceRegionIndex,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        ulong nextNodeIndex)
    {
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeSourceRegionIndexSlot, sourceRegionIndex);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePhysicalStartSlot, physicalStart);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePhysicalEndExclusiveSlot, physicalEndExclusive);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePageCountSlot, pageCount);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeByteLengthSlot, byteLength);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeNextIndexSlot, nextNodeIndex);
    }

    private static void SetBootPageAllocatorNodeNext(ulong* nodeSlots, int nodeIndex, ulong nextNodeIndex)
    {
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeNextIndexSlot, nextNodeIndex);
    }

    private static void SetBootPageAllocatorNodeSlot(
        ulong* nodeSlots,
        int nodeIndex,
        int slotIndex,
        ulong value)
    {
        nodeSlots[(nodeIndex * BootPageAllocatorNodeSlotCount) + slotIndex] = value;
    }

    private static ulong GetBootPageAllocatorNodeSlot(ulong* nodeSlots, int nodeIndex, int slotIndex)
    {
        return nodeSlots[(nodeIndex * BootPageAllocatorNodeSlotCount) + slotIndex];
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > 0xffffffffffffffff - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }

    private static void WriteBootPageAllocatorReadinessMetadata(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootPageAllocatorReadinessResult readiness)
    {
        Write(console, serial, (byte)' ');
        WriteRegionsField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.RegionCount);
        Write(console, serial, (byte)' ');
        WriteConventionalPagesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.ConventionalPages);
        Write(console, serial, (byte)' ');
        WriteConventionalBytesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.ConventionalBytes);
        Write(console, serial, (byte)' ');
        WriteFreeNodesField(console, serial);
        WriteUnsignedDecimal(console, serial, (ulong)readiness.FreeNodeCount);
        Write(console, serial, (byte)' ');
        WriteFreePagesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.FreePages);
        Write(console, serial, (byte)' ');
        WriteFreeBytesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.FreeBytes);
        Write(console, serial, (byte)' ');
        WritePageSizeField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.PageSize);
        Write(console, serial, (byte)' ');
        WriteStatusCodeField(console, serial);
        WriteUnsignedDecimal(console, serial, (ulong)readiness.Status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootPageAllocatorReadyPrefix(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)' ');
        WriteStatusField(console, serial);
    }

    private static void WriteReadyStatus(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'y');
    }

    private static void WriteBootPageAllocatorNotReadyPrefix(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)' ');
        WriteStatusField(console, serial);
    }

    private static void WriteStatusField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'u');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteStatusCodeField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'u');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'=');
    }

    private static void WriteLiveAllocatorNotInstalledStatus(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'d');
    }

    private static void WriteNoConventionalMemoryStatus(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'m');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'y');
    }

    private static void WriteRegionsField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteConventionalPagesField(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteConventionalBytesField(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'y');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreeNodesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreePagesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreeBytesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'y');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WritePageSizeField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'p');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'z');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'=');
    }

    private static bool ScanAndWriteAcpiRsdp(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        if (systemTable == null)
        {
            s_acpiRsdpScanResult = EfiConfigurationTableScanResult.Fail(
                EfiConfigurationTableScanStatus.InvalidConfigurationTableAddress);
            s_bootHandoff.ClearAcpiRsdp((uint)s_acpiRsdpScanResult.Status);
            WriteUefiAcpiRsdpInvalid(console, serial, s_acpiRsdpScanResult.Status);
            return true;
        }

        s_acpiRsdpScanResult = EfiConfigurationTableScanner.Scan(
            systemTable->ConfigurationTable,
            systemTable->NumberOfTableEntries);

        if (s_acpiRsdpScanResult.Succeeded)
        {
            s_bootHandoff.SetAcpiRsdp(
                s_acpiRsdpScanResult.RsdpAddress,
                (uint)s_acpiRsdpScanResult.TableKind,
                (uint)s_acpiRsdpScanResult.Status);

            EfiBootHandoffStatus handoffStatus = EfiBootHandoffValidator.Validate(s_bootHandoff);
            if (handoffStatus != EfiBootHandoffStatus.Success)
            {
                WriteBootHandoffFail(console, serial, handoffStatus);
                return false;
            }

            WriteBootAcpiRsdpHandoffReady(console, serial);
            WriteUefiAcpiRsdpReady(console, serial);
            return true;
        }

        s_bootHandoff.ClearAcpiRsdp((uint)s_acpiRsdpScanResult.Status);
        if (s_acpiRsdpScanResult.Status == EfiConfigurationTableScanStatus.NotFound)
        {
            WriteUefiAcpiRsdpMissing(console, serial);
            return true;
        }

        WriteUefiAcpiRsdpInvalid(console, serial, s_acpiRsdpScanResult.Status);
        return true;
    }

    private static void WriteBootAcpiRsdpHandoffReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpMissing(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'G');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpInvalid(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiConfigurationTableScanStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

#if ACPI_PHYSICAL_DISCOVERY_PROBE
    private static bool WriteAcpiPhysicalDiscoveryProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        if (!TryReadAndValidateAcpiRsdp(
            console,
            serial,
            out ulong rsdtAddress,
            out ulong xsdtAddress,
            out bool isAcpi20,
            out ulong rsdpStatus))
        {
            WriteAcpiDiscoveryFail(console, serial, 1, rsdpStatus);
            return false;
        }

        WriteAcpiRsdpValidated(console, serial);

        if (!TryReadAndValidateRootSdt(
            console,
            serial,
            rsdtAddress,
            xsdtAddress,
            isAcpi20,
            out int rootTableLength,
            out int rootPointerCount,
            out ulong rootStatus))
        {
            WriteAcpiDiscoveryFail(console, serial, 2, rootStatus);
            return false;
        }

        WriteAcpiRootSdtValidated(console, serial);

        if (!TryDiscoverMcfg(rootTableLength, rootPointerCount, out ulong mcfgStatus))
        {
            WriteAcpiDiscoveryFail(console, serial, 3, mcfgStatus);
            return false;
        }

        WriteAcpiMcfgDiscoveryReady(console, serial);
        return true;
    }

    private static bool TryReadAndValidateAcpiRsdp(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        out ulong rsdtAddress,
        out ulong xsdtAddress,
        out bool isAcpi20,
        out ulong status)
    {
        rsdtAddress = 0;
        xsdtAddress = 0;
        isAcpi20 = false;
        status = 0;

        if (!s_acpiRsdpScanResult.Succeeded || s_acpiRsdpScanResult.RsdpAddress == 0)
        {
            status = 10 + (ulong)s_acpiRsdpScanResult.Status;
            return false;
        }

        fixed (byte* rsdp = &s_acpiRsdpScratch.Bytes[0])
        {
            if (!TryReadAcpiPhysicalBytes(
                s_acpiRsdpScanResult.RsdpAddress,
                20,
                rsdp,
                AcpiRsdpScratchSize))
            {
                status = 20;
                return false;
            }

            WriteAcpiRsdpSnapshotReady(console, serial);

            if (!HasRsdpSignature(rsdp))
            {
                status = 21;
                return false;
            }

            if (HasZeroOemId(rsdp))
            {
                status = 22;
                return false;
            }

            if (!IsChecksumValid(rsdp, 20))
            {
                status = 23;
                return false;
            }

            byte revision = rsdp[15];
            if (revision == 0)
            {
                rsdtAddress = ReadUInt32LittleEndian(rsdp + 16);
                return true;
            }

            if (revision == 1)
            {
                status = 24;
                return false;
            }

            if (!TryReadAcpiPhysicalBytes(
                s_acpiRsdpScanResult.RsdpAddress,
                36,
                rsdp,
                AcpiRsdpScratchSize))
            {
                status = 25;
                return false;
            }

            uint declaredLength = ReadUInt32LittleEndian(rsdp + 20);
            if (declaredLength < 36 || declaredLength > AcpiRsdpScratchSize)
            {
                status = 26;
                return false;
            }

            if (!TryReadAcpiPhysicalBytes(
                s_acpiRsdpScanResult.RsdpAddress,
                (int)declaredLength,
                rsdp,
                AcpiRsdpScratchSize))
            {
                status = 27;
                return false;
            }

            if (!IsChecksumValid(rsdp, (int)declaredLength))
            {
                status = 28;
                return false;
            }

            rsdtAddress = ReadUInt32LittleEndian(rsdp + 16);
            xsdtAddress = ReadUInt64LittleEndian(rsdp + 24);
            isAcpi20 = true;
            return true;
        }
    }

    private static bool TryReadAndValidateRootSdt(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong rsdtAddress,
        ulong xsdtAddress,
        bool isAcpi20,
        out int rootTableLength,
        out int rootPointerCount,
        out ulong status)
    {
        rootTableLength = 0;
        rootPointerCount = 0;
        status = 0;

        bool useXsdt = isAcpi20 && xsdtAddress != 0;
        ulong rootAddress = useXsdt ? xsdtAddress : rsdtAddress;
        if (rootAddress == 0)
        {
            status = 30;
            return false;
        }

        fixed (byte* rootTable = &s_acpiRootSdt.Bytes[0])
        fixed (ulong* pointerScratch = &s_acpiPointerScratch.Slots[0])
        {
            if (!TryReadAcpiPhysicalBytes(
                rootAddress,
                AcpiSdtHeaderLength,
                rootTable,
                AcpiRootSdtBufferSize))
            {
                status = 31;
                return false;
            }

            uint signature = ReadUInt32LittleEndian(rootTable);
            uint expectedSignature = useXsdt ? AcpiXsdtSignature : AcpiRsdtSignature;
            if (signature != expectedSignature)
            {
                status = 32;
                return false;
            }

            uint declaredLength = ReadUInt32LittleEndian(rootTable + 4);
            if (declaredLength < AcpiSdtHeaderLength || declaredLength > AcpiRootSdtBufferSize)
            {
                status = 33;
                return false;
            }

            rootTableLength = (int)declaredLength;
            if (!TryReadAcpiPhysicalBytes(
                rootAddress,
                rootTableLength,
                rootTable,
                AcpiRootSdtBufferSize))
            {
                status = 34;
                return false;
            }

            WriteAcpiRootSdtSnapshotReady(console, serial);

            if (!IsChecksumValid(rootTable, rootTableLength))
            {
                status = 35;
                return false;
            }

            int pointerSize = useXsdt ? 8 : 4;
            int pointerBytes = rootTableLength - AcpiSdtHeaderLength;
            if ((pointerBytes % pointerSize) != 0)
            {
                status = 36;
                return false;
            }

            rootPointerCount = pointerBytes / pointerSize;
            if (rootPointerCount == 0 || rootPointerCount > AcpiPointerScratchCapacity)
            {
                status = 37;
                return false;
            }

            for (int index = 0; index < rootPointerCount; index++)
            {
                byte* pointerSource = rootTable + AcpiSdtHeaderLength + (index * pointerSize);
                ulong tableAddress = pointerSize == 8
                    ? ReadUInt64LittleEndian(pointerSource)
                    : ReadUInt32LittleEndian(pointerSource);
                if (tableAddress == 0)
                {
                    status = 38;
                    return false;
                }

                pointerScratch[index] = tableAddress;
            }

            return true;
        }
    }

    private static bool TryDiscoverMcfg(
        int rootTableLength,
        int rootPointerCount,
        out ulong status)
    {
        status = 0;
        if (rootTableLength < AcpiSdtHeaderLength || rootPointerCount <= 0)
        {
            status = 40;
            return false;
        }

        fixed (ulong* pointerScratch = &s_acpiPointerScratch.Slots[0])
        fixed (byte* childHeader = &s_acpiChildHeaderScratch.Bytes[0])
        fixed (byte* childTable = &s_acpiChildTable.Bytes[0])
        fixed (ulong* segmentScratch = &s_acpiEcamSegmentScratch.Slots[0])
        {
            for (int pointerIndex = 0; pointerIndex < rootPointerCount; pointerIndex++)
            {
                ulong tableAddress = pointerScratch[pointerIndex];
                if (!TryReadAcpiPhysicalBytes(
                    tableAddress,
                    AcpiChildHeaderScratchSize,
                    childHeader,
                    AcpiChildHeaderScratchSize))
                {
                    status = 41;
                    return false;
                }

                uint signature = ReadUInt32LittleEndian(childHeader);
                if (signature != AcpiMcfgSignature)
                {
                    continue;
                }

                uint declaredLength = ReadUInt32LittleEndian(childHeader + 4);
                if (declaredLength < AcpiSdtHeaderLength || declaredLength > AcpiChildTableBufferSize)
                {
                    status = 42;
                    return false;
                }

                int tableLength = (int)declaredLength;
                if (!TryReadAcpiPhysicalBytes(
                    tableAddress,
                    tableLength,
                    childTable,
                    AcpiChildTableBufferSize))
                {
                    status = 43;
                    return false;
                }

                if (!IsChecksumValid(childTable, tableLength))
                {
                    status = 44;
                    return false;
                }

                if (!TryParseMcfgSegments(childTable, tableLength, segmentScratch, out status))
                {
                    return false;
                }

                return true;
            }
        }

        status = 45;
        return false;
    }

    private static bool TryParseMcfgSegments(
        byte* table,
        int tableLength,
        ulong* segmentScratch,
        out ulong status)
    {
        status = 0;
        if (tableLength < AcpiMcfgAllocationOffset + AcpiMcfgAllocationLength)
        {
            status = 50;
            return false;
        }

        for (int index = 0; index < 8; index++)
        {
            if (table[AcpiSdtHeaderLength + index] != 0)
            {
                status = 51;
                return false;
            }
        }

        int allocationBytes = tableLength - AcpiMcfgAllocationOffset;
        if ((allocationBytes % AcpiMcfgAllocationLength) != 0)
        {
            status = 52;
            return false;
        }

        int segmentCount = allocationBytes / AcpiMcfgAllocationLength;
        if (segmentCount == 0 || segmentCount > AcpiEcamSegmentScratchCapacity)
        {
            status = 53;
            return false;
        }

        for (int index = 0; index < segmentCount; index++)
        {
            byte* entry = table + AcpiMcfgAllocationOffset + (index * AcpiMcfgAllocationLength);
            byte startBus = entry[10];
            byte endBus = entry[11];
            if (startBus > endBus)
            {
                status = 54;
                return false;
            }

            int slot = index * 4;
            segmentScratch[slot] = ReadUInt64LittleEndian(entry);
            segmentScratch[slot + 1] = ReadUInt16LittleEndian(entry + 8);
            segmentScratch[slot + 2] = startBus;
            segmentScratch[slot + 3] = endBus;
        }

        return true;
    }

    private static bool TryReadAcpiPhysicalBytes(
        ulong physicalAddress,
        int byteCount,
        byte* destination,
        int destinationCapacity)
    {
        if (physicalAddress == 0 || byteCount <= 0 || destination == null)
        {
            return false;
        }

        if (destinationCapacity < byteCount)
        {
            return false;
        }

        ulong byteLength = (ulong)byteCount;
        if (physicalAddress > 0xffffffffffffffff - byteLength)
        {
            return false;
        }

        if (!IsAcpiPhysicalRangeAllowed(physicalAddress, byteLength))
        {
            return false;
        }

        byte* source = (byte*)physicalAddress;
        for (int index = 0; index < byteCount; index++)
        {
            destination[index] = source[index];
        }

        return true;
    }

    private static bool IsAcpiPhysicalRangeAllowed(ulong physicalAddress, ulong byteLength)
    {
        if (!s_memoryRegionMaterialization.Succeeded || byteLength == 0)
        {
            return false;
        }

        ulong endExclusive = physicalAddress + byteLength;
        if (endExclusive <= physicalAddress)
        {
            return false;
        }

        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        {
            BootMemoryRegionRecord* regions = (BootMemoryRegionRecord*)regionSlots;
            for (ulong regionIndex = 0; regionIndex < s_memoryRegionMaterialization.RegionCount; regionIndex++)
            {
                BootMemoryRegionRecord region = regions[(int)regionIndex];
                if ((region.UefiType == UefiAcpiReclaimMemoryType || region.UefiType == UefiAcpiMemoryNvsType)
                    && physicalAddress >= region.PhysicalStart
                    && endExclusive <= region.PhysicalEndExclusive)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasRsdpSignature(byte* source)
    {
        return source[0] == (byte)'R'
            && source[1] == (byte)'S'
            && source[2] == (byte)'D'
            && source[3] == (byte)' '
            && source[4] == (byte)'P'
            && source[5] == (byte)'T'
            && source[6] == (byte)'R'
            && source[7] == (byte)' ';
    }

    private static bool HasZeroOemId(byte* source)
    {
        for (int index = 0; index < 6; index++)
        {
            if (source[9 + index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsChecksumValid(byte* source, int byteCount)
    {
        uint sum = 0;
        for (int index = 0; index < byteCount; index++)
        {
            sum += source[index];
        }

        return (sum & 0xff) == 0;
    }

    private static ushort ReadUInt16LittleEndian(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static uint ReadUInt32LittleEndian(byte* source)
    {
        return source[0]
            | ((uint)source[1] << 8)
            | ((uint)source[2] << 16)
            | ((uint)source[3] << 24);
    }

    private static ulong ReadUInt64LittleEndian(byte* source)
    {
        return (ulong)source[0]
            | ((ulong)source[1] << 8)
            | ((ulong)source[2] << 16)
            | ((ulong)source[3] << 24)
            | ((ulong)source[4] << 32)
            | ((ulong)source[5] << 40)
            | ((ulong)source[6] << 48)
            | ((ulong)source[7] << 56);
    }

    private static void WriteAcpiRsdpSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRsdp(console, serial);
        WriteUnderscore(console, serial);
        WriteSnapshotReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRsdpValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRsdp(console, serial);
        WriteUnderscore(console, serial);
        WriteValidated(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRootSdtSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRootSdt(console, serial);
        WriteUnderscore(console, serial);
        WriteSnapshotReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRootSdtValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRootSdt(console, serial);
        WriteUnderscore(console, serial);
        WriteValidated(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiMcfgDiscoveryReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'G');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        WriteUnderscore(console, serial);
        WriteReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiDiscoveryFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong stage,
        ulong status)
    {
        WriteAcpiPrefix(console, serial);
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, stage);
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, status);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiPrefix(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        WriteUnderscore(console, serial);
    }

    private static void WriteRsdp(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
    }

    private static void WriteRootSdt(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'T');
    }

    private static void WriteSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        WriteUnderscore(console, serial);
        WriteReady(console, serial);
    }

    private static void WriteValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
    }

    private static void WriteReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
    }

    private static void WriteUnderscore(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'_');
    }
#endif

    private static bool WriteExitBootServicesFreshMapProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteUefiEbsFreshMapProbeStart(console, serial);

        if (!TryCaptureUefiMemoryMap(systemTable))
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)s_memoryMapCopyStatus);
            return false;
        }

        EfiMemoryMapNormalizeResult normalizeResult = EfiMemoryMapNormalizer.Normalize(
            s_memoryMapSnapshot.Buffer,
            s_memoryMapSnapshot.MapSize,
            s_memoryMapSnapshot.DescriptorSize,
            s_memoryMapSnapshot.DescriptorVersion,
            s_memoryMapSnapshot.DescriptorCount);
        if (!normalizeResult.Succeeded)
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)normalizeResult.Status);
            return false;
        }

        UefiExitBootServicesPlanResult planResult = UefiExitBootServicesPlan.Evaluate(
            s_bootHandoff,
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.ExitCallSkipped,
            attemptIndex: 0,
            maximumRetryCount: UefiExitBootServicesPlan.DefaultMaximumRetryCount);
        if (planResult.Status != UefiExitBootServicesPlanStatus.ExitCallSkipped)
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)planResult.Status);
            return false;
        }

        WriteUefiEbsFreshMapReady(console, serial);
        WriteUefiEbsCallSkipped(console, serial);
        return true;
    }

    private static void WriteUefiEbsFreshMapProbeStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'T');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsFreshMapReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsFreshMapFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, status);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsCallSkipped(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'K');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapNormalizeFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiMemoryMapNormalizeStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'Z');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootHandoffReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteBootHandoffFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootHandoffStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapCopyFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        UefiMemoryMapCopyPlanStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootSequencePlan(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            int length = BootSequencePlan.GetLineLength(lineIndex);
            for (int offset = 0; offset < length; offset++)
            {
                Write(console, serial, BootSequencePlan.GetLineByte(lineIndex, offset));
            }

            WriteCrLf(console, serial);
        }
    }

#if UEFI_GONE_SIMULATION_PROBE
    private static void WriteUefiGoneSimulation(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteUefiGoneSimulationStart(console, serial);
        WriteUefiGoneSimulationPlatformDenied(console, serial);
        WritePostFirmwareDiagnosticsReady(console, serial);
        WriteUefiGoneSimulationPass(console, serial);
    }

    private static void WriteUefiGoneSimulationStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'T');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiGoneSimulationPlatformDenied(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WritePostFirmwareDiagnosticsReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'W');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiGoneSimulationPass(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'S');
        WriteCrLf(console, serial);
    }
#endif

    private static bool TryCaptureUefiMemoryMap(EfiSystemTable* systemTable)
    {
        s_memoryMapCopyStatus = UefiMemoryMapCopyPlanStatus.Success;

        if (systemTable == null || systemTable->BootServices == null)
        {
            return false;
        }

        EfiBootServices* bootServices = systemTable->BootServices;
        if (bootServices->GetMemoryMap == null || bootServices->AllocatePool == null)
        {
            return false;
        }

        UefiMemoryMapCaptureResult captureResult;
        fixed (byte* ownedMemoryMapBytes = &s_ownedMemoryMap.Bytes[0])
        {
            captureResult = UefiMemoryMapCapture.Capture(
                bootServices->GetMemoryMap,
                bootServices->AllocatePool,
                ownedMemoryMapBytes,
                OwnedMemoryMapBufferSize);
        }

        if (!captureResult.Succeeded)
        {
            s_memoryMapCopyStatus = captureResult.CopyPlanStatus;
            return false;
        }

        FirmwareMemoryMapRecord sourceRecord = captureResult.SourceRecord;
        FirmwareMemoryMapRecord ownedRecord = captureResult.OwnedRecord;
        s_memoryMapSnapshot.Buffer = (void*)ownedRecord.BufferAddress;
        s_memoryMapSnapshot.BufferSize = ownedRecord.BufferSize;
        s_memoryMapSnapshot.SourceBuffer = (void*)sourceRecord.BufferAddress;
        s_memoryMapSnapshot.SourceBufferSize = sourceRecord.BufferSize;
        s_memoryMapSnapshot.MapSize = ownedRecord.MapSize;
        s_memoryMapSnapshot.MapKey = ownedRecord.MapKey;
        s_memoryMapSnapshot.DescriptorSize = ownedRecord.DescriptorSize;
        s_memoryMapSnapshot.DescriptorVersion = ownedRecord.DescriptorVersion;
        s_memoryMapSnapshot.DescriptorCount = ownedRecord.DescriptorCount;
        s_memoryMapSnapshot.IsOwned = true;
        s_bootHandoff = BootHandoffRecord.CreateUefiWithOwnedMemoryMap(
            sourceRecord.BufferAddress,
            sourceRecord.BufferSize,
            ownedRecord.BufferAddress,
            ownedRecord.BufferSize,
            ownedRecord.MapSize,
            ownedRecord.MapKey,
            ownedRecord.DescriptorSize,
            ownedRecord.DescriptorVersion,
            s_memoryMapSnapshot.DescriptorCount);

        return s_memoryMapSnapshot.DescriptorCount != 0;
    }

    private static void WriteCrLf(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        EfiConsoleAdapter adapter = new EfiConsoleAdapter(console, serial);
        adapter.WriteCrLf();
    }

    private static void Write(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, byte value)
    {
        EfiConsoleAdapter adapter = new EfiConsoleAdapter(console, serial);
        adapter.WriteByte(value);
    }

    private static void WriteUnsignedDecimal(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong value)
    {
        if (value >= 10)
        {
            WriteUnsignedDecimal(console, serial, value / 10);
        }

        Write(console, serial, (byte)((value % 10) + (byte)'0'));
    }
}

internal struct EfiTableHeader
{
    public ulong Signature;
    public uint Revision;
    public uint HeaderSize;
    public uint Crc32;
    public uint Reserved;
}

internal unsafe struct EfiSimpleTextOutputProtocol
{
    public void* Reset;
    public delegate* unmanaged<EfiSimpleTextOutputProtocol*, ushort*, ulong> OutputString;
}

internal unsafe struct EfiBootServices
{
    public EfiTableHeader Hdr;
    public void* RaiseTpl;
    public void* RestoreTpl;
    public void* AllocatePages;
    public void* FreePages;
    public delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> GetMemoryMap;
    public delegate* unmanaged<uint, ulong, void**, ulong> AllocatePool;
    public void* FreePool;
    public void* CreateEvent;
    public void* SetTimer;
    public void* WaitForEvent;
    public void* SignalEvent;
    public void* CloseEvent;
    public void* CheckEvent;
    public void* InstallProtocolInterface;
    public void* ReinstallProtocolInterface;
    public void* UninstallProtocolInterface;
    public void* HandleProtocol;
    public void* Reserved;
    public void* RegisterProtocolNotify;
    public void* LocateHandle;
    public void* LocateDevicePath;
    public void* InstallConfigurationTable;
    public void* LoadImage;
    public void* StartImage;
    public void* Exit;
    public void* UnloadImage;
    public void* ExitBootServices;
    public void* GetNextMonotonicCount;
    public void* Stall;
    public void* SetWatchdogTimer;
    public void* ConnectController;
    public void* DisconnectController;
    public void* OpenProtocol;
    public void* CloseProtocol;
    public void* OpenProtocolInformation;
    public void* ProtocolsPerHandle;
    public void* LocateHandleBuffer;
    public delegate* unmanaged<EfiGuid*, void*, void**, ulong> LocateProtocol;
}

internal unsafe struct EfiSystemTable
{
    public EfiTableHeader Hdr;
    public ushort* FirmwareVendor;
    public uint FirmwareRevision;
    public void* ConsoleInHandle;
    public void* ConIn;
    public void* ConsoleOutHandle;
    public EfiSimpleTextOutputProtocol* ConOut;
    public void* StandardErrorHandle;
    public EfiSimpleTextOutputProtocol* StdErr;
    public void* RuntimeServices;
    public EfiBootServices* BootServices;
    public ulong NumberOfTableEntries;
    public void* ConfigurationTable;
}

internal unsafe struct EfiMemoryMapSnapshot
{
    public void* Buffer;
    public ulong BufferSize;
    public void* SourceBuffer;
    public ulong SourceBufferSize;
    public ulong MapSize;
    public ulong MapKey;
    public ulong DescriptorSize;
    public uint DescriptorVersion;
    public ulong DescriptorCount;
    public bool IsOwned;
}

internal unsafe struct OwnedMemoryMapBuffer
{
    public fixed byte Bytes[131072];
}

internal unsafe struct OwnedMemoryRegionBuffer
{
    public fixed ulong Slots[32768];
}

internal unsafe struct BootPageAllocatorNodeBuffer
{
    public fixed ulong Slots[24576];
}

internal enum EfiBootPageAllocatorReadinessStatus
{
    Ready = 0,
    MaterializationMissing = 1,
    InvalidRegionCount = 2,
    InvalidRegionShape = 3,
    UnsortedRegion = 4,
    OverlappingRegion = 5,
    NoConventionalMemory = 6,
    NoFreePages = 7,
    NodeCapacityTooSmall = 8,
    NodeValidationFailed = 9,
    NodeCountMismatch = 10,
    TotalMismatch = 11
}

internal struct EfiBootPageAllocatorReadinessResult
{
    public EfiBootPageAllocatorReadinessStatus Status;
    public bool IsReady;
    public ulong RegionCount;
    public ulong ConventionalPages;
    public ulong ConventionalBytes;
    public int FreeNodeCount;
    public ulong FreePages;
    public ulong FreeBytes;
    public ulong PageSize;
}

#if ACPI_PHYSICAL_DISCOVERY_PROBE
internal unsafe struct AcpiRsdpScratchBuffer
{
    public fixed byte Bytes[4096];
}

internal unsafe struct AcpiRootSdtBuffer
{
    public fixed byte Bytes[65536];
}

internal unsafe struct AcpiChildHeaderScratchBuffer
{
    public fixed byte Bytes[36];
}

internal unsafe struct AcpiChildTableBuffer
{
    public fixed byte Bytes[65536];
}

internal unsafe struct AcpiPointerScratchBuffer
{
    public fixed ulong Slots[256];
}

internal unsafe struct AcpiEcamSegmentScratchBuffer
{
    public fixed ulong Slots[64];
}
#endif

internal struct EfiGuid
{
    public uint Data1;
    public ushort Data2;
    public ushort Data3;
    public byte Data4_0;
    public byte Data4_1;
    public byte Data4_2;
    public byte Data4_3;
    public byte Data4_4;
    public byte Data4_5;
    public byte Data4_6;
    public byte Data4_7;
}

internal unsafe struct EfiSerialIoProtocol
{
    public uint Revision;
    public void* Reset;
    public void* SetAttributes;
    public void* SetControl;
    public void* GetControl;
    public delegate* unmanaged<EfiSerialIoProtocol*, ulong*, void*, ulong> Write;
    public void* Read;
    public void* Mode;
}

internal struct EfiConsoleCharacter
{
    public ushort Value;
    public ushort NullTerminator;
}

#pragma warning restore 649
