using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;

internal static unsafe class EFIBUFFERTOOSMALLDescriptorGrowthSlackOverflowTests
{
    private const ulong TooSmall = 0x8000000000000005;
    private const ulong DeviceError = 0x8000000000000007;
    private const ulong OutOfResources = 0x8000000000000009;
    private const int Guard = 16;
    private const byte Untouched = 0xD7;
    private static Scenario s_scenario;
    private static byte* s_firmware;
    private static byte* s_payload;
    private static ulong s_capacity;
    private static ulong s_expectedAllocation;
    private static ulong s_requestedAllocation;
    private static ulong s_recaptureCapacity;
    private static ulong s_written;
    private static int s_events;
    private static int s_maps;
    private static int s_allocations;
    private static bool s_callbackMismatch;

    private struct Scenario
    {
        internal ulong InitialSize;
        internal ulong InitialStride;
        internal ulong FinalSize;
        internal ulong FinalStride;
        internal uint Version;
        internal ulong Slack;
        internal ulong QueryStatus;
        internal ulong AllocationStatus;
        internal ulong FinalStatus;
        internal bool NullAllocation;
        internal bool MissingMap;
        internal bool MissingAllocation;
        internal bool MissingDestination;
        internal bool DefaultSlack;
    }

    private static Scenario Normal() => new()
    {
        InitialSize = 96,
        InitialStride = 48,
        FinalSize = 144,
        FinalStride = 48,
        Version = 1,
        Slack = 8,
        QueryStatus = TooSmall
    };

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m03.001");
        int failures = 0;
        Run("m03.001.within_slack", TestWithinSlack, ref failures);
        Run("m03.001.stride_and_slack", TestStrideAndSlack, ref failures);
        Run("m03.001.slack_exhaustion", TestSlackExhaustion, ref failures);
        Run("m03.001.firmware_failures", TestFirmwareFailures, ref failures);
        Run("m03.001.preflight_precedence", TestPreflightPrecedence, ref failures);
        Run("m03.001.metadata_rejection", TestMetadataRejection, ref failures);
        Run("m03.001.descriptor_limits", TestDescriptorLimits, ref failures);
        Run("m03.001.arithmetic_edges", TestArithmeticEdges, ref failures);
        Run("m03.001.copyplan_scalars", TestCopyPlanScalars, ref failures);
        Run("m03.001.zero_allocation", TestZeroAllocation, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m03.001 assertion_failed");
            throw new InvalidOperationException("M03-001 host regressions failed.");
        }
        Console.WriteLine("TEST_PASS m03.001");
    }

    private static void Run(string name, Action test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);
        try
        {
            test();
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + exception.GetType().Name);
            Console.Error.WriteLine(exception.Message);
        }
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private static void TestWithinSlack()
    {
        Scenario s = Normal();
        s.DefaultSlack = true;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.Success, count: 3);
        s.FinalSize = 480;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.Success, count: 10);
    }

    private static void TestStrideAndSlack()
    {
        Scenario s = Normal();
        s.InitialSize = 80;
        s.InitialStride = 40;
        s.FinalSize = 120;
        s.FinalStride = 40;
        Check(s, 400, 512, UefiMemoryMapCaptureStatus.Success, count: 3);
        s.InitialSize = 128;
        s.InitialStride = 64;
        s.FinalSize = 192;
        s.FinalStride = 64;
        Check(s, 640, 640, UefiMemoryMapCaptureStatus.Success, count: 3, firmwareCapacity: 640);
        s = Normal();
        s.FinalSize = 192;
        s.FinalStride = 64;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.Success, count: 3);
        s = Normal();
        s.Slack = 1;
        Check(s, 144, 144, UefiMemoryMapCaptureStatus.Success, count: 3);
        s.FinalSize = 192;
        s.FinalStatus = TooSmall;
        Check(s, 144, 512, UefiMemoryMapCaptureStatus.FinalGetMemoryMapFailed, firmware: TooSmall, required: 192);
    }

    private static void TestSlackExhaustion()
    {
        Scenario s = Normal();
        s.FinalSize = 528;
        s.FinalStatus = TooSmall;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.FinalGetMemoryMapFailed, firmware: TooSmall, required: 528);
        // Even a huge reported requirement cannot make this callback write or retry.
        s.FinalSize = ulong.MaxValue;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.FinalGetMemoryMapFailed, firmware: TooSmall, required: ulong.MaxValue);
    }

    private static void TestFirmwareFailures()
    {
        foreach (ulong status in new ulong[] { 0, DeviceError })
        {
            Scenario s = Normal();
            s.QueryStatus = status;
            Check(s, 0, 512, UefiMemoryMapCaptureStatus.InitialGetMemoryMapFailed, firmware: status, required: 96, events: 1);
        }
        Scenario allocation = Normal();
        allocation.AllocationStatus = OutOfResources;
        Check(allocation, 480, 512, UefiMemoryMapCaptureStatus.AllocatePoolFailed, firmware: OutOfResources, required: 96, events: 0x12);
        allocation.AllocationStatus = 0;
        allocation.NullAllocation = true;
        Check(allocation, 480, 512, UefiMemoryMapCaptureStatus.AllocatePoolFailed, required: 96, events: 0x12);
        Scenario final = Normal();
        final.FinalStatus = DeviceError;
        final.FinalSize = 192;
        Check(final, 480, 512, UefiMemoryMapCaptureStatus.FinalGetMemoryMapFailed, firmware: DeviceError, required: 192);
    }

    private static void TestPreflightPrecedence()
    {
        Scenario s = Normal();
        s.MissingMap = s.MissingAllocation = s.MissingDestination = true;
        Check(s, 0, 0, UefiMemoryMapCaptureStatus.MissingGetMemoryMap, events: 0);
        s.MissingMap = false;
        Check(s, 0, 0, UefiMemoryMapCaptureStatus.MissingAllocatePool, events: 0);
        s.MissingAllocation = false;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.MissingOwnedDestination, UefiMemoryMapCopyPlanStatus.DestinationTooSmall, events: 0);
        s.MissingDestination = false;
        Check(s, 0, 0, UefiMemoryMapCaptureStatus.MissingOwnedDestination, UefiMemoryMapCopyPlanStatus.DestinationTooSmall, events: 0);
        s = Normal();
        s.QueryStatus = DeviceError;
        s.InitialSize = s.InitialStride = s.Slack = 0;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.InitialGetMemoryMapFailed, firmware: DeviceError, events: 1);
        s.QueryStatus = TooSmall;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.InvalidInitialMetadata, events: 1);
        s = Normal();
        s.InitialSize = 0;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.InvalidInitialMetadata, events: 1);
        s = Normal();
        s.InitialStride = 0;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.InvalidInitialMetadata, required: 96, events: 1);
        s = Normal();
        s.Slack = 0;
        s.InitialSize = ulong.MaxValue;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.InvalidInitialMetadata, required: ulong.MaxValue, events: 1);
    }

    private static void TestMetadataRejection()
    {
        Scenario s = Normal();
        s.FinalSize = 0;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.InvalidCapturedMetadata);
        s = Normal();
        s.FinalStride = 0;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.InvalidCapturedMetadata, required: 144);
        s = Normal();
        s.Version = 0;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.InvalidCapturedMetadata, required: 144);
        s.FinalStatus = DeviceError;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.FinalGetMemoryMapFailed, firmware: DeviceError, required: 144);
        s = Normal();
        s.FinalSize = 481; // Beyond the real allocation and not stride-aligned.
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.CopyPlanRejected, UefiMemoryMapCopyPlanStatus.InvalidMapSize);
        s.FinalSize = 95;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.CopyPlanRejected, UefiMemoryMapCopyPlanStatus.MapSizeDescriptorMismatch);
        s.FinalSize = 78;
        s.FinalStride = 39;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.CopyPlanRejected, UefiMemoryMapCopyPlanStatus.InvalidDescriptorSize);
        s = Normal();
        Check(s, 480, 143, UefiMemoryMapCaptureStatus.CopyPlanRejected, UefiMemoryMapCopyPlanStatus.DestinationTooSmall);
        Check(s, 480, 144, UefiMemoryMapCaptureStatus.Success, count: 3);
    }

    private static void TestDescriptorLimits()
    {
        Scenario s = Normal();
        s.InitialSize = s.InitialStride = s.FinalStride = 40;
        s.Slack = 4095;
        s.FinalSize = 163840;
        Check(s, 163840, 163840, UefiMemoryMapCaptureStatus.Success, count: 4096, firmwareCapacity: 163840);
        s.Slack = 4096;
        s.FinalSize = 163880;
        Check(s, 163880, 163880, UefiMemoryMapCaptureStatus.CopyPlanRejected, UefiMemoryMapCopyPlanStatus.DescriptorCountLimitExceeded, firmwareCapacity: 163880);
    }

    private static void TestArithmeticEdges()
    {
        Scenario s = Normal();
        s.InitialSize = ulong.MaxValue - 40;
        s.InitialStride = 40;
        s.Slack = 1;
        s.AllocationStatus = OutOfResources;
        Check(s, ulong.MaxValue, 512, UefiMemoryMapCaptureStatus.AllocatePoolFailed, firmware: OutOfResources, required: ulong.MaxValue - 40, events: 0x12);
        s.InitialSize = ulong.MaxValue - 39;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.AllocationSizeOverflow, UefiMemoryMapCopyPlanStatus.SizeOverflow, required: ulong.MaxValue - 39, events: 1);
        s.InitialSize = ulong.MaxValue - 41;
        Check(s, ulong.MaxValue - 1, 512, UefiMemoryMapCaptureStatus.AllocatePoolFailed, firmware: OutOfResources, required: ulong.MaxValue - 41, events: 0x12);
        s.InitialSize = 1;
        s.InitialStride = 1UL << 63;
        s.Slack = 2;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.AllocationSizeOverflow, UefiMemoryMapCopyPlanStatus.SizeOverflow, required: 1, events: 1);
        s.InitialStride = 40;
        s.Slack = 1UL << 63;
        Check(s, 0, 512, UefiMemoryMapCaptureStatus.AllocationSizeOverflow, UefiMemoryMapCopyPlanStatus.SizeOverflow, required: 1, events: 1);
    }

    private static void TestCopyPlanScalars()
    {
        FirmwareMemoryMapRecord r = new()
        {
            BufferAddress = 0x100000, BufferSize = 120, MapSize = 120,
            DescriptorSize = 40, DescriptorVersion = 1, DescriptorCount = 3, MapKey = 0xFEDCBA9876543210
        };
        UefiMemoryMapCopyPlanResult ok = UefiMemoryMapCopyPlanResult.Create(r, 120);
        Need(ok.Succeeded && ok.Plan.RequiredDestinationBytes == 120 && ok.Plan.DescriptorCount == 3 && ok.Plan.MapKey == 0xFEDCBA9876543210, "literal copy plan");
        CopyFailure(r, 119, 4096, UefiMemoryMapCopyPlanStatus.DestinationTooSmall);
        r.DescriptorCount = 0;
        CopyFailure(r, 120, 4096, UefiMemoryMapCopyPlanStatus.InvalidDescriptorCount);
        r.DescriptorCount = 2;
        CopyFailure(r, 120, 4096, UefiMemoryMapCopyPlanStatus.InvalidDescriptorCount);
        r.DescriptorCount = ulong.MaxValue / 40 + 1;
        CopyFailure(r, 0, 4096, UefiMemoryMapCopyPlanStatus.DescriptorCountLimitExceeded);
        CopyFailure(r, 0, ulong.MaxValue, UefiMemoryMapCopyPlanStatus.SizeOverflow);
        r.DescriptorSize = 39;
        r.DescriptorVersion = 0;
        CopyFailure(r, 0, 0, UefiMemoryMapCopyPlanStatus.InvalidDescriptorSize);
        r.MapSize = 121;
        CopyFailure(r, 0, 0, UefiMemoryMapCopyPlanStatus.InvalidMapSize);
        r.BufferAddress = 0;
        CopyFailure(r, 0, 0, UefiMemoryMapCopyPlanStatus.MissingSource);
    }

    private static void CopyFailure(FirmwareMemoryMapRecord source, ulong capacity, ulong limit, UefiMemoryMapCopyPlanStatus status)
    {
        UefiMemoryMapCopyPlanResult result = UefiMemoryMapCopyPlanResult.Create(source, capacity, limit);
        Need(!result.Succeeded && result.Status == status && ZeroPlan(result.Plan), "copy-plan scalar status/default");
    }

    private static void TestZeroAllocation()
    {
        Check(Normal(), 480, 512, UefiMemoryMapCaptureStatus.Success, count: 3, measure: true);
        Scenario s = Normal();
        s.FinalStatus = TooSmall;
        s.FinalSize = 528;
        Check(s, 480, 512, UefiMemoryMapCaptureStatus.FinalGetMemoryMapFailed, firmware: TooSmall, required: 528, measure: true);
    }

    private static void Check(Scenario scenario, ulong allocation, int destinationCapacity,
        UefiMemoryMapCaptureStatus status, UefiMemoryMapCopyPlanStatus copyStatus = UefiMemoryMapCopyPlanStatus.Success,
        ulong firmware = 0, ulong required = 0, ulong count = 0, int events = 0x123,
        int firmwareCapacity = 512, bool measure = false)
    {
        // The fixture input is authored independently of the production sizing/copy loops.
        ReadOnlySpan<byte> descriptor = [
            7, 0, 0, 0, 0xA1, 0xB2, 0xC3, 0xD4,
            0, 0, 0x20, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            3, 0, 0, 0, 0, 0, 0, 0,
            8, 0, 0, 0, 0, 0, 0, 0,
            0x81, 0x92, 0xA3, 0xB4, 0xC5, 0xD6, 0xE7, 0xF8];
        byte[] payload = new byte[firmwareCapacity];
        for (int i = 0; i < payload.Length; i++) payload[i] = descriptor[i % descriptor.Length];
        byte[] source = new byte[firmwareCapacity + Guard * 2];
        byte[] destination = new byte[destinationCapacity + Guard * 2];
        Array.Fill(source, Untouched);
        Array.Fill(destination, Untouched);
        fixed (byte* p = payload)
        fixed (byte* src = source)
        fixed (byte* dst = destination)
        {
            s_payload = p;
            s_firmware = src + Guard;
            s_capacity = (ulong)firmwareCapacity;
            s_expectedAllocation = allocation;
            Reset(scenario);
            UefiMemoryMapCaptureResult result = Invoke(dst + Guard, (ulong)destinationCapacity);
            if (measure)
            {
                // Warm both callbacks and helper before measuring only their repeated calls.
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    Reset(scenario);
                    result = Invoke(dst + Guard, (ulong)destinationCapacity);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Need(allocated == 0, "capture/callback allocated managed bytes");
            }
            Need(!s_callbackMismatch && s_events == events, "callback order/argument mismatch");
            Need(s_maps == (events == 0 ? 0 : events == 0x123 ? 2 : 1), "GetMemoryMap call count");
            Need(s_allocations == (events is 0x12 or 0x123 ? 1 : 0), "AllocatePool call count");
            Need(s_requestedAllocation == allocation && s_recaptureCapacity == (events == 0x123 ? allocation : 0), "full-width allocation/recapture capacity");
            Need(result.Status == status && result.CopyPlanStatus == copyStatus && result.Succeeded == (status == UefiMemoryMapCaptureStatus.Success), "capture status/precedence");
            Need(result.AllocationSize == allocation && result.FirmwareStatus == firmware && result.RequiredBufferSize == required, "preserved firmware/size diagnostics");
            if (result.Succeeded)
            {
                CheckRecord(result.SourceRecord, (ulong)s_firmware, allocation, (ulong)s_firmware, allocation, scenario, count, 0);
                CheckRecord(result.OwnedRecord, (ulong)(dst + Guard), (ulong)destinationCapacity, (ulong)s_firmware, allocation, scenario, count, 1);
                UefiMemoryMapCopyPlan plan = result.CopyPlan;
                Need(plan.SourceAddress == (ulong)s_firmware && plan.SourceBufferSize == allocation && plan.SourceMapSize == scenario.FinalSize
                    && plan.DestinationCapacity == (ulong)destinationCapacity && plan.RequiredDestinationBytes == scenario.FinalSize
                    && plan.MapKey == 0xFEDCBA9876543210 && plan.DescriptorSize == scenario.FinalStride
                    && plan.DescriptorVersion == scenario.Version && plan.DescriptorCount == count, "copy plan binding");
            }
            else
            {
                Need(ZeroRecord(result.OwnedRecord) && ZeroPlan(result.CopyPlan), "rejection published owned copy/plan");
                if (status == UefiMemoryMapCaptureStatus.CopyPlanRejected)
                    CheckRecord(result.SourceRecord, (ulong)s_firmware, allocation, (ulong)s_firmware, allocation, scenario, scenario.FinalSize / scenario.FinalStride, 0);
                else
                    Need(ZeroRecord(result.SourceRecord), "unexpected source diagnostics");
            }
            ulong expectedWritten = events == 0x123 && scenario.FinalStatus == 0 && scenario.FinalSize <= allocation
                && scenario.FinalSize <= (ulong)firmwareCapacity ? scenario.FinalSize : 0;
            Need(s_written == expectedWritten, "callback bounded write count");
            for (int i = 0; i < source.Length; i++)
            {
                bool copied = i >= Guard && (ulong)(i - Guard) < expectedWritten;
                Need(source[i] == (copied ? payload[i - Guard] : Untouched), "firmware bytes/canaries");
            }
            for (int i = 0; i < destination.Length; i++)
            {
                bool copied = result.Succeeded && i >= Guard && (ulong)(i - Guard) < scenario.FinalSize;
                Need(destination[i] == (copied ? payload[i - Guard] : Untouched), "whole owned buffer/canaries");
            }
        }
        s_firmware = s_payload = null;
    }

    private static void CheckRecord(FirmwareMemoryMapRecord r, ulong address, ulong capacity, ulong source, ulong sourceCapacity, Scenario s, ulong count, uint flags)
    {
        Need(r.BufferAddress == address && r.BufferSize == capacity && r.SourceBufferAddress == source && r.SourceBufferSize == sourceCapacity
            && r.MapSize == s.FinalSize && r.MapKey == 0xFEDCBA9876543210 && r.DescriptorSize == s.FinalStride
            && r.DescriptorVersion == s.Version && r.DescriptorCount == count && r.Flags == flags, "record identity/final metadata");
    }

    private static bool ZeroRecord(FirmwareMemoryMapRecord r) => r.BufferAddress == 0 && r.BufferSize == 0
        && r.SourceBufferAddress == 0 && r.SourceBufferSize == 0 && r.MapSize == 0 && r.MapKey == 0
        && r.DescriptorSize == 0 && r.DescriptorVersion == 0 && r.DescriptorCount == 0 && r.Flags == 0;

    private static bool ZeroPlan(UefiMemoryMapCopyPlan p) => p.SourceAddress == 0 && p.SourceBufferSize == 0
        && p.SourceMapSize == 0 && p.DestinationCapacity == 0 && p.RequiredDestinationBytes == 0
        && p.MapKey == 0 && p.DescriptorSize == 0 && p.DescriptorVersion == 0 && p.DescriptorCount == 0;

    private static void Reset(Scenario scenario)
    {
        s_scenario = scenario;
        s_events = s_maps = s_allocations = 0;
        s_requestedAllocation = s_recaptureCapacity = s_written = 0;
        s_callbackMismatch = false;
    }

    private static UefiMemoryMapCaptureResult Invoke(byte* destination, ulong capacity)
    {
        delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> map = &GetMemoryMap;
        delegate* unmanaged<uint, ulong, void**, ulong> allocate = &AllocatePool;
        if (s_scenario.DefaultSlack)
            return UefiMemoryMapCapture.Capture(map, allocate, destination, capacity);
        return UefiMemoryMapCapture.Capture(s_scenario.MissingMap ? null : map,
            s_scenario.MissingAllocation ? null : allocate,
            s_scenario.MissingDestination ? null : destination, capacity, s_scenario.Slack);
    }

    [UnmanagedCallersOnly]
    private static ulong GetMemoryMap(ulong* size, void* buffer, ulong* key, ulong* stride, uint* version)
    {
        s_maps++;
        if (s_maps == 1)
        {
            s_callbackMismatch |= s_events != 0 || buffer != null || *size != 0;
            s_events = (s_events << 4) | 1;
            *size = s_scenario.InitialSize;
            *stride = s_scenario.InitialStride;
            *version = 1;
            *key = 0x123456789ABCDEF0;
            return s_scenario.QueryStatus;
        }
        if (s_maps != 2 || s_events != 0x12 || buffer != s_firmware)
        {
            s_callbackMismatch = true;
            return DeviceError;
        }
        s_events = 0x123;
        s_recaptureCapacity = *size;
        s_callbackMismatch |= *size != s_expectedAllocation;
        // Malformed-success cases deliberately report metadata without an unsafe write.
        // BUFFER_TOO_SMALL and every other error write no descriptor bytes.
        if (s_scenario.FinalStatus == 0 && s_scenario.FinalSize <= *size && s_scenario.FinalSize <= s_capacity)
        {
            for (ulong i = 0; i < s_scenario.FinalSize; i++) s_firmware[i] = s_payload[i];
            s_written = s_scenario.FinalSize;
        }
        *size = s_scenario.FinalSize;
        *stride = s_scenario.FinalStride;
        *version = s_scenario.Version;
        *key = 0xFEDCBA9876543210;
        return s_scenario.FinalStatus;
    }

    [UnmanagedCallersOnly]
    private static ulong AllocatePool(uint type, ulong size, void** buffer)
    {
        s_allocations++;
        s_callbackMismatch |= s_events != 1 || s_allocations != 1 || type != 2 || size != s_expectedAllocation;
        s_events = (s_events << 4) | 2;
        s_requestedAllocation = size;
        *buffer = null;
        if (s_scenario.AllocationStatus != 0) return s_scenario.AllocationStatus;
        if (s_scenario.NullAllocation) return 0;
        if (size > s_capacity)
        {
            s_callbackMismatch = true;
            return OutOfResources;
        }
        *buffer = s_firmware;
        return 0;
    }
}
