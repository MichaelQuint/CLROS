#if M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE || TYPE_MANAGER_CAPTURE_HOST
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// One caller-owned static buffer, one original M01 authority, one core decoder.
// The numeric entry is also exercised on genuinely mapped controlled host storage.
internal static unsafe class NativeAotModuleRegistrationProbe
{
    internal const ulong Capacity = 4096;
    internal const ulong GuardStart = 4064;
    internal struct Buffer { internal fixed ulong Words[512]; }
    internal struct Observation
    {
        internal ulong Image, ImageBytes, Required, Storage, Slot, Handle;
        internal NativeAotTypeManagerAbi Abi;
        internal TypeManagerModuleResult Thread, Indirection, Empty, Absent;
        internal TypeManagerModuleStatus Short, Repeat, Foreign;
        internal ulong Failure;
    }
    private static Buffer s_storage;
    private static bool s_attempted;

    // No reset, seeding or pointer write occurs before core registration. The
    // caller supplies this exact live static buffer (or mapped host fixture).
    internal static bool Execute(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* storage, ulong capacity,
        out Observation observation)
    {
        observation = default;
        if (capacity != Capacity || storage == null || ((ulong)storage & 7) != 0 ||
            (ulong)storage > ~0UL - capacity)
            return Reject(ref observation, 1);
        TypeManagerModuleResult valid = TypeManagerModuleMetadataLiveNativeAOTImage.Validate(capture, mapping);
        if (!valid.Succeeded || valid.RequiredStorage == 0 || valid.RequiredStorage > GuardStart)
            return Reject(ref observation, 2);
        // Mapping/lifetime of the declared buffer is a caller obligation. Validate
        // is image-only; its result is not claimed as retained-storage admission.
        if (!Zero(storage, 0, capacity)) return Reject(ref observation, 3);
        observation.Required = valid.RequiredStorage;
        observation.Storage = (ulong)storage;
        if (mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out observation.Image) != AddressValueStatus.Success)
            return Reject(ref observation, 4);
        observation.ImageBytes = capture.Range.ImageSize;
        TypeManagerModuleResult shortResult = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(
            capture, mapping, storage, valid.RequiredStorage - 1);
        observation.Short = shortResult.Status;
        if (shortResult.Status != TypeManagerModuleStatus.StorageRejected || !Zero(storage, 0, capacity))
            return Reject(ref observation, 5);

        TypeManagerModuleResult registered = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(
            capture, mapping, storage, capacity);
        if (!registered.Succeeded || registered.RequiredStorage != valid.RequiredStorage)
            return Reject(ref observation, 6);
        observation.Handle = registered.Handle;
        // Success admitted the full capacity. These are post-registration tail
        // canaries, not pre-registration or external guards.
        if (!Zero(storage, valid.RequiredStorage, capacity - valid.RequiredStorage))
            return Reject(ref observation, 7);
        for (ulong i = GuardStart; i < Capacity; i++) storage[i] = Canary(i);
        if (!Preserved(storage, valid.RequiredStorage)) return Reject(ref observation, 8);

        TypeManagerModuleResult slot = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, storage, capacity, registered.Handle, 204);
        if (!slot.Succeeded || slot.Length != 8 || slot.Section == 0)
            return Reject(ref observation, 9);
        observation.Slot = slot.Section;
        ulong consumerHandle = ReadWord(slot.Section);
        if (consumerHandle != registered.Handle || ReadWord(slot.Section + 8) != 0)
            return Reject(ref observation, 10);
        observation.Abi = ReadAbi(consumerHandle);
        if (observation.Abi.OsModule != observation.Image || observation.Abi.ReadyToRunHeader == 0 ||
            observation.Abi.ClassLibraryFunctions != 0 || observation.Abi.ClassLibraryFunctionCount != 0 ||
            observation.Abi.Padding != 0)
            return Reject(ref observation, 11);
        observation.Thread = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, storage, capacity, consumerHandle, 202);
        observation.Indirection = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, storage, capacity, consumerHandle, 204);
        observation.Empty = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, storage, capacity, consumerHandle, 213);
        observation.Absent = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, storage, capacity, consumerHandle, 0);
        if (!observation.Thread.Succeeded || observation.Thread.Length != 8 ||
            observation.Thread.Section != observation.Abi.ThreadStaticRegion ||
            !observation.Indirection.Succeeded || observation.Indirection.Section != slot.Section ||
            observation.Indirection.Length != 8 || !observation.Empty.Succeeded || observation.Empty.Length != 0 ||
            observation.Absent.Status != TypeManagerModuleStatus.SectionAbsent ||
            observation.Absent.Section != 0 || observation.Absent.Length != 0 ||
            !Preserved(storage, valid.RequiredStorage))
            return Reject(ref observation, 12);
        observation.Repeat = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(
            capture, mapping, storage, capacity).Status;
        observation.Foreign = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, storage, capacity, 0, 204).Status;
        TypeManagerModuleResult retained = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, storage, capacity, ReadWord(slot.Section), 204);
        if (observation.Repeat != TypeManagerModuleStatus.AlreadyRegistered ||
            observation.Foreign != TypeManagerModuleStatus.RegistrationChanged ||
            !retained.Succeeded || retained.Section != slot.Section || retained.Length != 8 ||
            ReadWord(slot.Section) != consumerHandle || ReadWord(slot.Section + 8) != 0 ||
            !Preserved(storage, valid.RequiredStorage))
            return Reject(ref observation, 13);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ulong ReadWord(ulong address) => *(ulong*)address;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static NativeAotTypeManagerAbi ReadAbi(ulong address)
    {
        NativeAotTypeManagerAbi value = default;
        value.OsModule = ReadWord(address);
        value.ReadyToRunHeader = ReadWord(address + 8);
        value.GcStaticRegion = ReadWord(address + 16);
        value.ThreadStaticRegion = ReadWord(address + 24);
        value.ClassLibraryFunctions = ReadWord(address + 32);
        ulong last = ReadWord(address + 40);
        value.ClassLibraryFunctionCount = (uint)last;
        value.Padding = (uint)(last >> 32);
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Zero(byte* p, ulong start, ulong count)
    {
        for (ulong i = 0; i < count; i++) if (p[start + i] != 0) return false;
        return true;
    }
    private static byte Canary(ulong offset) => offset < 4080 ? (byte)0xA5 : (byte)0x5A;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Preserved(byte* storage, ulong required)
    {
        if (required > GuardStart || !Zero(storage, required, GuardStart - required)) return false;
        for (ulong i = GuardStart; i < Capacity; i++) if (storage[i] != Canary(i)) return false;
        return true;
    }
    private static bool Reject(ref Observation observation, ulong reason)
    { observation.Failure = reason; return false; }

    internal static bool Run(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, EfiConsoleAdapter console)
    {
        Token(console, 0); console.WriteCrLf();
        if (s_attempted) { Token(console, 1); Number(console, 14); console.WriteCrLf(); return false; }
        s_attempted = true;
        fixed (Buffer* buffer = &s_storage)
        {
            if (!Execute(capture, mapping, (byte*)buffer->Words, Capacity, out Observation o))
            { Token(console, 1); Number(console, o.Failure); console.WriteCrLf(); return false; }
            Token(console, 2); Number(console, o.Image); Token(console, 3); Number(console, o.ImageBytes); console.WriteCrLf();
            Token(console, 4); Number(console, o.Required); Token(console, 5); Number(console, o.Storage); Token(console, 6); console.WriteCrLf();
            Token(console, 7); console.WriteCrLf();
            Token(console, 8); Number(console, o.Slot); Token(console, 9); Number(console, o.Handle);
            Token(console, 10); Number(console, o.Abi.OsModule); Token(console, 11); Number(console, o.Abi.ReadyToRunHeader);
            Token(console, 12); Number(console, o.Abi.GcStaticRegion); Token(console, 13); Number(console, o.Abi.ThreadStaticRegion);
            Token(console, 14); console.WriteCrLf();
            Section(console, 202, o.Thread); Section(console, 204, o.Indirection);
            Section(console, 213, o.Empty); Section(console, 0, o.Absent);
            Token(console, 15); console.WriteCrLf(); Token(console, 16); console.WriteCrLf();
            Token(console, 17); console.WriteCrLf(); Token(console, 18); console.WriteCrLf();
#if M04_X64_UNWIND_METADATA_PROBE
            if (!X64UnwindMetadataProbe.Run(capture, mapping, (byte*)buffer->Words,
                    Capacity, o.Handle, console)) return false;
            if (!Preserved((byte*)buffer->Words, o.Required))
            { Token(console, 1); Number(console, 15); console.WriteCrLf(); return false; }
#endif
            return true;
        }
    }
    private static void Section(EfiConsoleAdapter console, ulong id, TypeManagerModuleResult result)
    {
        Token(console, 19); Number(console, id); Token(console, 20); Number(console, (ulong)result.Status);
        Token(console, 21); Number(console, result.Section); Token(console, 22); Number(console, (ulong)result.Length); console.WriteCrLf();
    }
    private static void Number(EfiConsoleAdapter console, ulong value)
    {
        ulong divisor = 1;
        while (divisor <= value / 10) divisor *= 10;
        do { console.WriteByte((byte)(48 + value / divisor)); value %= divisor; divisor /= 10; } while (divisor != 0);
    }
    // Token body generated from the literal reviewed transcript; no strings,
    // delegate, managed object or static reference is needed in the EFI image.
    private static void Token(EfiConsoleAdapter console, uint token)
    {
        switch (token)
        {
            case 0: console.WriteByte(84); console.WriteByte(69); console.WriteByte(83); console.WriteByte(84); console.WriteByte(95); console.WriteByte(83); console.WriteByte(84); console.WriteByte(65); console.WriteByte(82); console.WriteByte(84); console.WriteByte(32); console.WriteByte(109); console.WriteByte(48); console.WriteByte(52); console.WriteByte(46); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); break;
            case 1: console.WriteByte(84); console.WriteByte(69); console.WriteByte(83); console.WriteByte(84); console.WriteByte(95); console.WriteByte(70); console.WriteByte(65); console.WriteByte(73); console.WriteByte(76); console.WriteByte(32); console.WriteByte(109); console.WriteByte(48); console.WriteByte(52); console.WriteByte(46); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(32); console.WriteByte(114); console.WriteByte(101); console.WriteByte(97); console.WriteByte(115); console.WriteByte(111); console.WriteByte(110); console.WriteByte(61); break;
            case 2: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(95); console.WriteByte(73); console.WriteByte(77); console.WriteByte(65); console.WriteByte(71); console.WriteByte(69); console.WriteByte(32); console.WriteByte(98); console.WriteByte(97); console.WriteByte(115); console.WriteByte(101); console.WriteByte(61); break;
            case 3: console.WriteByte(32); console.WriteByte(98); console.WriteByte(121); console.WriteByte(116); console.WriteByte(101); console.WriteByte(115); console.WriteByte(61); break;
            case 4: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(95); console.WriteByte(86); console.WriteByte(65); console.WriteByte(76); console.WriteByte(73); console.WriteByte(68); console.WriteByte(65); console.WriteByte(84); console.WriteByte(69); console.WriteByte(32); console.WriteByte(114); console.WriteByte(101); console.WriteByte(113); console.WriteByte(117); console.WriteByte(105); console.WriteByte(114); console.WriteByte(101); console.WriteByte(100); console.WriteByte(61); break;
            case 5: console.WriteByte(32); console.WriteByte(115); console.WriteByte(116); console.WriteByte(111); console.WriteByte(114); console.WriteByte(97); console.WriteByte(103); console.WriteByte(101); console.WriteByte(61); break;
            case 6: console.WriteByte(32); console.WriteByte(99); console.WriteByte(97); console.WriteByte(112); console.WriteByte(97); console.WriteByte(99); console.WriteByte(105); console.WriteByte(116); console.WriteByte(121); console.WriteByte(61); console.WriteByte(52); console.WriteByte(48); console.WriteByte(57); console.WriteByte(54); break;
            case 7: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(95); console.WriteByte(82); console.WriteByte(69); console.WriteByte(74); console.WriteByte(69); console.WriteByte(67); console.WriteByte(84); console.WriteByte(32); console.WriteByte(107); console.WriteByte(105); console.WriteByte(110); console.WriteByte(100); console.WriteByte(61); console.WriteByte(99); console.WriteByte(97); console.WriteByte(112); console.WriteByte(97); console.WriteByte(99); console.WriteByte(105); console.WriteByte(116); console.WriteByte(121); console.WriteByte(32); console.WriteByte(115); console.WriteByte(116); console.WriteByte(97); console.WriteByte(116); console.WriteByte(117); console.WriteByte(115); console.WriteByte(61); console.WriteByte(55); console.WriteByte(32); console.WriteByte(117); console.WriteByte(110); console.WriteByte(99); console.WriteByte(104); console.WriteByte(97); console.WriteByte(110); console.WriteByte(103); console.WriteByte(101); console.WriteByte(100); console.WriteByte(61); console.WriteByte(116); console.WriteByte(114); console.WriteByte(117); console.WriteByte(101); break;
            case 8: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(95); console.WriteByte(82); console.WriteByte(69); console.WriteByte(71); console.WriteByte(73); console.WriteByte(83); console.WriteByte(84); console.WriteByte(69); console.WriteByte(82); console.WriteByte(32); console.WriteByte(115); console.WriteByte(108); console.WriteByte(111); console.WriteByte(116); console.WriteByte(61); break;
            case 9: console.WriteByte(32); console.WriteByte(104); console.WriteByte(97); console.WriteByte(110); console.WriteByte(100); console.WriteByte(108); console.WriteByte(101); console.WriteByte(61); break;
            case 10: console.WriteByte(32); console.WriteByte(111); console.WriteByte(115); console.WriteByte(77); console.WriteByte(111); console.WriteByte(100); console.WriteByte(117); console.WriteByte(108); console.WriteByte(101); console.WriteByte(61); break;
            case 11: console.WriteByte(32); console.WriteByte(104); console.WriteByte(101); console.WriteByte(97); console.WriteByte(100); console.WriteByte(101); console.WriteByte(114); console.WriteByte(61); break;
            case 12: console.WriteByte(32); console.WriteByte(103); console.WriteByte(99); console.WriteByte(61); break;
            case 13: console.WriteByte(32); console.WriteByte(116); console.WriteByte(108); console.WriteByte(115); console.WriteByte(61); break;
            case 14: console.WriteByte(32); console.WriteByte(115); console.WriteByte(108); console.WriteByte(111); console.WriteByte(116); console.WriteByte(66); console.WriteByte(121); console.WriteByte(116); console.WriteByte(101); console.WriteByte(115); console.WriteByte(61); console.WriteByte(49); console.WriteByte(54); console.WriteByte(32); console.WriteByte(105); console.WriteByte(110); console.WriteByte(100); console.WriteByte(101); console.WriteByte(120); console.WriteByte(61); console.WriteByte(48); console.WriteByte(32); console.WriteByte(112); console.WriteByte(97); console.WriteByte(100); console.WriteByte(100); console.WriteByte(105); console.WriteByte(110); console.WriteByte(103); console.WriteByte(61); console.WriteByte(48); console.WriteByte(32); console.WriteByte(97); console.WriteByte(98); console.WriteByte(105); console.WriteByte(66); console.WriteByte(121); console.WriteByte(116); console.WriteByte(101); console.WriteByte(115); console.WriteByte(61); console.WriteByte(52); console.WriteByte(56); console.WriteByte(32); console.WriteByte(99); console.WriteByte(108); console.WriteByte(97); console.WriteByte(115); console.WriteByte(115); console.WriteByte(108); console.WriteByte(105); console.WriteByte(98); console.WriteByte(61); console.WriteByte(48); console.WriteByte(32); console.WriteByte(99); console.WriteByte(108); console.WriteByte(97); console.WriteByte(115); console.WriteByte(115); console.WriteByte(108); console.WriteByte(105); console.WriteByte(98); console.WriteByte(67); console.WriteByte(111); console.WriteByte(117); console.WriteByte(110); console.WriteByte(116); console.WriteByte(61); console.WriteByte(48); break;
            case 15: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(95); console.WriteByte(82); console.WriteByte(69); console.WriteByte(74); console.WriteByte(69); console.WriteByte(67); console.WriteByte(84); console.WriteByte(32); console.WriteByte(107); console.WriteByte(105); console.WriteByte(110); console.WriteByte(100); console.WriteByte(61); console.WriteByte(114); console.WriteByte(101); console.WriteByte(112); console.WriteByte(101); console.WriteByte(97); console.WriteByte(116); console.WriteByte(32); console.WriteByte(115); console.WriteByte(116); console.WriteByte(97); console.WriteByte(116); console.WriteByte(117); console.WriteByte(115); console.WriteByte(61); console.WriteByte(57); console.WriteByte(32); console.WriteByte(117); console.WriteByte(110); console.WriteByte(99); console.WriteByte(104); console.WriteByte(97); console.WriteByte(110); console.WriteByte(103); console.WriteByte(101); console.WriteByte(100); console.WriteByte(61); console.WriteByte(116); console.WriteByte(114); console.WriteByte(117); console.WriteByte(101); break;
            case 16: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(95); console.WriteByte(82); console.WriteByte(69); console.WriteByte(74); console.WriteByte(69); console.WriteByte(67); console.WriteByte(84); console.WriteByte(32); console.WriteByte(107); console.WriteByte(105); console.WriteByte(110); console.WriteByte(100); console.WriteByte(61); console.WriteByte(102); console.WriteByte(111); console.WriteByte(114); console.WriteByte(101); console.WriteByte(105); console.WriteByte(103); console.WriteByte(110); console.WriteByte(32); console.WriteByte(115); console.WriteByte(116); console.WriteByte(97); console.WriteByte(116); console.WriteByte(117); console.WriteByte(115); console.WriteByte(61); console.WriteByte(49); console.WriteByte(48); console.WriteByte(32); console.WriteByte(117); console.WriteByte(110); console.WriteByte(99); console.WriteByte(104); console.WriteByte(97); console.WriteByte(110); console.WriteByte(103); console.WriteByte(101); console.WriteByte(100); console.WriteByte(61); console.WriteByte(116); console.WriteByte(114); console.WriteByte(117); console.WriteByte(101); break;
            case 17: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(95); console.WriteByte(82); console.WriteByte(69); console.WriteByte(65); console.WriteByte(68); console.WriteByte(89); console.WriteByte(32); console.WriteByte(109); console.WriteByte(111); console.WriteByte(100); console.WriteByte(117); console.WriteByte(108); console.WriteByte(101); console.WriteByte(115); console.WriteByte(61); console.WriteByte(49); console.WriteByte(32); console.WriteByte(97); console.WriteByte(98); console.WriteByte(105); console.WriteByte(66); console.WriteByte(121); console.WriteByte(116); console.WriteByte(101); console.WriteByte(115); console.WriteByte(61); console.WriteByte(52); console.WriteByte(56); console.WriteByte(32); console.WriteByte(115); console.WriteByte(108); console.WriteByte(111); console.WriteByte(116); console.WriteByte(66); console.WriteByte(121); console.WriteByte(116); console.WriteByte(101); console.WriteByte(115); console.WriteByte(61); console.WriteByte(49); console.WriteByte(54); console.WriteByte(32); console.WriteByte(114); console.WriteByte(101); console.WriteByte(103); console.WriteByte(105); console.WriteByte(115); console.WriteByte(116); console.WriteByte(101); console.WriteByte(114); console.WriteByte(101); console.WriteByte(100); console.WriteByte(61); console.WriteByte(116); console.WriteByte(114); console.WriteByte(117); console.WriteByte(101); console.WriteByte(32); console.WriteByte(99); console.WriteByte(111); console.WriteByte(110); console.WriteByte(115); console.WriteByte(117); console.WriteByte(109); console.WriteByte(101); console.WriteByte(114); console.WriteByte(61); console.WriteByte(116); console.WriteByte(114); console.WriteByte(117); console.WriteByte(101); console.WriteByte(32); console.WriteByte(116); console.WriteByte(97); console.WriteByte(105); console.WriteByte(108); console.WriteByte(67); console.WriteByte(97); console.WriteByte(110); console.WriteByte(97); console.WriteByte(114); console.WriteByte(105); console.WriteByte(101); console.WriteByte(115); console.WriteByte(61); console.WriteByte(116); console.WriteByte(114); console.WriteByte(117); console.WriteByte(101); break;
            case 18: console.WriteByte(84); console.WriteByte(69); console.WriteByte(83); console.WriteByte(84); console.WriteByte(95); console.WriteByte(80); console.WriteByte(65); console.WriteByte(83); console.WriteByte(83); console.WriteByte(32); console.WriteByte(109); console.WriteByte(48); console.WriteByte(52); console.WriteByte(46); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); break;
            case 19: console.WriteByte(67); console.WriteByte(76); console.WriteByte(82); console.WriteByte(79); console.WriteByte(83); console.WriteByte(95); console.WriteByte(77); console.WriteByte(48); console.WriteByte(52); console.WriteByte(95); console.WriteByte(48); console.WriteByte(49); console.WriteByte(49); console.WriteByte(95); console.WriteByte(83); console.WriteByte(69); console.WriteByte(67); console.WriteByte(84); console.WriteByte(73); console.WriteByte(79); console.WriteByte(78); console.WriteByte(32); console.WriteByte(105); console.WriteByte(100); console.WriteByte(61); break;
            case 20: console.WriteByte(32); console.WriteByte(115); console.WriteByte(116); console.WriteByte(97); console.WriteByte(116); console.WriteByte(117); console.WriteByte(115); console.WriteByte(61); break;
            case 21: console.WriteByte(32); console.WriteByte(115); console.WriteByte(116); console.WriteByte(97); console.WriteByte(114); console.WriteByte(116); console.WriteByte(61); break;
            case 22: console.WriteByte(32); console.WriteByte(108); console.WriteByte(101); console.WriteByte(110); console.WriteByte(103); console.WriteByte(116); console.WriteByte(104); console.WriteByte(61); break;
        }
    }
}
#endif
