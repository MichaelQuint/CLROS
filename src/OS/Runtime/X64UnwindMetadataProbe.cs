#if NOSTDLIB_EFI && M04_X64_UNWIND_METADATA_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// One original TypeManager registration, one static metadata snapshot. No heap,
// second registration, stack walk, language handler or unwind execution occurs.
internal static unsafe partial class X64UnwindMetadataProbe
{
    internal const ulong Capacity = 65536, GuardStart = Capacity - 32;
    private const ulong StackBytes = 33 * 4096, StackClearance = 16384;
    private const ulong StackWord0 = 0x3D947E21A6C850BF, StackWord1 = 0xC2715AB809E634DF;





    private static Buffer s_storage;
    private static StackState s_stack;
    private static ulong s_phase, s_sampleA, s_sampleB, s_sampleFailure;

    // The bounded decoder stackalloc needs the existing no-CRT cookie convention.
    // NativeAOT reads this retained RX symbol; it is never called. Randomized
    // cookie initialization remains separate hardening work.
    [System.Runtime.RuntimeExport("__security_cookie")]
    private static ulong SecurityCookieStorageAnchor() => 0x434c_524f_5347_5331UL;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Run(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* moduleOwner, ulong moduleCapacity,
        ulong moduleHandle, EfiConsoleAdapter console)
    {
        Text(console, 0); console.WriteCrLf();
        if (s_phase != 0) return Fail(console, 1, X64UnwindMetadataStatus.AlreadyBound);
        s_phase = 1;
        TypeManagerModuleResult original = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, moduleOwner, moduleCapacity, moduleHandle, 204);
        if (!original.Succeeded || original.Length != (int)TypeManagerModuleMetadataLiveNativeAOTImage.SlotBytes || original.Section == 0 ||
            mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success ||
            ReadWord(original.Section) != moduleHandle)
            return Fail(console, 2, X64UnwindMetadataStatus.ModuleRejected);
        ulong imageBytes = capture.Range.ImageSize, witness = 0;
        fixed (Buffer* buffer = &s_storage)
        {
            byte* storage = (byte*)buffer;
            if (!ArmStack((ulong)&witness, image, imageBytes, (ulong)moduleOwner, moduleCapacity,
                    (ulong)storage) || !CheckStack())
                return Fail(console, 3, X64UnwindMetadataStatus.StorageRejected);
            X64UnwindMetadataResult measured = X64PERuntimeFunctionUnwindMetadataHostileInput.Validate(
                capture, mapping, moduleOwner, moduleCapacity, moduleHandle);
            if (!measured.Succeeded) return Fail(console, 4, measured.Status);
            if (measured.RequiredStorage == 0 || measured.RequiredStorage > GuardStart ||
                measured.ImageBase != image || measured.ModuleOwner != (ulong)moduleOwner ||
                measured.ModuleHandle != moduleHandle || measured.FunctionCount == 0 ||
                !Zero(storage, 0, Capacity))
                return Fail(console, 5, X64UnwindMetadataStatus.StorageRejected);
            X64UnwindMetadataResult bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(
                capture, mapping, moduleOwner, moduleCapacity, moduleHandle, storage, Capacity);
            if (!bound.Succeeded) return Fail(console, 6, bound.Status);
            if (bound.RequiredStorage != measured.RequiredStorage || bound.FunctionCount != measured.FunctionCount ||
                !Zero(storage, measured.RequiredStorage, Capacity - measured.RequiredStorage))
                return Fail(console, 7, X64UnwindMetadataStatus.MetadataChanged);
            // Bind has admitted the complete static buffer. Sentinels are only
            // armed in its unused tail, never in the immutable snapshot prefix.
            for (ulong i = GuardStart; i < Capacity; i++) storage[i] = Canary(i);
            if (!Preserved(storage, measured.RequiredStorage) || !CheckStack())
                return Fail(console, 8, X64UnwindMetadataStatus.MetadataChanged);

            // Header accesses are observations after full original-owner/core
            // validation, not another parser or an independent range authority.
            byte* optional = (byte*)image + Read32((byte*)image + 60) + 24;
            uint directory = Read32(optional + 136), directoryBytes = Read32(optional + 140);
            if (directoryBytes != measured.FunctionCount * 12)
                return Fail(console, 9, X64UnwindMetadataStatus.MetadataChanged);
            Line(console, 0, image, imageBytes, (ulong)moduleOwner, moduleHandle);
            Line(console, 1, directory, directoryBytes, measured.FunctionCount, measured.RequiredStorage);
            Line(console, 2, (ulong)storage, Capacity, GuardStart, X64PERuntimeFunctionUnwindMetadataHostileInput.StateBytes);
            Line(console, 3, s_stack.Authority, s_stack.Start, s_stack.End, s_stack.InitialRsp);
            Line(console, 4, s_stack.Witness, s_stack.Minimum, ReadWord(s_stack.Start), ReadWord(s_stack.Start + 8));

            delegate* managed<ulong, ulong> first = &SampleA, second = &SampleB;
            ulong seed = image ^ moduleHandle, firstValue = first(seed), secondValue = second(seed);
            if ((ulong)first == (ulong)second || firstValue != (seed ^ 0xFF) ||
                secondValue != (seed ^ 0x55) || s_sampleFailure != 0 || s_sampleA != 1 || s_sampleB != 2)
                return Fail(console, 10, X64UnwindMetadataStatus.FunctionRejected);
            if (!ObserveMethod(console, storage, measured, 5, (ulong)first, seed, firstValue) ||
                !ObserveMethod(console, storage, measured, 10, (ulong)second, seed, secondValue))
                return false;

            X64UnwindMetadataResult zero = BootSequence.RunX64UnwindMetadata(storage, Capacity, 0);
            X64UnwindMetadataResult outside = BootSequence.RunX64UnwindMetadata(storage, Capacity, image + imageBytes);
            if (zero.Status != X64UnwindMetadataStatus.NotFound || outside.Status != X64UnwindMetadataStatus.NotFound)
                return Fail(console, 11, X64UnwindMetadataStatus.MetadataChanged);
            X64UnwindMetadataResult final = X64PERuntimeFunctionUnwindMetadataHostileInput.Validate(
                capture, mapping, moduleOwner, moduleCapacity, moduleHandle);
            TypeManagerModuleResult retained = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
                capture, mapping, moduleOwner, moduleCapacity, moduleHandle, 204);
            if (!final.Succeeded || final.RequiredStorage != measured.RequiredStorage ||
                final.FunctionCount != measured.FunctionCount || !retained.Succeeded ||
                retained.Section != original.Section || retained.Length != (int)TypeManagerModuleMetadataLiveNativeAOTImage.SlotBytes ||
                ReadWord(retained.Section) != moduleHandle || !Preserved(storage, measured.RequiredStorage) ||
                !CheckStack() || s_stack.Checks != 7)
                return Fail(console, 12, X64UnwindMetadataStatus.ModuleRejected);
            Line(console, 15, (ulong)zero.Status, (ulong)outside.Status, s_sampleA, s_sampleB);
            Line(console, 16, s_stack.Checks, s_stack.Minimum, ReadWord(s_stack.Start), ReadWord(s_stack.Start + 8));
            Line(console, 17, measured.RequiredStorage, final.RequiredStorage, bound.FunctionCount, 4);
            Line(console, 18, (ulong)moduleOwner, moduleHandle, retained.Section, ReadWord(retained.Section));
            s_phase = 2;
            Text(console, 2); console.WriteCrLf(); Text(console, 3); console.WriteCrLf();
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ObserveMethod(EfiConsoleAdapter console, byte* storage, X64UnwindMetadataResult owner,
        ulong stage, ulong pc, ulong seed, ulong value)
    {
        X64UnwindMetadataResult found = BootSequence.RunX64UnwindMetadata(storage, Capacity, pc);
        if (!found.Succeeded) return Fail(console, 13, found.Status);
        if (found.ImageBase != owner.ImageBase || found.ModuleOwner != owner.ModuleOwner ||
            found.ModuleHandle != owner.ModuleHandle || found.FunctionCount != owner.FunctionCount ||
            found.RequiredStorage != owner.RequiredStorage || found.FunctionIndex >= owner.FunctionCount ||
            pc < found.ImageBase + found.BeginRva || pc >= found.ImageBase + found.EndRva ||
            found.StackBytes == 0 || found.CodeSlots == 0)
            return Fail(console, 14, X64UnwindMetadataStatus.FunctionRejected);
        ulong last = found.ImageBase + found.EndRva - 1;
        X64UnwindMetadataResult tail = BootSequence.RunX64UnwindMetadata(storage, Capacity, last);
        if (!tail.Succeeded || tail.FunctionIndex != found.FunctionIndex ||
            tail.BeginRva != found.BeginRva || tail.EndRva != found.EndRva ||
            tail.UnwindRva != found.UnwindRva || tail.PrimaryUnwindRva != found.PrimaryUnwindRva ||
            tail.ModuleOwner != owner.ModuleOwner || tail.ModuleHandle != owner.ModuleHandle || !CheckStack())
            return Fail(console, 15, X64UnwindMetadataStatus.MetadataChanged);
        Line(console, stage, pc, found.FunctionIndex, found.BeginRva, found.EndRva);
        Line(console, stage + 1, found.UnwindRva, found.PrimaryUnwindRva, found.PrologBytes, found.CodeSlots);
        Line(console, stage + 2, found.FrameRegister, found.FrameOffset, found.FixedAllocationBytes, found.StackBytes);
        Line(console, stage + 3, found.Flags, found.PrimaryFlags, found.ChainDepth, found.HandlerRva);
        Line(console, stage + 4, found.LanguageDataStartRva, seed, value, last);
        return true;
    }

    // The addresses are managed function pointers to these actual executed
    // non-leaf methods. Native map/PE review must associate both PCs and records.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong SampleA(ulong value)
    {
        s_sampleA++;
        return SampleB(value) ^ 0xAA;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong SampleB(ulong value)
    {
        s_sampleB++;
        ulong local = value;
        if (!CheckStack()) { s_sampleFailure = 1; return 0; }
        return ReadWord((ulong)&local) ^ 0x55;
    }

    private static bool ArmStack(ulong witness, ulong image, ulong imageBytes,
        ulong owner, ulong ownerBytes, ulong storage)
    {
        if (s_stack.Start != 0 || !EfiEntry.TryGetOriginalReferenceStack(out ulong authority,
                out ulong start, out ulong end, out ulong initialRsp) ||
            start == 0 || (start & 4095) != 0 || start > ~0UL - StackBytes ||
            end != start + StackBytes || initialRsp != end - 40 || !Witness(witness, start, initialRsp) ||
            !Separate(start, end, authority, 768) || !Separate(start, end, image, imageBytes) ||
            !Separate(start, end, owner, ownerBytes) || !Separate(start, end, storage, Capacity))
            return false;
        s_stack.Authority = authority; s_stack.Start = start; s_stack.End = end;
        s_stack.InitialRsp = initialRsp; s_stack.Witness = witness; s_stack.Minimum = witness;
        // Same original stack ownership protocol as the existing GC diagnostic.
        // These are diagnostic sentinels, not guard-page or current-RSP proof.
        *(ulong*)start = StackWord0; *(ulong*)(start + 8) = StackWord1;
        return ReadWord(start) == StackWord0 && ReadWord(start + 8) == StackWord1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CheckStack()
    {
        ulong local = 0, witness = (ulong)&local;
        if (s_phase != 1 || !EfiEntry.TryGetOriginalReferenceStack(out ulong authority,
                out ulong start, out ulong end, out ulong initialRsp) ||
            authority != s_stack.Authority || start != s_stack.Start || end != s_stack.End ||
            initialRsp != s_stack.InitialRsp || !Witness(witness, start, initialRsp) ||
            ReadWord(start) != StackWord0 || ReadWord(start + 8) != StackWord1)
            return false;
        if (witness < s_stack.Minimum) s_stack.Minimum = witness;
        s_stack.Checks++;
        return true;
    }

    private static bool Witness(ulong value, ulong start, ulong initialRsp) =>
        (value & 7) == 0 && value >= start + 16 + StackClearance && value <= initialRsp - 8;
    private static bool Separate(ulong start, ulong end, ulong at, ulong bytes) =>
        at != 0 && bytes != 0 && at <= ~0UL - bytes && (at + bytes <= start || at >= end);
    private static byte Canary(ulong offset) => offset < GuardStart + 16 ? (byte)0xA5 : (byte)0x5A;
    private static bool Preserved(byte* storage, ulong required)
    {
        if (required > GuardStart || !Zero(storage, required, GuardStart - required)) return false;
        for (ulong i = GuardStart; i < Capacity; i++) if (storage[i] != Canary(i)) return false;
        return true;
    }
    private static bool Zero(byte* storage, ulong start, ulong count)
    {
        for (ulong i = 0; i < count; i++) if (storage[start + i] != 0) return false;
        return true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ReadWord(ulong address) => *(ulong*)address;
    private static uint Read32(byte* value) =>
        (uint)value[0] | (uint)value[1] << 8 | (uint)value[2] << 16 | (uint)value[3] << 24;
    private static bool Fail(EfiConsoleAdapter console, ulong reason, X64UnwindMetadataStatus status)
    {
        s_phase = 3;
        Text(console, 1); Number(console, reason); Text(console, 9); Number(console, (ulong)status); console.WriteCrLf();
        return false;
    }
    private static void Line(EfiConsoleAdapter console, ulong stage, ulong a, ulong b, ulong c, ulong d)
    {
        Text(console, 4); Number(console, stage); Text(console, 5); Number(console, a);
        Text(console, 6); Number(console, b); Text(console, 7); Number(console, c);
        Text(console, 8); Number(console, d); console.WriteCrLf();
    }
    private static void Number(EfiConsoleAdapter console, ulong value)
    {
        ulong divisor = 1;
        while (divisor <= value / 10) divisor *= 10;
        do { console.WriteByte((byte)(48 + value / divisor)); value %= divisor; divisor /= 10; } while (divisor != 0);
    }
    private static void Text(EfiConsoleAdapter console, uint token)
    {
        switch (token)
        {
            // TEST_START m04.020
            case 0: console.WriteByte(84); console.WriteByte(69); console.WriteByte(83); console.WriteByte(84); console.WriteByte(95); console.WriteByte(83); console.WriteByte(84); console.WriteByte(65); console.WriteByte(82); console.WriteByte(84); console.WriteByte(32); console.WriteByte(109); console.WriteByte(48); console.WriteByte(52); console.WriteByte(46); console.WriteByte(48); console.WriteByte(50); console.WriteByte(48); break;
            // TEST_FAIL m04.020 reason=
            case 1: console.WriteByte(84); console.WriteByte(69); console.WriteByte(83); console.WriteByte(84); console.WriteByte(95); console.WriteByte(70); console.WriteByte(65); console.WriteByte(73); console.WriteByte(76); console.WriteByte(32); console.WriteByte(109); console.WriteByte(48); console.WriteByte(52); console.WriteByte(46); console.WriteByte(48); console.WriteByte(50); console.WriteByte(48); console.WriteByte(32); console.WriteByte(114); console.WriteByte(101); console.WriteByte(97); console.WriteByte(115); console.WriteByte(111); console.WriteByte(110); console.WriteByte(61); break;
            // CLROS_M04_020_READY methods=2 lookups=4 version=1 hostConformance=false guestIntegrated=true unwindExecution=false
            case 2: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(50); console.WriteByte(48); console.WriteByte(95); console.WriteByte(82); console.WriteByte(69); console.WriteByte(65); console.WriteByte(68); console.WriteByte(89); console.WriteByte(32); console.WriteByte(109); console.WriteByte(101); console.WriteByte(116); console.WriteByte(104); console.WriteByte(111); console.WriteByte(100); console.WriteByte(115); console.WriteByte(61); console.WriteByte(50); console.WriteByte(32); console.WriteByte(108); console.WriteByte(111); console.WriteByte(111); console.WriteByte(107); console.WriteByte(117); console.WriteByte(112); console.WriteByte(115); console.WriteByte(61); console.WriteByte(52); console.WriteByte(32); console.WriteByte(118); console.WriteByte(101); console.WriteByte(114); console.WriteByte(115); console.WriteByte(105); console.WriteByte(111); console.WriteByte(110); console.WriteByte(61); console.WriteByte(49); console.WriteByte(32); console.WriteByte(104); console.WriteByte(111); console.WriteByte(115); console.WriteByte(116); console.WriteByte(67); console.WriteByte(111); console.WriteByte(110); console.WriteByte(102); console.WriteByte(111); console.WriteByte(114); console.WriteByte(109); console.WriteByte(97); console.WriteByte(110); console.WriteByte(99); console.WriteByte(101); console.WriteByte(61); console.WriteByte(102); console.WriteByte(97); console.WriteByte(108); console.WriteByte(115); console.WriteByte(101); console.WriteByte(32); console.WriteByte(103); console.WriteByte(117); console.WriteByte(101); console.WriteByte(115); console.WriteByte(116); console.WriteByte(73); console.WriteByte(110); console.WriteByte(116); console.WriteByte(101); console.WriteByte(103); console.WriteByte(114); console.WriteByte(97); console.WriteByte(116); console.WriteByte(101); console.WriteByte(100); console.WriteByte(61); console.WriteByte(116); console.WriteByte(114); console.WriteByte(117); console.WriteByte(101); console.WriteByte(32); console.WriteByte(117); console.WriteByte(110); console.WriteByte(119); console.WriteByte(105); console.WriteByte(110); console.WriteByte(100); console.WriteByte(69); console.WriteByte(120); console.WriteByte(101); console.WriteByte(99); console.WriteByte(117); console.WriteByte(116); console.WriteByte(105); console.WriteByte(111); console.WriteByte(110); console.WriteByte(61); console.WriteByte(102); console.WriteByte(97); console.WriteByte(108); console.WriteByte(115); console.WriteByte(101); break;
            // TEST_PASS m04.020
            case 3: console.WriteByte(84); console.WriteByte(69); console.WriteByte(83); console.WriteByte(84); console.WriteByte(95); console.WriteByte(80); console.WriteByte(65); console.WriteByte(83); console.WriteByte(83); console.WriteByte(32); console.WriteByte(109); console.WriteByte(48); console.WriteByte(52); console.WriteByte(46); console.WriteByte(48); console.WriteByte(50); console.WriteByte(48); break;
            // CLROS_M04_020_DATA stage=
            case 4: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(50); console.WriteByte(48); console.WriteByte(95); console.WriteByte(68); console.WriteByte(65); console.WriteByte(84); console.WriteByte(65); console.WriteByte(32); console.WriteByte(115); console.WriteByte(116); console.WriteByte(97); console.WriteByte(103); console.WriteByte(101); console.WriteByte(61); break;
            //  a=
            case 5: console.WriteByte(32); console.WriteByte(97); console.WriteByte(61); break;
            //  b=
            case 6: console.WriteByte(32); console.WriteByte(98); console.WriteByte(61); break;
            //  c=
            case 7: console.WriteByte(32); console.WriteByte(99); console.WriteByte(61); break;
            //  d=
            case 8: console.WriteByte(32); console.WriteByte(100); console.WriteByte(61); break;
            //  status=
            case 9: console.WriteByte(32); console.WriteByte(115); console.WriteByte(116); console.WriteByte(97); console.WriteByte(116); console.WriteByte(117); console.WriteByte(115); console.WriteByte(61); break;
        }
    }
}
#endif
