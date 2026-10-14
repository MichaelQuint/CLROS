using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Transition = OS.Boot.FirstOptRealExitBootServicesTransition;

internal static unsafe class FirstOptRealExitBootServicesTransitionTests
{
    private static ulong captureStatus, exitStatus;
    private static int maps, exits, continuations, reentries;
    private static FirstExitTransitionState* active;
    private static FirstExitDescriptorContext* descriptors;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        Run("default boot boundary refuses before reading unprepared storage", () =>
        {
            FirstExitOwnedArenaState arena = default;
            Need(BootSequence.RunFirstRealExitBootServicesTransition(ref arena, null) == FirstExitReturn.NotStarted,
                "default-off boot boundary must not consume firmware or dereference the arena");
            Need(Probes.TryGetProbe(16, out var probe) &&
                probe.Name == nameof(Probes.EnableM03FirstRealExitBootServicesProbe) &&
                !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive &&
                probe.Group == Probes.UefiExitBootServicesGroup, "destructive default-off catalog entry");
        });
        Run("admission and failed exits cannot transfer", Failures);
        Run("successful exit transfers once and cannot return to its caller", TerminalTransfer);
        Run("permanent CR3 preparation and emitted continuation", PermanentContinuation);
        Run("owned descriptor bridge preserves selectors across adoption", DescriptorPreparation);
        Run("firmware entries bind from checked tables and consume once", FirmwareBinding);
        Run("owned stack entry has bounded storage and a terminal native path", OwnedStackEntry);
        Run("owned arena materializes retained descriptors map storage and four stacks", OwnedArena);
        Run("loaded image sections retain exact bounded code and data permissions", ImageMappings);
        Run("normalized terminal faults use only owned serial and never return natively", TerminalFault);
    }

    private static void Run(string name, Action action)
    {

        HostCheck.Run("m03.007.coordinator " + name, action);
    }
    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static readonly byte[] FaultOutput = new byte[4096];
    private static int faultOutputBytes, faultPolls;
    private static int faultTransmitByteLimit = int.MaxValue;
    private static byte* faultCorruptCanary;
    private static int faultCorruptAfterBytes;
    private static bool faultDivisorLatch, faultTransmitReady;
    [UnmanagedCallersOnly]
    private static void FaultOut8(ushort port, byte value)
    {
        if (port == 0x3fb) faultDivisorLatch = (value & 0x80) != 0;
        if (port == 0x3f8 && !faultDivisorLatch && faultOutputBytes < FaultOutput.Length)
        {
            FaultOutput[faultOutputBytes++] = value;
            if (faultCorruptCanary != null && faultOutputBytes == faultCorruptAfterBytes) *faultCorruptCanary ^= 1;
        }
    }
    [UnmanagedCallersOnly]
    private static byte FaultIn8(ushort port)
    { faultPolls++; return port == 0x3fd && faultTransmitReady && faultOutputBytes < faultTransmitByteLimit ? (byte)0x20 : (byte)0; }

    private static void TerminalFault()
    {
        Need(sizeof(FirstExitFaultFrame) == 184 &&
            Marshal.OffsetOf<FirstExitFaultFrame>(nameof(FirstExitFaultFrame.Vector)) == 120 &&
            Marshal.OffsetOf<FirstExitFaultFrame>(nameof(FirstExitFaultFrame.Rip)) == 144 &&
            Marshal.OffsetOf<FirstExitFaultFrame>(nameof(FirstExitFaultFrame.Rsp)) == 168, "normalized native frame layout");
        nuint out8 = (nuint)(delegate* unmanaged<ushort, byte, void>)&FaultOut8;
        nuint in8 = (nuint)(delegate* unmanaged<ushort, byte>)&FaultIn8;
        nuint first = Math.Min(out8, in8), end = Math.Max(out8 + 7, in8 + 5);
        var request = CheckedPortIoFunctionPointerBindingRequest.Create(true, 0x3f8, first, end, 12,
            out8 - first, in8 - first, out8, in8, 7, 5, 7, 5);
        // Controlled unmanaged host callbacks, not executed IN/OUT instructions.
        var backend = new Com1OwnedPortIoBackend(InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(request));
        Need(backend.IsReady, "controlled checked port backend");
        FirstExitFaultContext context = default;
        Need(!FirstExitTerminalFaultStub.Prepare(ref context, default) && context.Phase == 0 &&
            FirstExitTerminalFaultStub.Prepare(ref context, backend) && context.Phase == 1 &&
            !FirstExitTerminalFaultStub.Prepare(ref context, backend), "one-use fault preparation");
        FirstExitFaultFrame frame = new() { Vector = 14, HasErrorCode = 1, ErrorCode = 2,
            Rip = 0x12345000, Rsp = 0x87654000, Rflags = 0x202, Rax = 0xaabbccdd };
        Need(!FirstExitTerminalFaultStub.Report(&frame, &context, 0x1000, 0x2000), "native entry phase required");
        context.Phase = 2; faultTransmitReady = true; faultOutputBytes = faultPolls = 0;
        Need(FirstExitTerminalFaultStub.Report(&frame, &context, 0x1000, 0x2000) && context.Phase == 3 &&
            Encoding.ASCII.GetString(FaultOutput, 0, faultOutputBytes).Contains("exit-fault", StringComparison.Ordinal), "owned panic report");
        int bytes = faultOutputBytes;
        Need(!FirstExitTerminalFaultStub.Report(&frame, &context, 0x1000, 0x2000) && faultOutputBytes == bytes, "no duplicate report");
        context.Phase = 2; frame.HasErrorCode = 0; faultOutputBytes = faultPolls = 0;
        Need(!FirstExitTerminalFaultStub.Report(&frame, &context, 0, 0) && context.Phase == 4 && faultPolls == 0, "malformed frame refuses before I/O");
        context.Phase = 2; frame.HasErrorCode = 1; faultTransmitReady = false;
        Need(!FirstExitTerminalFaultStub.Report(&frame, &context, 0, 0) && context.Phase == 4 &&
            faultPolls == PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit && faultOutputBytes == 0,
            "serial timeout is bounded and consumed");
        byte* code = stackalloc byte[128];
        new Span<byte>(code, 128).Fill(0xa5);
        Need(!FirstExitTerminalFaultStub.Emit(code, 1, 0x200000, 0x300000, out uint written) &&
            !FirstExitTerminalFaultStub.Emit(code, 128, (ulong)code, 0x300000, out written) &&
            !FirstExitTerminalFaultStub.Emit(code, 128, 0x200000, 0x200010, out written) &&
            !FirstExitTerminalFaultStub.Emit(code, 128, 0x200001, 0x300000, out written) &&
            !new ReadOnlySpan<byte>(code, 128).ContainsAnyExcept((byte)0xa5), "short output unchanged");
        Need(FirstExitTerminalFaultStub.Emit(code, 128, 0x200000, 0x300000, out written) &&
            written == FirstExitTerminalFaultStub.RequiredBytes && code[written] == 0xa5, "bounded terminal emission");
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "issue64", "fault-stub");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "fault.bin"), new ReadOnlySpan<byte>(code, (int)written).ToArray());
    }

    private static void ImageMappings()
    {
        const ulong bytes = 65536;
        byte* image = (byte*)NativeMemory.AlignedAlloc((nuint)bytes, 4096);
        Need(image != null, "image fixture allocation");
        try
        {
            new Span<byte>(image, (int)bytes).Clear();
            *(ushort*)image = 0x5a4d; *(uint*)(image + 60) = 0x80;
            byte* pe = image + 0x80;
            *(uint*)pe = 0x4550; *(ushort*)(pe + 4) = 0x8664;
            *(ushort*)(pe + 6) = 3; *(ushort*)(pe + 20) = 240; *(ushort*)(pe + 22) = 2;
            byte* optional = pe + 24;
            *(ushort*)optional = 0x20b; *(uint*)(optional + 16) = 0x1000;
            *(uint*)(optional + 32) = 4096; *(uint*)(optional + 36) = 512;
            *(uint*)(optional + 56) = (uint)bytes; *(uint*)(optional + 60) = 512;
            *(ushort*)(optional + 68) = 10; *(uint*)(optional + 108) = 16;
            byte* text = optional + 240;
            *(uint*)(text + 8) = 0x1001; *(uint*)(text + 12) = 0x1000;
            *(uint*)(text + 16) = 0x1200; *(uint*)(text + 36) = 0x60000020;
            byte* data = text + 40;
            *(uint*)(data + 8) = 0x3000; *(uint*)(data + 12) = 0x4000;
            *(uint*)(data + 16) = 512; *(uint*)(data + 36) = 0xc0000040;
            byte* readOnly = data + 40;
            *(uint*)(readOnly + 8) = 0x100; *(uint*)(readOnly + 12) = 0x8000;
            *(uint*)(readOnly + 16) = 512; *(uint*)(readOnly + 36) = 0x40000040;
            EfiLoadedImageProtocolObservation observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(
                0, 0x1000, 0x2000, (ulong)image, bytes);
            EfiLoadedImageRangeCapturePlan capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            M01LoadedImageVirtualMappingEvidence mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(capture);
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[5];
            ranges[4] = new(0x12345000, 1, PageProtection.Read);
            Need(FirstExitImageMappings.WriteRanges(capture, mapping, ranges, 5, out ulong count) && count == 4 &&
                ranges[4].Start == 0x12345000, "derive sections without touching tail");
            Need(ranges[0].Start == (ulong)image && ranges[0].PageCount == 1 && ranges[0].Protection == PageProtection.Read &&
                ranges[1].Start == (ulong)image + 0x1000 && ranges[1].PageCount == 2 && ranges[1].Protection == (PageProtection.Read | PageProtection.Execute) &&
                ranges[2].Start == (ulong)image + 0x4000 && ranges[2].PageCount == 3 && ranges[2].Protection == (PageProtection.Read | PageProtection.Write) &&
                ranges[3].Start == (ulong)image + 0x8000 && ranges[3].PageCount == 1 && ranges[3].Protection == PageProtection.Read,
                "PE code, zero-filled data, immutable data and headers have exact page permissions");
            Need(FirstExitImageMappings.Contains(ranges, count, (ulong)image + 0x4010, 8192, PageProtection.Read | PageProtection.Write) &&
                !FirstExitImageMappings.Contains(ranges, count, (ulong)image + 0x4010, 8192, PageProtection.Read | PageProtection.Execute) &&
                !FirstExitImageMappings.Contains(ranges, count, (ulong)image + 0x2ff0, 32, PageProtection.Read | PageProtection.Execute),
                "retained object containment respects permissions and gaps");
            VerifyCodeBundle(capture, mapping, (ulong)image + 0x1000, (ulong)image + 0x4000);
            byte[] original = new ReadOnlySpan<byte>(image, (int)bytes).ToArray();
            byte[] output = new ReadOnlySpan<byte>(ranges, 5 * sizeof(SeparatePageFaultMappingRange)).ToArray();
            foreach (int fault in Enumerable.Range(0, 10))
            {
                original.CopyTo(new Span<byte>(image, (int)bytes));
                switch (fault)
                {
                    case 0: *(uint*)(image + 60) = (uint)bytes - 8; break;
                    case 1: *(ushort*)(pe + 6) = 17; break;
                    case 2: *(uint*)(optional + 32) = 512; break;
                    case 3: *(uint*)(text + 36) |= 0x80000000; break;
                    case 4: *(uint*)(data + 12) = 0x2000; break;
                    case 5: *(uint*)(data + 8) = (uint)bytes; break;
                    case 6: *(uint*)(readOnly + 36) = 0; break;
                    case 7: *(uint*)(optional + 16) = 0x4000; break;
                    case 8: *(uint*)(optional + 60) = 256; break;
                    case 9: *(uint*)(readOnly + 12) = 0x8001; break;
                }
                Need(!FirstExitImageMappings.WriteRanges(capture, mapping, ranges, 5, out count) && count == 0 &&
                    output.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(ranges, 5 * sizeof(SeparatePageFaultMappingRange))),
                    "invalid image refuses before output mutation " + fault);
            }
            original.CopyTo(new Span<byte>(image, (int)bytes));
            Need(!FirstExitImageMappings.WriteRanges(capture, mapping, ranges, 3, out count) &&
                !FirstExitImageMappings.WriteRanges(capture, mapping, (SeparatePageFaultMappingRange*)image, 5, out count) &&
                !FirstExitImageMappings.WriteRanges(default, mapping, ranges, 5, out count) &&
                !FirstExitImageMappings.WriteRanges(capture, default, ranges, 5, out count) &&
                original.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(image, (int)bytes)),
                "capacity, overlap and identity evidence refuse without modifying image");
        }
        finally { NativeMemory.AlignedFree(image); }
    }

    private static void VerifyCodeBundle(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, ulong callback, ulong nonExecutableTarget)
    {
        byte* allocation = (byte*)NativeMemory.AlignedAlloc(FirstExitCodeBundle.Bytes + 8192, 4096);
        byte* data = (byte*)NativeMemory.AlignedAlloc(FirstExitOwnedArena.Bytes, 4096);
        Need(allocation != null && data != null, "code bundle backing");
        byte* code = allocation + 4096;
        try
        {
            new Span<byte>(allocation, (int)FirstExitCodeBundle.Bytes + 8192).Fill(0xa5);
            ref FirstExitOwnedArenaState arena = ref *(FirstExitOwnedArenaState*)(nonExecutableTarget + 16);
            arena = default; // original adapter state lives in the fixture's RW image section
            Need(FirstExitOwnedArena.Materialize(ref arena, data, FirstExitOwnedArena.Bytes, (ulong)code, 56), "bundle arena");
            Need(FirstExitOwnedArena.ValidateDescriptors(ref arena, (ulong)code) &&
                !FirstExitOwnedArena.ValidateDescriptors(ref arena, (ulong)code + 4096), "IDTs bind the exact vector page");
            foreach (int offset in new int[] { 2, 80, 96, 4096 + 13, 4096 + 56, 8192 + 36,
                8192 + 200, 12288 + 16, 16384 + 2, 16384 + 4 })
            {
                data[offset] ^= 1;
                Need(!FirstExitCodeBundle.Materialize(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, callback) &&
                    arena.Fault->Phase == 0 && !new ReadOnlySpan<byte>(code, (int)FirstExitCodeBundle.Bytes).ContainsAnyExcept((byte)0xa5),
                    "changed descriptor storage refuses before code writes " + offset);
                data[offset] ^= 1;
            }
            Need(!FirstExitCodeBundle.Materialize(code, FirstExitCodeBundle.Bytes - 1, ref arena, capture, mapping, callback) &&
                !FirstExitCodeBundle.Materialize(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, nonExecutableTarget) &&
                !FirstExitCodeBundle.Materialize(data, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, callback) &&
                arena.Fault->Phase == 0 && !new ReadOnlySpan<byte>(code, (int)FirstExitCodeBundle.Bytes).ContainsAnyExcept((byte)0xa5),
                "capacity, data callback and code/data overlap refuse before mutation");
            Need(FirstExitCodeBundle.Materialize(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, callback) &&
                arena.Fault->Phase == 1 && arena.Fault->Backend.IsReady &&
                FirstExitCodeBundle.ValidateReadBack(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, callback),
                "actual composed code and bound port stubs read back");
            Need(!new ReadOnlySpan<byte>(allocation, 4096).ContainsAnyExcept((byte)0xa5) &&
                !new ReadOnlySpan<byte>(code + FirstExitCodeBundle.Bytes, 4096).ContainsAnyExcept((byte)0xa5), "bundle output extent");
            byte[] original = new ReadOnlySpan<byte>(code, (int)FirstExitCodeBundle.Bytes).ToArray();
            foreach (uint offset in new uint[] { 0, FirstExitCodeBundle.FaultOffset + 71, FirstExitCodeBundle.Out8Offset,
                FirstExitCodeBundle.In8Offset, FirstExitCodeBundle.StackEntryOffset, FirstExitCodeBundle.PermanentOffset, 6000 })
            {
                code[offset] ^= 1;
                Need(!FirstExitCodeBundle.ValidateReadBack(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, callback),
                    "byte mutation rejected at " + offset);
                code[offset] ^= 1;
            }
            Need(!FirstExitCodeBundle.Materialize(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, callback) &&
                !FirstExitCodeBundle.ValidateReadBack(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, callback + 1) &&
                original.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(code, (int)FirstExitCodeBundle.Bytes)),
                "prepared bundle cannot be regenerated or retargeted by readback");
            VerifyCompleteRetainedMappings(ref arena, code, capture, mapping, callback, nonExecutableTarget);
            arena = default;
            // No generated IN/OUT or privileged transition instruction is executed.
        }
        finally { NativeMemory.AlignedFree(allocation); NativeMemory.AlignedFree(data); }
    }

    private static void VerifyCompleteRetainedMappings(ref FirstExitOwnedArenaState arena, byte* code,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        ulong callback, ulong imageData)
    {
        byte* allocator = (byte*)NativeMemory.AlignedAlloc(8192, 4096);
        Need(allocator != null, "retained allocator backing");
        try
        {
            FirstExitImageReferences references = new() { EntryCallback = callback + 8,
                DiagnosticCallback = callback + 16, FaultCallback = callback,
                AdapterState = imageData, AdapterStateBytes = 512,
                AllocatorState = imageData + 1024, AllocatorStateBytes = 128,
                CopiedAcpi = imageData + 4096, CopiedAcpiBytes = 4096 };
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary(0x100000, out PhysicalAddress root);
            X64PatMappingToken.TryCreate(0x0006_0501_0007_0406, 0, 3, 4, out X64PatMappingToken pat);
            var tables = new SeparatePageFaultPageTableRequest(root, 55,
                X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat));
            var backing = new SeparatePageFaultMappingRange((ulong)allocator, 2, PageProtection.Read | PageProtection.Write);
            SeparatePageFaultMappingRange* output = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            new Span<byte>(output, (int)FirstExitRetainedMappings.MaximumRanges * sizeof(SeparatePageFaultMappingRange)).Fill(0xa5);
            byte[] before = new ReadOnlySpan<byte>(output, (int)FirstExitRetainedMappings.MaximumRanges * sizeof(SeparatePageFaultMappingRange)).ToArray();
            for (int failure = 0; failure < 11; failure++)
            {
                var candidate = references;
                var range = backing;
                if (failure == 0) candidate.EntryCallback = imageData;
                if (failure == 1) candidate.AdapterState += 512;
                if (failure == 2) candidate.CopiedAcpiBytes = 3 * 4096;
                if (failure == 3) range = new((ulong)code, 2, PageProtection.Read | PageProtection.Write);
                if (failure == 4) range = new(0x100000, 2, PageProtection.Read | PageProtection.Write);
                if (failure == 6) candidate.RuntimeSupportCodeBytes = 4096;
                if (failure >= 7) { candidate.RuntimeSupportCode = 0x500000; candidate.RuntimeSupportCodeBytes = 4096; }
                if (failure == 7) candidate.RuntimeSupportCodeBytes = 8192;
                if (failure == 8) candidate.RuntimeSupportCode++;
                if (failure == 9) candidate.RuntimeSupportCode = imageData;
                if (failure == 10) candidate.RuntimeSupportCode = 0x100000;
                Need(!FirstExitRetainedMappings.WriteRanges(ref arena, code, capture, mapping, candidate, range, tables,
                    output, failure == 5 ? 1UL : FirstExitRetainedMappings.MaximumRanges, out ulong refused) && refused == 0 &&
                    before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(output, before.Length)), "inventory refuses without writes " + failure);
            }
            Need(!FirstExitRetainedMappings.WriteRanges(ref arena, code, capture, mapping, references, backing, tables,
                (SeparatePageFaultMappingRange*)imageData, FirstExitRetainedMappings.MaximumRanges, out _), "inventory cannot overwrite image inputs");
            Need(FirstExitRetainedMappings.WriteRanges(ref arena, code, capture, mapping, references, backing, tables,
                output, FirstExitRetainedMappings.MaximumRanges, out ulong count) && count == 11 &&
                output[9].Start == (ulong)code && output[9].PageCount == 3 && output[9].Protection == (PageProtection.Read | PageProtection.Execute) &&
                output[10].Start == (ulong)allocator && output[10].PageCount == 2 &&
                !new ReadOnlySpan<byte>(output + count, (int)(FirstExitRetainedMappings.MaximumRanges - count) * sizeof(SeparatePageFaultMappingRange))
                    .ContainsAnyExcept((byte)0xa5), "complete inventory and untouched capacity tail");
            VerifyRetainedArenaImage(arena.Storage, output, count, FirstExitRetainedMappings.MaximumRanges, 109);
            references.RuntimeSupportCode = 0x500000;
            references.RuntimeSupportCodeBytes = 4096;
            Need(FirstExitRetainedMappings.WriteRanges(ref arena, code, capture, mapping, references, backing, tables,
                output, FirstExitRetainedMappings.MaximumRanges, out count) && count == 12 &&
                output[11].Start == 0x500000 && output[11].PageCount == 1 &&
                output[11].Protection == (PageProtection.Read | PageProtection.Execute), "retain original runtime helper RX page");
            VerifyRetainedArenaImage(arena.Storage, output, count, FirstExitRetainedMappings.MaximumRanges, 110);
            VerifyPostExitReport(ref arena, code, capture, mapping, references, backing, output, count);
        }
        finally { NativeMemory.AlignedFree(allocator); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void* VirtualAlloc(void* address, nuint bytes, uint allocationType, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(void* address, nuint bytes, uint freeType);
    [UnmanagedCallersOnly]
    private static ulong ReportMap(ulong* bytes, void* buffer, ulong* key, ulong* width, uint* version)
    { *bytes = 40; *key = 1; *width = 40; *version = 1; return 0; }
    [UnmanagedCallersOnly]
    private static ulong ReportExit(void* image, ulong key) => key == 1 ? 0UL : 1UL;

    private static void VerifyPostExitReport(ref FirstExitOwnedArenaState arena, byte* code,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        FirstExitImageReferences references, SeparatePageFaultMappingRange allocator,
        SeparatePageFaultMappingRange* ranges, ulong count)
    {
        // The production reporter requires identity backing below 4 GiB. This
        // Windows-host fixture allocates ordinary RW pages there; it never loads CR3.
        if (!OperatingSystem.IsWindows())
        { Console.WriteLine("CLROS_M03_007_REPORT_FIXTURE_SKIPPED reason=windows_low_address_allocation"); return; }
        ulong* entries = null;
        for (uint hint = 0x30000000; hint < 0x60000000 && entries == null; hint += 0x01000000)
            entries = (ulong*)VirtualAlloc((void*)(nuint)hint, 55 * 4096, 0x3000, 4);
        Need(entries != null, "low identity table backing");
        try
        {
            references.CopiedAcpi += 4096;
            X64PageTableImagePage* pages = (X64PageTableImagePage*)(references.AdapterState + 4096);
            Need(55 * sizeof(X64PageTableImagePage) <= 4096, "retained metadata fixture extent");
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary((ulong)entries, out PhysicalAddress root);
            X64PatMappingToken.TryCreate(0x0006_0501_0007_0406, 0, 3, 4, out X64PatMappingToken pat);
            FirstExitDiagnosticMemory memory = new() { Code = code, Capture = capture, Mapping = mapping,
                References = references, Allocator = allocator, Tables = new(root, 55,
                    X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat)),
                Pages = pages, PageCapacity = 55, Entries = entries, EntryCapacity = 55 * 512 };
            Need(SeparateDisabledDefaultPageFaultPageTables.BuildRetainedImage(memory.Tables, ranges, count,
                FirstExitRetainedMappings.MaximumRanges, pages, 55, entries, 55 * 512).Succeeded, "build actual identity-backed supplied tables");
            Need(FirstExitOwnedStackStub.Prepare(ref *arena.StackEntry, 0x100000, 0x668,
                arena.StackBottom, arena.StackTop, FirstExitOwnedArena.LowerCanary, FirstExitOwnedArena.UpperCanary, 196608,
                (delegate* unmanaged<FirstExitOwnedStackContext*, void>)references.EntryCallback,
                (void*)references.AdapterState, references.AdapterStateBytes), "prepare original entry");
            Need(FirstExitPermanentCr3Stub.Prepare(ref *arena.Permanent, 0x100000, (ulong)entries, 0x668,
                arena.StackBottom, arena.StackTop, 4096, FirstExitOwnedArena.LowerCanary, FirstExitOwnedArena.UpperCanary,
                (delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void>)references.DiagnosticCallback,
                (void*)references.AdapterState, arena.Descriptors), "prepare original continuation");
            BoundedExitRetryState retry = default;
            BoundedExitRetryResult attempt = BoundedRetryStaleKeyStatus.Execute(ref retry,
                FirmwareServicesMode.BootServicesAvailable, &ReportMap, &ReportExit, (void*)123, arena.Map, FirstExitOwnedArena.MapBytes);
            Need(attempt.Succeeded, "controlled successful exit result");
            *(BoundedExitRetryResult*)((byte*)arena.Transition + Marshal.OffsetOf<FirstExitTransitionState>("AttemptValue")) = attempt;
            *(uint*)arena.Transition = (uint)FirstExitPhase.OwnedContinuation;
            ((uint*)arena.Transition)[1] = (uint)FirmwareServicesMode.BootServicesExited;
            *(uint*)arena.Binding = 2; // model the native adapter's already-consumed binding
            arena.StackEntry->Phase = 2; arena.StackEntry->ObservedRsp = arena.StackTop - FirstExitOwnedStackStub.CallStackBytes;
            arena.Permanent->Phase = FirstExitAdoptionPhase.OwnedDiagnostic;
            arena.Permanent->ObservedCr3 = (ulong)entries; arena.Permanent->ObservedCr4 = 0x668 & ~0x80UL;
            arena.Permanent->ObservedRsp = arena.StackTop - 4096 - 8;
            FirstExitDescriptorContext* descriptor = arena.Descriptors;
            descriptor->Gdt[13] |= 1; descriptor->Gdt[21] |= 1; descriptor->Gdt[29] |= 2;
            descriptor->Gdt[descriptor->FirmwareCs + 5] |= 1;
            for (int i = 0; i < 16; i++) { descriptor->ObservedGdtr[i] = descriptor->Gdtr[i]; descriptor->ObservedIdtr[i] = descriptor->FinalIdtr[i]; }
            descriptor->ObservedTr = 24; descriptor->ObservedCs = 8;
            descriptor->ObservedDs = descriptor->ObservedEs = descriptor->ObservedSs = 16;
            Need(!FirstExitOwnedArena.ValidateInstalledDescriptors(ref arena, (ulong)code),
                "installed observations require the descriptor context consumed by the native stub");
            descriptor->Prepared = 2;
            Need(!FirstExitOwnedArena.ValidateDescriptors(ref arena, (ulong)code) &&
                FirstExitOwnedArena.ValidateInstalledDescriptors(ref arena, (ulong)code), "fresh and installed descriptor checks stay distinct");
            SeparatePageFaultMappingRange* ownedScratch = (SeparatePageFaultMappingRange*)(arena.StackBottom + 8);
            Need(FirstExitRetainedMappings.WriteInstalledRanges(ref arena, code, capture, mapping, references, allocator,
                memory.Tables, ownedScratch, FirstExitRetainedMappings.MaximumRanges, out ulong installedCount) && installedCount == count,
                "post-adoption declarations can use bounded main-stack scratch");
            nuint out8 = (nuint)(delegate* unmanaged<ushort, byte, void>)&FaultOut8;
            nuint in8 = (nuint)(delegate* unmanaged<ushort, byte>)&FaultIn8;
            nuint first = Math.Min(out8, in8), end = Math.Max(out8 + 7, in8 + 5);
            arena.Fault->Backend = new Com1OwnedPortIoBackend(InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(
                CheckedPortIoFunctionPointerBindingRequest.Create(true, 0x3f8, first, end, 12,
                    out8 - first, in8 - first, out8, in8, 7, 5, 7, 5)));
            // Controlled host port callbacks replace the fixture backend. Native
            // observations above are modeled, not evidence of privileged execution.
            ref FirstExitDiagnosticState state = ref *(FirstExitDiagnosticState*)(references.AdapterState + 64);
            const string firstMarker = "M03_EBS_RETURNED_SUCCESS\r\n";
            const string secondMarker = "M03_PERMANENT_CR3_ADOPTED\r\n";
            const string thirdMarker = "M03_POST_EBS_OWNED_DIAGNOSTIC\r\n";
            const string readyMarker = "CLROS_M03_007_READY\r\n";
            const string passMarker = "TEST_PASS m03.007\r\n";
            const string diagnosticsStart = "TEST_START m03.009\r\n";
            const string exitSuccess = "UEFI_EBS_SUCCESS\r\n";
            const string diagnosticsReady = "POST_FIRMWARE_DIAGNOSTICS_READY\r\n";
            const string diagnosticsClaim = "CLROS_M03_009_READY\r\n";
            const string diagnosticsPass = "TEST_PASS m03.009\r\n";
            string transitionMarkers = firstMarker + secondMarker + thirdMarker;
            string originalMarkers = transitionMarkers + readyMarker + passMarker;
            string completeMarkers = originalMarkers + diagnosticsStart + exitSuccess +
                diagnosticsReady + diagnosticsClaim + diagnosticsPass;
            for (int scenario = 0; scenario < 17; scenario++)
            {
                state = default; faultOutputBytes = faultPolls = 0; faultTransmitReady = true; faultTransmitByteLimit = int.MaxValue;
                FirstExitDiagnosticMemory candidate = memory;
                if (scenario == 1) arena.Permanent->ObservedCr3 ^= 4096;
                if (scenario == 2) descriptor->ObservedTr = 16;
                if (scenario == 3) entries[0] ^= 1;
                if (scenario == 4) faultTransmitReady = false;
                if (scenario == 5) faultTransmitByteLimit = firstMarker.Length;
                if (scenario == 6) candidate.Entries++;
                if (scenario == 7) descriptor->Gdt[13] ^= 0x10;
                if (scenario == 8) arena.Permanent->ObservedRsp = arena.StackBottom;
                if (scenario == 9) { faultCorruptCanary = (byte*)(arena.StackBottom - 8); faultCorruptAfterBytes = firstMarker.Length + secondMarker.Length; }
                if (scenario == 10) faultTransmitByteLimit = firstMarker.Length + secondMarker.Length;
                if (scenario == 11) ((uint*)arena.Transition)[1] = (uint)FirmwareServicesMode.BootServicesAvailable;
                if (scenario == 12) arena.Permanent->Phase = FirstExitAdoptionPhase.Prepared;
                if (scenario == 13) faultTransmitByteLimit = transitionMarkers.Length;
                if (scenario == 14) { faultCorruptCanary = (byte*)(arena.StackBottom - 8); faultCorruptAfterBytes = transitionMarkers.Length; }
                if (scenario == 15) faultTransmitByteLimit = transitionMarkers.Length + readyMarker.Length;
                if (scenario == 16) { faultCorruptCanary = (byte*)(arena.StackBottom - 8); faultCorruptAfterBytes = transitionMarkers.Length + readyMarker.Length; }
                bool passed = FirstExitPostExitDiagnostics.Report(ref state, ref arena, candidate);
                string observed = Encoding.ASCII.GetString(FaultOutput, 0, faultOutputBytes);
                string expected = scenario == 0 ? completeMarkers :
                    scenario is 15 or 16 ? transitionMarkers + readyMarker : scenario is 13 or 14 ? transitionMarkers :
                    scenario is 9 or 10 ? firstMarker + secondMarker : scenario is 3 or 5 ? firstMarker : "";
                Need(passed == (scenario == 0) && observed == expected, "post-exit marker gating " + scenario);
                FirstExitDiagnosticStatus expectedStatus = scenario == 0 ? FirstExitDiagnosticStatus.Completed :
                    scenario is 3 or 9 or 14 or 16 ? FirstExitDiagnosticStatus.ValidationFailed :
                    scenario is 4 or 5 or 10 or 13 or 15 ? FirstExitDiagnosticStatus.SerialFailed : FirstExitDiagnosticStatus.AdmissionFailed;
                Need(state.Status == expectedStatus, "post-exit status " + scenario);
                int priorBytes = faultOutputBytes, priorPolls = faultPolls;
                Need(!FirstExitPostExitDiagnostics.Report(ref state, ref arena, candidate) && faultOutputBytes == priorBytes && faultPolls == priorPolls,
                    "every report attempt is consumed");
                if (scenario == 1) arena.Permanent->ObservedCr3 ^= 4096;
                if (scenario == 2) descriptor->ObservedTr = 24;
                if (scenario == 3) entries[0] ^= 1;
                if (scenario == 7) descriptor->Gdt[13] ^= 0x10;
                if (scenario == 8) arena.Permanent->ObservedRsp = arena.StackTop - 4096 - 8;
                if (scenario is 9 or 14 or 16) { *faultCorruptCanary ^= 1; faultCorruptCanary = null; }
                if (scenario == 11) ((uint*)arena.Transition)[1] = (uint)FirmwareServicesMode.BootServicesExited;
                if (scenario == 12) arena.Permanent->Phase = FirstExitAdoptionPhase.OwnedDiagnostic;
            }
            int beforeExitSuccess = originalMarkers.Length + diagnosticsStart.Length;
            int beforeDiagnosticsReady = beforeExitSuccess + exitSuccess.Length;
            int beforeDiagnosticsClaim = beforeDiagnosticsReady + diagnosticsReady.Length;
            int beforeDiagnosticsPass = beforeDiagnosticsClaim + diagnosticsClaim.Length;
            Need(diagnosticsReady.Length == 33 && completeMarkers.Length == 235, "exact owned diagnostic byte lengths");
            // Existing admission/mapping cases above remain intact. These cases
            // exercise only the appended claims and their original owned UART.
            (int Bytes, bool CorruptCanary)[] tailCases =
            [
                (originalMarkers.Length + 3, false),
                (beforeExitSuccess + 8, false),
                (beforeDiagnosticsReady + 16, false),
                (beforeDiagnosticsReady + 31, false),
                (beforeDiagnosticsReady + 32, false), // CR written, final LF cannot be sent
                (beforeDiagnosticsClaim, false), // full 33-byte line; no next claim
                (beforeDiagnosticsClaim + 10, false),
                (completeMarkers.Length - 1, false), // no Completed before the final LF
                (originalMarkers.Length, true),
                (beforeExitSuccess, true),
                (beforeDiagnosticsReady, true),
                (beforeDiagnosticsClaim, true),
                (beforeDiagnosticsPass, true)
            ];
            foreach (var test in tailCases)
            {
                state = default; faultOutputBytes = faultPolls = 0; faultTransmitReady = true;
                faultTransmitByteLimit = test.CorruptCanary ? int.MaxValue : test.Bytes;
                if (test.CorruptCanary)
                {
                    faultCorruptCanary = (byte*)(arena.StackBottom - 8);
                    faultCorruptAfterBytes = test.Bytes;
                }
                Need(!FirstExitPostExitDiagnostics.Report(ref state, ref arena, memory) &&
                    state.Status == (test.CorruptCanary ? FirstExitDiagnosticStatus.ValidationFailed : FirstExitDiagnosticStatus.SerialFailed) &&
                    Encoding.ASCII.GetString(FaultOutput, 0, faultOutputBytes) == completeMarkers[..test.Bytes],
                    "owned diagnostic tail cutoff " + test);
                Need(faultPolls == test.Bytes + (test.CorruptCanary ? 0 :
                    PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit), "bounded tail polling " + test);
                int priorBytes = faultOutputBytes, priorPolls = faultPolls;
                Need(!FirstExitPostExitDiagnostics.Report(ref state, ref arena, memory) &&
                    faultOutputBytes == priorBytes && faultPolls == priorPolls, "consumed diagnostic tail " + test);
                if (test.CorruptCanary) { *faultCorruptCanary ^= 1; faultCorruptCanary = null; }
            }
            Console.WriteLine("CLROS_M03_007_REPORT_HOST_READY identityBacking=true markerBytes=235 guestExecution=false");
            Console.WriteLine("CLROS_M03_009_REPORT_HOST_READY tailCases=13 markerBytes=111 guestExecution=false");
        }
        finally { faultTransmitByteLimit = int.MaxValue; faultCorruptCanary = null; VirtualFree(entries, 0, 0x8000); }
    }

    private static void OwnedArena()
    {
        byte* allocation = (byte*)NativeMemory.AlignedAlloc(FirstExitOwnedArena.Bytes + 8192, 4096);
        Need(allocation != null, "allocate retained arena fixture");
        byte* data = allocation + 4096;
        try
        {
            new Span<byte>(allocation, (int)FirstExitOwnedArena.Bytes + 8192).Fill(0xa5);
            FirstExitOwnedArenaState state = default;
            Need(!RuntimeHelpers.IsReferenceOrContainsReferences<FirstExitOwnedArenaState>(), "arena state POD");
            Need(!FirstExitOwnedArena.Materialize(ref state, data, FirstExitOwnedArena.Bytes - 1, 0x100000, 56) &&
                !FirstExitOwnedArena.Materialize(ref state, data, FirstExitOwnedArena.Bytes, (ulong)data, 56) &&
                !FirstExitOwnedArena.Materialize(ref state, data, FirstExitOwnedArena.Bytes, 0x100000, 16) &&
                state.Storage == null && state.Descriptors == null &&
                !new ReadOnlySpan<byte>(data, (int)FirstExitOwnedArena.Bytes).ContainsAnyExcept((byte)0xa5),
                "invalid capacity, vector overlap and selector refuse before writes");
            Need(FirstExitOwnedArena.Materialize(ref state, data, FirstExitOwnedArena.Bytes, 0x100000, 56) &&
                state.IsReady && !state.FailedAfterWrite && FirstExitOwnedArena.ValidateCanaries(state), "materialize actual retained buffers");
            Need(!new ReadOnlySpan<byte>(allocation, 4096).ContainsAnyExcept((byte)0xa5) &&
                !new ReadOnlySpan<byte>(data + FirstExitOwnedArena.Bytes, 4096).ContainsAnyExcept((byte)0xa5), "arena extent leaves surrounding pages intact");
            Need((byte*)state.Descriptors == data && (byte*)state.StackEntry == data + 128 &&
                (byte*)state.Permanent == data + 256 && (byte*)state.Binding == data + 384 &&
                (byte*)state.Transition == data + 512 && (byte*)state.Fault == data + 1024 &&
                state.Fault->Phase == 0 && state.Map == data + 5 * 4096 &&
                !new ReadOnlySpan<byte>(data + 128, 4096 - 128).ContainsAnyExcept((byte)0) &&
                !new ReadOnlySpan<byte>(state.Map, (int)FirstExitOwnedArena.MapBytes).ContainsAnyExcept((byte)0), "disjoint zero-initialized contexts and map");
            Need(state.Descriptors->Gdt == data + 4096 && state.Descriptors->Tss == data + 8192 &&
                state.Descriptors->FinalIdt == data + 12288 && state.Descriptors->BridgeIdt == data + 16384 &&
                *(ulong*)(state.Descriptors->Tss + 4) == state.StackTop &&
                state.StackTop - state.StackBottom - FirstExitOwnedStackStub.CallStackBytes >= 128 * 1024 + 64 * 1024,
                "main stack retains UEFI minimum plus first-party reserve");
            Need(OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                state.Descriptors->FinalIdt, 4096, (ulong)state.Descriptors->FinalIdt, 0x100000).Succeeded,
                "reuse canonical final IDT verifier");
            int guards = 0;
            for (uint page = 0; page < FirstExitOwnedArena.PageCount; page++)
                if (FirstExitOwnedArena.IsGuardPage(page))
                { guards++; Need(!new ReadOnlySpan<byte>(data + page * 4096, 4096).ContainsAnyExcept((byte)0), "reserved guard page remains zero"); }
            Need(guards == 8, "guard reservations for four stacks; mapping still deferred");
            SeparatePageFaultMappingRange* mapping = stackalloc SeparatePageFaultMappingRange[6];
            mapping[0] = new(0x12345000, 1, PageProtection.Read);
            mapping[5] = new(0x54321000, 1, PageProtection.Read);
            Need(!FirstExitOwnedArena.WriteMappingRanges(ref state, mapping, 4, out ulong mappedRanges) &&
                mappedRanges == 0 && mapping[0].Start == 0x12345000 &&
                !FirstExitOwnedArena.WriteMappingRanges(ref state, (SeparatePageFaultMappingRange*)data, 5, out mappedRanges),
                "short or arena-aliased mapping output refuses without writes");
            Need(FirstExitOwnedArena.WriteMappingRanges(ref state, mapping, 6, out mappedRanges) &&
                mappedRanges == 5 && mapping[5].Start == 0x54321000, "five ranges leave capacity tail intact");
            for (uint page = 0; page < FirstExitOwnedArena.PageCount; page++)
            {
                int covering = 0;
                for (ulong range = 0; range < mappedRanges; range++)
                {
                    Need(mapping[range].Protection == (PageProtection.Read | PageProtection.Write), "arena RW/NX intent");
                    ulong address = (ulong)data + page * 4096UL;
                    if (address >= mapping[range].Start && address - mapping[range].Start < mapping[range].PageCount * 4096)
                        covering++;
                }
                Need(covering == (FirstExitOwnedArena.IsGuardPage(page) ? 0 : 1), "exact arena coverage excludes all eight guards");
            }
            VerifyRetainedArenaImage(data, mapping, mappedRanges);
            data[22 * 4096] ^= 1;
            Need(!FirstExitOwnedArena.WriteMappingRanges(ref state, mapping, 6, out mappedRanges) && mappedRanges == 0,
                "damaged stack canary refuses mapping declarations");
            data[22 * 4096] ^= 1;
            foreach (uint offset in new uint[] { 22 * 4096, 86 * 4096 - 16, 88 * 4096, 92 * 4096 - 16,
                94 * 4096, 98 * 4096 - 16, 100 * 4096, 104 * 4096 - 16 })
            { data[offset] ^= 1; Need(!FirstExitOwnedArena.ValidateCanaries(state), "each canary is checked"); data[offset] ^= 1; }
            Need(FirstExitOwnedArena.ValidateCanaries(state) &&
                !FirstExitOwnedArena.Materialize(ref state, data, FirstExitOwnedArena.Bytes, 0x100000, 56), "prepared arena cannot be reset");
        }
        finally { NativeMemory.AlignedFree(allocation); }
    }

    private static void VerifyRetainedArenaImage(byte* data, SeparatePageFaultMappingRange* ranges, ulong rangeCount,
        ulong rangeCapacity = 6, ulong expectedMappedPages = 97)
    {
        const ulong root = 0x100000;
        const ulong tablePages = SeparateDisabledDefaultPageFaultPageTables.MaximumTablePages;
        ulong* entries = (ulong*)NativeMemory.AlignedAlloc((nuint)(tablePages * 4096), 4096);
        Need(entries != null, "retained mapping backing");
        try
        {
            new Span<ulong>(entries, (int)tablePages * 512).Clear();
            X64PageTableImagePage* metadata = stackalloc X64PageTableImagePage[(int)tablePages];
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary(root, out PhysicalAddress physical);
            X64PatMappingToken.TryCreate(0x0006_0501_0007_0406, 0, 3, 4, out X64PatMappingToken pat);
            X64PageTableEncodingPreconditions mode = X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat);
            SeparatePageFaultPageTableResult result = SeparateDisabledDefaultPageFaultPageTables.BuildRetainedImage(
                new(physical, tablePages, mode), ranges, rangeCount, rangeCapacity,
                metadata, tablePages, entries, tablePages * 512);
            Need(result.Succeeded && result.MappedPages == tablePages + expectedMappedPages, "actual retained declarations build an audited hierarchy");
            // Independent raw-bit walk checks the real output, including every guard gap.
            ulong Leaf(ulong address)
            {
                ulong table = 0, leaf = 0;
                for (int level = 4; level >= 1; level--)
                {
                    ulong raw = entries[table * 512 + ((address >> (12 + 9 * (level - 1))) & 511)];
                    if (raw == 0) break;
                    if (level == 1) { leaf = raw; break; }
                    ulong target = raw & 0x000f_ffff_ffff_f000;
                    Need((raw & ~0x000f_ffff_ffff_f000UL) == 3 && target >= root && target - root < tablePages * 4096,
                        "arena oracle follows only owned tables");
                    table = (target - root) / 4096;
                }
                return leaf;
            }
            for (ulong range = 0; range < rangeCount; range++)
                for (ulong page = 0; page < ranges[range].PageCount; page++)
                {
                    ulong address = ranges[range].Start + page * 4096;
                    PageProtection protection = ranges[range].Protection;
                    ulong expected = address | 1;
                    if ((protection & PageProtection.Write) != 0) expected |= 2;
                    if ((protection & PageProtection.Execute) == 0) expected |= 1UL << 63;
                    Need(Leaf(address) == expected, "independent retained range permissions");
                }
            for (uint page = 0; page < FirstExitOwnedArena.PageCount; page++)
            {
                ulong address = (ulong)data + page * 4096UL;
                ulong leaf = Leaf(address);
                Need(leaf == (FirstExitOwnedArena.IsGuardPage(page) ? 0 : address | (1UL << 63) | 3),
                    "actual arena mapping is exact RW/NX or an absent guard");
            }
        }
        finally { NativeMemory.AlignedFree(entries); }
    }

    private static void OwnedStackEntry()
    {
        Need(sizeof(FirstExitOwnedStackContext) == 88 &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<FirstExitOwnedStackContext>(), "stack-entry POD ABI");
        (string Field, int Offset)[] fields = [("FirmwareCr3", 0), ("FirmwareCr4", 8), ("StackBottom", 16),
            ("StackTop", 24), ("LowerCanary", 32), ("UpperCanary", 40), ("Entry", 48),
            ("EntryContext", 56), ("EntryStackBytes", 64), ("ObservedRsp", 72), ("Phase", 80)];
        foreach (var field in fields)
            Need((int)Marshal.OffsetOf<FirstExitOwnedStackContext>(field.Field) == field.Offset, "stack-entry offset " + field.Field);
        byte* memory = (byte*)NativeMemory.AlignedAlloc(4 * 4096, 4096);
        Need(memory != null, "allocate stack-entry fixture");
        try
        {
            new Span<byte>(memory, 4 * 4096).Clear();
            ulong bottom = (ulong)memory + 8, top = (ulong)memory + 8192;
            var entry = (delegate* unmanaged<FirstExitOwnedStackContext*, void>)0x100000;
            FirstExitOwnedStackContext context = default;
            bool Prepare(ulong budget, ulong end, ulong cr3 = 0x1018, ulong cr4 = 0x20)
                => FirstExitOwnedStackStub.Prepare(ref context, cr3, cr4, bottom, end, 11, 22,
                    budget, entry, memory + 12288, 256);
            Need(Prepare(4096, top) && context.Phase == 1 && context.EntryStackBytes == 4096 &&
                context.StackBottom == bottom && context.StackTop == top && context.ObservedRsp == 0 &&
                context.EntryContext == memory + 12288, "prepare retained stack without native execution");
            byte[] prepared = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref context, 1)).ToArray();
            Need(!Prepare(4096, top) && MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref context, 1)).SequenceEqual(prepared),
                "single-use preparation is unchanged on rejection");
            context = default;
            Need(!Prepare(0, top) && !Prepare(top - bottom, top) && !Prepare(4096, top - 8) &&
                !Prepare(4096, top, 0) && !Prepare(4096, top, 0x1001) && !Prepare(4096, top, 0x1018, 0x100020),
                "reject budget, alignment and unsupported CPU roots/modes");
            Need(!FirstExitOwnedStackStub.Prepare(ref *(FirstExitOwnedStackContext*)(memory + 16),
                0x1000, 0x20, bottom, top, 11, 22, 4096, entry, memory + 12288, 256), "stack cannot overwrite its context");
            Need(!FirstExitOwnedStackStub.Prepare(ref context, 0x1000, 0x20, bottom, top, 11, 22,
                4096, entry, memory + 128, 256) && context.Phase == 0, "entry context disjoint from stack");
            Need(Prepare(top - bottom - FirstExitOwnedStackStub.CallStackBytes, top), "exact budget boundary");

            byte[] code = new byte[FirstExitOwnedStackStub.RequiredBytes];
            fixed (byte* output = code)
            {
                Need(!FirstExitOwnedStackStub.Emit(output, (uint)code.Length - 1, out uint count) && count == 0 &&
                    code.All(value => value == 0), "short destination is not mutated");
                Need(FirstExitOwnedStackStub.Emit(output, (uint)code.Length, out count) && count == code.Length,
                    "emit actual owned-stack entry for independent decoding");
            }
            string path = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/issue64/stack-entry");
            Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, "entry.bin"), code);
        }
        finally { NativeMemory.AlignedFree(memory); }
    }

    private static void SealTable(byte* table, int bytes)
    {
        *(uint*)(table + 16) = 0;
        *(uint*)(table + 16) = OS.Std.Hash.Crc32.Compute(new ReadOnlySpan<byte>(table, bytes));
    }

    private static FirmwareMemoryMapRecord FirmwareFixture(byte* memory)
    {
        new Span<byte>(memory, 3 * 4096).Clear();
        byte* system = memory, boot = memory + 4096, map = memory + 8192;
        *(ulong*)system = 0x5453595320494249; *(uint*)(system + 8) = 0x20000; *(uint*)(system + 12) = 128;
        *(ulong*)(system + 96) = (ulong)boot; system[127] = 0x5a;
        *(ulong*)boot = 0x56524553544f4f42; *(uint*)(boot + 8) = 0x20000; *(uint*)(boot + 12) = 384;
        ulong mapEntry = (ulong)(delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong>)&Map;
        ulong exitEntry = (ulong)(delegate* unmanaged<void*, ulong, ulong>)&Exit;
        *(ulong*)(boot + 56) = mapEntry; *(ulong*)(boot + 232) = exitEntry; boot[383] = 0xa5;
        SealTable(system, 128); SealTable(boot, 384);
        ulong start = Math.Min(mapEntry, exitEntry) & ~4095UL, last = Math.Max(mapEntry, exitEntry);
        *(uint*)map = 3; *(ulong*)(map + 8) = start; *(ulong*)(map + 24) = (last - start) / 4096 + 1;
        // UEFI map attributes describe capabilities, not current page permissions.
        *(ulong*)(map + 32) = 0x4000;
        return new FirmwareMemoryMapRecord { BufferAddress = (ulong)map, BufferSize = 96, MapSize = 48,
            DescriptorSize = 48, DescriptorVersion = 1, DescriptorCount = 1, Flags = FirmwareMemoryMapRecord.FlagOwnedCopy };
    }

    private static FirstExitBindingStatus BindFirmware(ref FirstExitFirmwareBindingState binding,
        byte* memory, FirmwareMemoryMapRecord map)
        => FirstExitFirmwareBinding.Bind(ref binding, FirmwareServicesMode.BootServicesAvailable,
            (void*)123, memory, 128, memory + 4096, 384, map);

    private static void FirmwareBinding()
    {
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<FirstExitFirmwareBindingState>(), "binding POD");
        byte* memory = (byte*)NativeMemory.AlignedAlloc(3 * 4096, 4096);
        Need(memory != null, "allocate firmware fixture");
        int oldMaps = maps, oldExits = exits;
        try
        {
            FirmwareMemoryMapRecord map = FirmwareFixture(memory);
            byte[] original = new ReadOnlySpan<byte>(memory, 3 * 4096).ToArray();
            FirstExitFirmwareBindingState binding = default;
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.Ready && binding.IsReady, "bind original slots");
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.AlreadyBound, "cannot replace binding");
            Need(FirstExitFirmwareBinding.Consume(ref binding, FirmwareServicesMode.BootServicesAvailable,
                out var get, out var exit, out var handle) == FirstExitBindingStatus.Consumed &&
                (ulong)get == *(ulong*)(memory + 4096 + 56) && (ulong)exit == *(ulong*)(memory + 4096 + 232) &&
                handle == (void*)123 && binding.IsConsumed && binding.RetainedPointersCleared,
                "consume exact entries and clear binding before caller can issue final window");
            Need(new ReadOnlySpan<byte>(memory, 3 * 4096).SequenceEqual(original), "CRC validation never mutates tables");
            Need(FirstExitFirmwareBinding.Consume(ref binding, FirmwareServicesMode.BootServicesAvailable,
                out get, out exit, out handle) == FirstExitBindingStatus.NotReady && get == null && exit == null && handle == null,
                "cannot consume twice");
            FirstExitFirmwareBindingState unavailable = default;
            Need(BindFirmware(ref unavailable, memory, map) == FirstExitBindingStatus.Ready, "prepare unavailable scenario");
            Need(FirstExitFirmwareBinding.Consume(ref unavailable, FirmwareServicesMode.BootServicesExited,
                out get, out exit, out handle) == FirstExitBindingStatus.FirmwareUnavailable &&
                get == null && exit == null && handle == null && unavailable.IsConsumed && unavailable.RetainedPointersCleared,
                "unavailable mode clears capability without accessing firmware storage");

            foreach (int offset in new[] { 0, 16, 20, 127, 4096, 4096 + 16, 4096 + 20, 4096 + 383 })
            {
                map = FirmwareFixture(memory); binding = default; memory[offset] ^= 1;
                Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.InvalidTable && !binding.IsReady,
                    "reject signature/reserved/CRC/full extension corruption");
            }
            map = FirmwareFixture(memory); binding = default;
            *(uint*)(memory + 12) = 129;
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.InvalidTable, "header beyond readable extent");
            map = FirmwareFixture(memory); *(ulong*)(memory + 96) += 8; SealTable(memory, 128);
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.WrongBootServicesTable, "system table pointer association");
            map = FirmwareFixture(memory); *(ulong*)(memory + 4096 + 232) = 0; SealTable(memory + 4096, 384);
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.InvalidFunction, "null actual exit slot");
            map = FirmwareFixture(memory); *(uint*)(memory + 8192) = 4;
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.NotFirmwareCode, "data range cannot attest code");
            map = FirmwareFixture(memory);
            new ReadOnlySpan<byte>(memory + 8192, 48).CopyTo(new Span<byte>(memory + 8192 + 48, 48));
            map.MapSize = 96; map.DescriptorCount = 2;
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.NotFirmwareCode, "ambiguous overlapping code ranges");
            map = FirmwareFixture(memory); *(ulong*)(memory + 8192 + 24) = ulong.MaxValue;
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.InvalidMap, "overflowing map extent");
            map = FirmwareFixture(memory); map.Flags = 0;
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.InvalidMap, "map must be retained owned copy");
            map = FirmwareFixture(memory); map.BufferAddress = (ulong)memory;
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.InvalidStorage, "table/map overlap");

            map = FirmwareFixture(memory);
            Need(BindFirmware(ref binding, memory, map) == FirstExitBindingStatus.Ready, "fresh binding before mutation");
            memory[4096 + 383] ^= 1; SealTable(memory + 4096, 384);
            Need(FirstExitFirmwareBinding.Consume(ref binding, FirmwareServicesMode.BootServicesAvailable,
                out get, out exit, out handle) == FirstExitBindingStatus.Changed && get == null && exit == null && handle == null &&
                binding.IsConsumed && binding.RetainedPointersCleared, "changed valid-CRC table consumes without exposing entries");
            Need(maps == oldMaps && exits == oldExits, "binding never invokes firmware or host callbacks");
        }
        finally { NativeMemory.AlignedFree(memory); }
    }

    [UnmanagedCallersOnly]
    private static ulong Map(ulong* bytes, void* buffer, ulong* key, ulong* width, uint* version)
    {
        maps++; *bytes = 40; *key = (ulong)maps; *width = 40; *version = 1;
        if (Transition.AttemptAndTransfer(ref *active, FirmwareServicesMode.BootServicesAvailable,
            null, null, null, null, 0, null, null, 0) == FirstExitReturn.ReentryRejected) reentries++;
        return captureStatus;
    }
    [UnmanagedCallersOnly]
    private static ulong Exit(void* image, ulong key)
    { exits++; return (ulong)image == 123 && key == (ulong)maps ? exitStatus : ulong.MaxValue; }
    [UnmanagedCallersOnly]
    private static void Continuation(FirstExitTransitionState* state, void* context)
    { continuations++; }

    [UnmanagedCallersOnly]
    private static void Diagnostic(FirstExitTransitionState* state, FirstExitPermanentCr3Context* context) { }

    private static bool Prepare(ref FirstExitPermanentCr3Context context,
        ulong firmware = 0xF801000, ulong owned = 0x100000, ulong cr4 = 0x668,
        ulong bottom = 0x200008, ulong top = 0x240000, ulong depth = 16384)
        => FirstExitPermanentCr3Stub.Prepare(ref context, firmware, owned, cr4,
            bottom, top, depth, 0x1122334455667788, 0x8877665544332211, &Diagnostic, (void*)123, descriptors);

    private static void PermanentContinuation()
    {
        byte* memory = NewDescriptorFixture();
        descriptors = (FirstExitDescriptorContext*)memory;
        try
        {
            Need(PrepareDescriptors(memory), "prepare actual descriptor buffers");
            PermanentContinuationCore();
        }
        finally { descriptors = null; NativeMemory.AlignedFree(memory); }
    }

    private static void PermanentContinuationCore()
    {
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<FirstExitPermanentCr3Context>() &&
            sizeof(FirstExitPermanentCr3Context) == FirstExitPermanentCr3Stub.ContextBytes, "POD context size");
        (string Field, int Offset)[] layout = [("FirmwareCr3", 0), ("OwnedCr3", 8), ("FirmwareCr4", 16),
            ("StackBottom", 24), ("StackTop", 32), ("MinimumRsp", 40), ("LowerCanary", 48), ("UpperCanary", 56),
            ("Diagnostic", 64), ("DiagnosticContext", 72), ("ObservedCr3", 80), ("ObservedCr4", 88),
            ("Phase", 96), ("ObservedRsp", 104), ("Descriptors", 112)];
        foreach (var field in layout)
            Need((int)Marshal.OffsetOf<FirstExitPermanentCr3Context>(field.Field) == field.Offset, "native offset " + field.Field);
        Need((int)Marshal.OffsetOf<FirstExitTransitionState>("PhaseValue") == 0 &&
            (int)Marshal.OffsetOf<FirstExitTransitionState>("ModeValue") == 4 &&
            (uint)FirstExitPhase.OwnedContinuation == 3 && (uint)FirmwareServicesMode.BootServicesExited == 2,
            "native exit-state guard layout");
        FirstExitPermanentCr3Context context = default;
        Need(Prepare(ref context) && context.Phase == FirstExitAdoptionPhase.Prepared &&
            context.MinimumRsp == 0x204048 && context.DiagnosticContext == (void*)123 &&
            context.ObservedCr3 == 0 && context.ObservedCr4 == 0 && context.ObservedRsp == 0, "prepared bounds");
        Need(!Prepare(ref context), "cannot reset prepared context");
        foreach (ulong firmware in new[] { 0UL, 0x18UL, 0x1000_0000_0UL, 0xF801001UL })
        { context = default; Need(!Prepare(ref context, firmware: firmware) && context.Phase == 0, "invalid firmware root"); }
        foreach (ulong owned in new[] { 0UL, 0x100001UL, 0xF801000UL, 0x1000_0000_0UL })
        { context = default; Need(!Prepare(ref context, owned: owned) && context.Phase == 0, "invalid owned root"); }
        foreach (ulong cr4 in new[] { 0UL, 0x648UL, 0x1668UL, 0x20668UL, 0x800668UL })
        { context = default; Need(!Prepare(ref context, cr4: cr4) && context.Phase == 0, "unsupported processor mode"); }
        foreach (var stack in new[] { (0UL, 0x240000UL, 1UL), (0x200001UL, 0x240000UL, 1UL),
            (0x200008UL, 0x240001UL, 1UL), (0x200008UL, 0x200008UL, 1UL),
            (0x200008UL, 0x240000UL, 0UL), (0x200008UL, 0x240000UL, ulong.MaxValue),
            (0x200008UL, 0x240000UL, 0x3ffb8UL), (0x200008UL, 0x800000000000UL, 1UL) })
        { context = default; Need(!Prepare(ref context, bottom: stack.Item1, top: stack.Item2, depth: stack.Item3) &&
            context.Phase == 0, "invalid stack extent or headroom"); }
        context = default;
        ulong contextAddress = (ulong)&context;
        Need(!Prepare(ref context, bottom: contextAddress, top: contextAddress + 0x10000), "context overlaps live stack");
        Need(!FirstExitPermanentCr3Stub.Prepare(ref context, 0xF801000, 0x100000, 0x668,
            0x200008, 0x240000, 16384, 1, 2, null, null, descriptors), "missing diagnostic");
        Need(!FirstExitPermanentCr3Stub.Prepare(ref context, 0xF801000, 0x100000, 0x668,
            0x200008, 0x240000, 16384, 1, 2, &Diagnostic, null, null), "missing descriptors");

        uint bytes = FirstExitPermanentCr3Stub.RequiredBytes;
        byte* output = stackalloc byte[(int)bytes + 2];
        new Span<byte>(output, (int)bytes + 2).Fill(0xa5);
        Need(!FirstExitPermanentCr3Stub.Emit(output + 1, bytes - 1, out uint written) && written == 0 &&
            new ReadOnlySpan<byte>(output, (int)bytes + 2).IndexOfAnyExcept((byte)0xa5) == -1,
            "short destination unchanged");
        Need(!FirstExitPermanentCr3Stub.Emit(null, bytes, out written) && written == 0, "null destination");
        Need(FirstExitPermanentCr3Stub.Emit(output + 1, bytes, out written) && written == bytes &&
            output[0] == 0xa5 && output[bytes + 1] == 0xa5, "exact bounded emission");
        byte[] raw = new ReadOnlySpan<byte>(output + 1, (int)bytes).ToArray();
        Need(raw.AsSpan(0, 2).SequenceEqual(new byte[] { 0xfa, 0xfc }) &&
            raw.AsSpan(raw.Length - 4).SequenceEqual(new byte[] { 0xfa, 0xf4, 0xeb, 0xfc }), "terminal CLI/HLT loop");
        // Preserve actual emitter output for independent decoding, never execute
        // privileged instructions in the host test process.
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/issue64/permanent-stub");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "continuation.bin"), raw);
    }

    private static byte* NewDescriptorFixture()
    {
        byte* memory = (byte*)NativeMemory.AlignedAlloc(5 * 4096, 4096);
        Need(memory != null, "allocate descriptor fixture");
        ResetDescriptors(memory);
        return memory;
    }

    private static void ResetDescriptors(byte* memory)
    {
        new Span<byte>(memory, 5 * 4096).Clear();
        byte* gdt = memory + 4096, tss = memory + 8192, idt = memory + 12288;
        byte* raw = stackalloc byte[184];
        byte* scratch = (byte*)(((ulong)raw + 15) & ~15UL);
        var prepared = OS.Kernel.Diagnostics.OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(
            scratch, (ulong)tss - 64, 168);
        Need(prepared.ContextPrepared, "reuse M02 GDT/TSS materializer");
        new ReadOnlySpan<byte>(scratch + 24, 40).CopyTo(new Span<byte>(gdt, 40));
        new ReadOnlySpan<byte>(scratch + 64, 104).CopyTo(new Span<byte>(tss, 104));
        *(ulong*)(tss + 4) = 0x240000;
        *(ulong*)(tss + 36) = 0x300000; *(ulong*)(tss + 44) = 0x304000; *(ulong*)(tss + 52) = 0x308000;
        for (int vector = 0; vector < 256; vector++)
        {
            byte* gate = idt + vector * 16;
            uint handler = (uint)(0x100000 + vector * 16);
            *(ushort*)gate = (ushort)handler; *(ushort*)(gate + 6) = (ushort)(handler >> 16);
            *(ushort*)(gate + 2) = 8; gate[5] = 0x8e;
            gate[4] = vector == 2 ? (byte)3 : vector == 8 ? (byte)1 : vector == 14 ? (byte)2 : (byte)0;
        }
    }

    private static bool PrepareDescriptors(byte* memory, ushort firmwareCs = 56)
        => FirstExitDescriptors.Prepare(ref *(FirstExitDescriptorContext*)memory,
            memory + 4096, 4096, memory + 8192, 104, memory + 12288, 4096, memory + 16384, 4096, firmwareCs);

    private static void DescriptorPreparation()
    {
        Need(sizeof(FirstExitDescriptorContext) == 128 &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<FirstExitDescriptorContext>(), "descriptor POD ABI");
        (string Field, int Offset)[] layout = [("Gdtr", 0), ("BridgeIdtr", 16), ("FinalIdtr", 32),
            ("ObservedGdtr", 48), ("ObservedIdtr", 64), ("ObservedTr", 80), ("FirmwareCs", 82),
            ("ObservedCs", 84), ("ObservedDs", 86), ("ObservedEs", 88), ("ObservedSs", 90),
            ("Prepared", 92), ("Gdt", 96), ("Tss", 104), ("BridgeIdt", 112), ("FinalIdt", 120)];
        foreach (var field in layout)
            Need((int)Marshal.OffsetOf<FirstExitDescriptorContext>(field.Field) == field.Offset, "descriptor native offset " + field.Field);
        byte* memory = NewDescriptorFixture();
        try
        {
            foreach (ushort selector in new ushort[] { 8, 56, 4088 })
            {
                ResetDescriptors(memory);
                Need(PrepareDescriptors(memory, selector), "valid bridge selector");
                FirstExitDescriptorContext* d = (FirstExitDescriptorContext*)memory;
                Need(d->Prepared == 1 && *(ushort*)d->Gdtr == Math.Max(39, selector + 7) &&
                    *(ulong*)(d->Gdtr + 2) == (ulong)d->Gdt &&
                    *(ulong*)(d->Gdt + selector) == *(ulong*)(d->Gdt + 8), "GDT alias and extent");
                for (int vector = 0; vector < 256; vector++)
                {
                    byte* bridge = d->BridgeIdt + vector * 16, final = d->FinalIdt + vector * 16;
                    Need(*(ushort*)(bridge + 2) == selector && bridge[4] == 0 && *(ushort*)(final + 2) == 8,
                        "bridge uses current CS and no unloaded IST");
                    Need(*(ushort*)bridge == *(ushort*)final && new ReadOnlySpan<byte>(bridge + 5, 11).SequenceEqual(
                        new ReadOnlySpan<byte>(final + 5, 11)), "same owned terminal handler");
                }
                Need(!PrepareDescriptors(memory, selector), "descriptor preparation single use");
            }
            foreach (ushort selector in new ushort[] { 0, 7, 16, 24, 32, 4096, ushort.MaxValue })
            { ResetDescriptors(memory); Need(!PrepareDescriptors(memory, selector), "invalid/colliding code selector"); }
            // Each invalid input is rejected before context/bridge/GDT mutation.
            foreach (int offset in new[] { 4096, 4096 + 13, 4096 + 29, 4096 + 40,
                8192, 8192 + 36, 8192 + 102, 12288 + 2, 12288 + 4, 12288 + 5,
                12288 + 12, 12288 + 15 * 16 + 11, 16384 })
            {
                ResetDescriptors(memory); memory[offset] ^= 1;
                byte[] before = new ReadOnlySpan<byte>(memory, 5 * 4096).ToArray();
                Need(!PrepareDescriptors(memory) && new ReadOnlySpan<byte>(memory, 5 * 4096).SequenceEqual(before),
                    "refuse invalid descriptor at " + offset + " without mutation");
            }
            ResetDescriptors(memory);
            Need(!FirstExitDescriptors.Prepare(ref *(FirstExitDescriptorContext*)memory,
                memory + 4096, 4096, memory + 8192, 104, memory + 12288, 4096, memory + 12288, 4096, 56), "overlapping IDTs");
            foreach (int shortBuffer in new[] { 0, 1, 2, 3 })
                Need(!FirstExitDescriptors.Prepare(ref *(FirstExitDescriptorContext*)memory,
                    memory + 4096, shortBuffer == 0 ? 4095UL : 4096UL,
                    memory + 8192, shortBuffer == 1 ? 103UL : 104UL,
                    memory + 12288, shortBuffer == 2 ? 4095UL : 4096UL,
                    memory + 16384, shortBuffer == 3 ? 4095UL : 4096UL, 56), "short descriptor buffer");
        }
        finally { NativeMemory.AlignedFree(memory); }
    }

    private static FirstExitReturn Invoke(ref FirstExitTransitionState state, byte* buffer, void* context)
    {
        fixed (FirstExitTransitionState* pointer = &state)
        {
            active = pointer;
            return Transition.AttemptAndTransfer(ref state, FirmwareServicesMode.BootServicesAvailable,
                &Map, &Exit, (void*)123, buffer, 128, &Continuation, context, 8);
        }
    }

    private static void Failures()
    {
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<FirstExitTransitionState>(), "POD state");
        byte* buffer = stackalloc byte[128]; ulong context = 1;
        FirstExitTransitionState state = default;
        Need(Transition.AttemptAndTransfer(ref state, FirmwareServicesMode.BootServicesExited,
            &Map, &Exit, (void*)123, buffer, 128, &Continuation, &context, 8) == FirstExitReturn.FirmwareUnavailable,
            "firmware unavailable");
        Need(Transition.AttemptAndTransfer(ref state, FirmwareServicesMode.BootServicesAvailable,
            &Map, &Exit, (void*)123, buffer, 128, null, &context, 8) == FirstExitReturn.InvalidContinuation,
            "missing continuation");
        foreach (ulong badContext in new[] { 0UL, (ulong)buffer, (ulong)&state, ulong.MaxValue - 3 })
            Need(Invoke(ref state, buffer, (void*)badContext) == FirstExitReturn.InvalidStorage, "invalid/overlapping context");
        Need(Invoke(ref state, (byte*)&state, &context) == FirstExitReturn.InvalidStorage, "map overlaps state");
        Need(state.Phase == FirstExitPhase.Empty && maps == 0 && exits == 0 && continuations == 0,
            "admission is side-effect free");

        foreach (var scenario in new[] { (0x8000000000000005UL, 0UL, 0), (0UL, 7UL, 1), (0UL, 0x8000000000000002UL, 2) })
        {
            state = default; maps = exits = continuations = reentries = 0;
            captureStatus = scenario.Item1; exitStatus = scenario.Item2;
            Need(Invoke(ref state, buffer, &context) == FirstExitReturn.ExitNotCompleted, "failed exit returns failure");
            Need(state.Phase == FirstExitPhase.Failed && state.Mode == FirmwareServicesMode.BootServicesAvailable &&
                !state.Attempt.Succeeded && state.Attempt.ExitCalls == (uint)scenario.Item3 &&
                exits == scenario.Item3 && continuations == 0 && reentries == maps && state.RetainedFirmwareCallbacksCleared,
                "failed exit never adopts; captures/retries retained but callbacks cleared");
            int priorMaps = maps;
            Need(Invoke(ref state, buffer, &context) == FirstExitReturn.AlreadyConsumed && maps == priorMaps,
                "consumed attempt cannot call firmware again");
        }
    }

    private static void TerminalTransfer()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/issue64-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string kernel = SecurityElement.Escape(typeof(Transition).Assembly.Location)!;
        File.WriteAllText(Path.Combine(directory, "Worker.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>
            <TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
            <AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings>
            </PropertyGroup><ItemGroup><Reference Include="OS.Kernel"><HintPath>{{kernel}}</HintPath>
            </Reference></ItemGroup></Project>
            """, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "Program.cs"), Worker, new UTF8Encoding(false));
        Execute(directory, "build", ["build", "Worker.csproj", "--nologo"], "Build succeeded.");
        foreach (string scenario in new[] { "one", "retry", "return" })
            Execute(directory, scenario, ["bin/Debug/net11.0/Worker.dll", scenario], "TEST_PASS transfer." + scenario);
    }

    private static void Execute(string directory, string name, string[] arguments, string marker)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in arguments) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        bool ended = process.WaitForExit(60_000);
        if (!ended) { process.Kill(true); process.WaitForExit(); }
        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, name + ".stdout.log"), stdout);
        File.WriteAllText(Path.Combine(directory, name + ".stderr.log"), stderr);
        Need(ended && process.ExitCode == 0 && stderr.Length == 0 && stdout.Contains(marker, StringComparison.Ordinal),
            name + ": " + stdout + stderr);
    }

    // Controlled host callbacks only. Environment.Exit terminates the owned test
    // process, not firmware; the return case observes the coordinator's terminal
    // fallback from a host thread. No privileged instruction runs in these tests.
    private const string Worker = """
        using System.Runtime.InteropServices;
        using OS.Boot;
        using OS.Kernel.Platform;
        unsafe class Program
        {
            static string scenario = "";
            static int maps, exits;
            static FirstExitTransitionState* active;
            static int Main(string[] args)
            {
                scenario = args[0];
                FirstExitTransitionState state = default; active = &state;
                byte* buffer = stackalloc byte[128]; ulong context = 0xC1A05;
                FirstOptRealExitBootServicesTransition.AttemptAndTransfer(ref state,
                    FirmwareServicesMode.BootServicesAvailable, &Map, &Exit, (void*)123,
                    buffer, 128, &Owned, &context, 8);
                Console.WriteLine("TEST_FAIL returned_to_efi_caller");
                return 91;
            }
            [UnmanagedCallersOnly]
            static ulong Map(ulong* bytes, void* buffer, ulong* key, ulong* width, uint* version)
            { maps++; *bytes=40; *key=(ulong)maps; *width=40; *version=1; return 0; }
            [UnmanagedCallersOnly]
            static ulong Exit(void* handle, ulong key)
            { exits++; return scenario=="retry" && exits==1 ? 0x8000000000000002UL : 0; }
            [UnmanagedCallersOnly]
            static void Owned(FirstExitTransitionState* state, void* context)
            {
                int expected=scenario=="retry" ? 2 : 1;
                if(state!=active || *(ulong*)context!=0xC1A05 || maps!=expected || exits!=expected ||
                    state->Phase!=FirstExitPhase.OwnedContinuation || state->Mode!=FirmwareServicesMode.BootServicesExited ||
                    !state->Attempt.Succeeded || state->Attempt.ExitCalls!=(uint)expected || !state->RetainedFirmwareCallbacksCleared ||
                    FirstOptRealExitBootServicesTransition.AttemptAndTransfer(ref *state, state->Mode,
                        null,null,null,null,0,null,null,0)!=FirstExitReturn.AlreadyConsumed)
                    Environment.Exit(92);
                if(scenario=="return")
                {
                    new Thread(() => {
                        while(true) { Thread.MemoryBarrier(); if(active->Phase==FirstExitPhase.UnexpectedContinuationReturn) break; Thread.Yield(); }
                        Console.WriteLine("TEST_PASS transfer.return"); Environment.Exit(0);
                    }) { IsBackground=true }.Start();
                    return;
                }
                Console.WriteLine("TEST_PASS transfer."+scenario); Environment.Exit(0);
            }
        }
        """;
}
