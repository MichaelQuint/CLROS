using OS.Boot;

namespace OS.Kernel.Runtime;
/// <summary>
/// Exclusive, one-image decoder for the pinned PE32+/AMD64 version-one unwind format.
/// Caller retains mapped/readable original image/module storage and writable private storage.
/// Numeric admission is not a mapping probe. Bind snapshots only the directory and decoded
/// unwind prefixes; lookup revalidates the original owner and every retained byte. Storage
/// cannot move, be shared concurrently, or outlive its original registration. Handler data
/// has no format-independent length: only its start is reported, never admitted as a payload.
/// No stack walk, epilog decoding, register restoration, language EH or GC scan is performed.
/// </summary>

public static unsafe partial class X64PERuntimeFunctionUnwindMetadataHostileInput
{
    public const uint MaximumFunctions = 2048;
    public const uint MaximumChainDepth = 32;
    private const ulong Bound = 0x4D30343032305531;
    public static ulong StateBytes => (ulong)sizeof(State);

    public static X64UnwindMetadataResult Validate(EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle)
    {
        X64UnwindMetadataStatus status = Open(capture, mapping, moduleOwner, moduleCapacity, moduleHandle, out Image image);
        if (status != X64UnwindMetadataStatus.Success)
            return Failure(status);
        status = Scan(image, null, 0, 0, out ulong required);
        return status == X64UnwindMetadataStatus.Success ? Summary(image, required) : Failure(status);
    }

    public static X64UnwindMetadataResult Bind(EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle, byte* storage, ulong capacity)
    {
        X64UnwindMetadataStatus status = Open(capture, mapping, moduleOwner, moduleCapacity, moduleHandle, out Image image);
        if (status != X64UnwindMetadataStatus.Success)
            return Failure(status);
        status = Scan(image, null, 0, 0, out ulong required);
        if (status != X64UnwindMetadataStatus.Success)
            return Failure(status);
        status = AdmitStorage(image, storage, capacity, required);
        if (status != X64UnwindMetadataStatus.Success)
            return Failure(status);
        for (ulong i = 0; i < required; i++)
            if (storage[i] != 0)
                return Failure(X64UnwindMetadataStatus.AlreadyBound);
        status = Scan(image, storage, required, 1, out ulong written);
        if (status != X64UnwindMetadataStatus.Success || written != required)
        {
            // The admitted prefix was initially zero; no usable partial binding survives.
            for (ulong i = 0; i < required; i++)
                storage[i] = 0;
            return Failure(status == X64UnwindMetadataStatus.Success ? X64UnwindMetadataStatus.MetadataChanged : status);
        }

        State candidate = default;
        candidate.Self = (ulong)storage;
        candidate.Capacity = capacity;
        candidate.Required = required;
        candidate.Image = image;
        *(State*)storage = candidate;
        ((State*)storage)->Phase = Bound;
        return Summary(image, required);
    }

    /// <summary>Absolute active PC, begin-inclusive/end-exclusive. NotFound does not prove a leaf frame.</summary>

    public static X64UnwindMetadataResult Lookup(byte* storage, ulong capacity, ulong instructionPointer) => LookupCore(storage, capacity, instructionPointer, false);
    /// <summary>
    /// Zero-based listed function after original-owner and full-snapshot
    /// revalidation. An out-of-range index returns NotFound with no payload.
    /// </summary>

    public static X64UnwindMetadataResult LookupFunction(byte* storage, ulong capacity, uint functionIndex) => LookupCore(storage, capacity, functionIndex, true);
    private static X64UnwindMetadataResult LookupCore(byte* storage, ulong capacity, ulong key, bool indexed)
    {
        if (!Span((ulong)storage, capacity) || ((ulong)storage & 7) != 0 || capacity < StateBytes)
            return Failure(X64UnwindMetadataStatus.StorageRejected);
        State state = *(State*)storage;
        if (state.Phase != Bound || state.Self != (ulong)storage || state.Capacity != capacity || state.Required < StateBytes || state.Required > capacity)
            return Failure(X64UnwindMetadataStatus.NotBound);
        Image original = state.Image;
        X64UnwindMetadataStatus status = Open(original.Capture, original.Mapping, original.Owner, original.OwnerCapacity, original.Handle, out Image image);
        if (status != X64UnwindMetadataStatus.Success)
            return Failure(status);
        if (original.Base != image.Base || original.Bytes != image.Bytes || original.Directory != image.Directory || original.DirectoryBytes != image.DirectoryBytes || original.Count != image.Count)
            return Failure(X64UnwindMetadataStatus.MetadataChanged);
        status = AdmitStorage(image, storage, capacity, state.Required);
        if (status != X64UnwindMetadataStatus.Success)
            return Failure(status);
        status = Scan(image, storage, state.Required, 2, out ulong consumed);
        if (status != X64UnwindMetadataStatus.Success)
            return Failure(status);
        if (consumed != state.Required)
            return Failure(X64UnwindMetadataStatus.MetadataChanged);
        byte* directory = storage + StateBytes;
        uint index = image.Count;
        if (indexed)
        {
            if (key >= image.Count)
                return Failure(X64UnwindMetadataStatus.NotFound);
            index = (uint)key;
        }
        else
        {
            if (key < image.Base || key - image.Base >= image.Bytes)
                return Failure(X64UnwindMetadataStatus.NotFound);
            uint pc = (uint)(key - image.Base), lo = 0, hi = image.Count;
            while (lo < hi)
            {
                uint mid = lo + (hi - lo) / 2;
                byte* row = directory + mid * 12;
                uint begin = U32(row), end = U32(row + 4);
                if (pc < begin)
                    hi = mid;
                else if (pc >= end)
                    lo = mid + 1;
                else
                {
                    index = mid;
                    break;
                }
            }

            if (index == image.Count)
                return Failure(X64UnwindMetadataStatus.NotFound);
        }

        byte* selected = directory + index * 12;
        ulong ignored = 0;
        status = Walk(image, U32(selected), U32(selected + 4), U32(selected + 8), null, 0, 0, ref ignored, out var result);
        if (status != X64UnwindMetadataStatus.Success)
            return Failure(status);
        result.RequiredStorage = state.Required;
        result.FunctionCount = image.Count;
        result.FunctionIndex = index;
        result.ImageBase = image.Base;
        result.ModuleOwner = (ulong)image.Owner;
        result.ModuleHandle = image.Handle;
        result.Status = X64UnwindMetadataStatus.Success;
        return result;
    }

    private static X64UnwindMetadataStatus Open(EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, out Image image)
    {
        image = default;
        if (!TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(capture, mapping, owner, ownerCapacity, handle, 204).Succeeded || mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong address) != AddressValueStatus.Success)
            return X64UnwindMetadataStatus.ModuleRejected;
        image.Capture = capture;
        image.Mapping = mapping;
        image.Owner = owner;
        image.OwnerCapacity = ownerCapacity;
        image.Handle = handle;
        image.Base = address;
        image.Bytes = capture.Range.ImageSize;
        // The retained TypeManager has already authenticated these complete PE headers.
        byte* pe = (byte*)address;
        uint nt = U32(pe + 60);
        byte* optional = pe + nt + 24;
        uint optionalBytes = U16(pe + nt + 20);
        uint directories = U32(optional + 108);
        if (directories > (optionalBytes - 112) / 8)
            return X64UnwindMetadataStatus.DirectoryRejected;
        if (directories < 4)
            return X64UnwindMetadataStatus.DirectoryAbsent;
        if (optionalBytes < 144)
            return X64UnwindMetadataStatus.DirectoryRejected;
        image.Directory = U32(optional + 136);
        image.DirectoryBytes = U32(optional + 140);
        if (image.Directory == 0 && image.DirectoryBytes == 0)
            return X64UnwindMetadataStatus.DirectoryAbsent;
        if (image.Directory == 0 || (image.Directory & 3) != 0 || image.DirectoryBytes == 0 || image.DirectoryBytes % 12 != 0 || !Metadata(image, image.Directory, image.DirectoryBytes))
            return X64UnwindMetadataStatus.DirectoryRejected;
        image.Count = image.DirectoryBytes / 12;
        return image.Count > MaximumFunctions ? X64UnwindMetadataStatus.CapacityExceeded : X64UnwindMetadataStatus.Success;
    }

    // mode 0 measures, 1 copies, 2 compares. The compact snapshot deliberately
    // repeats shared unwind prefixes instead of introducing a mutable global cache.
    private static X64UnwindMetadataStatus Scan(Image image, byte* storage, ulong capacity, byte mode, out ulong used)
    {
        used = StateBytes;
        if (!Transfer((byte*)(image.Base + image.Directory), image.DirectoryBytes, storage, capacity, mode, ref used))
            return X64UnwindMetadataStatus.MetadataChanged;
        uint previousEnd = 0;
        for (uint i = 0; i < image.Count; i++)
        {
            byte* row = (byte*)(image.Base + image.Directory + i * 12);
            uint begin = U32(row), end = U32(row + 4), unwind = U32(row + 8);
            if (begin < previousEnd || !Function(image, begin, end))
                return X64UnwindMetadataStatus.FunctionRejected;
            previousEnd = end;
            X64UnwindMetadataStatus status = Walk(image, begin, end, unwind, storage, capacity, mode, ref used, out _);
            if (status != X64UnwindMetadataStatus.Success)
                return status;
        }

        return X64UnwindMetadataStatus.Success;
    }

    private static X64UnwindMetadataStatus Walk(Image image, uint begin, uint end, uint unwind, byte* storage, ulong capacity, byte mode, ref ulong used, out X64UnwindMetadataResult result)
    {
        result = default;
        result.BeginRva = begin;
        result.EndRva = end;
        result.UnwindRva = unwind;
        uint* visited = stackalloc uint[(int)MaximumChainDepth];
        byte expectedFrame = 0;
        for (uint depth = 0; depth < MaximumChainDepth; depth++)
        {
            for (uint i = 0; i < depth; i++)
                if (visited[i] == unwind)
                    return X64UnwindMetadataStatus.ChainCycle;
            visited[depth] = unwind;
            if (unwind == 0 || (unwind & 3) != 0 || !Metadata(image, unwind, 4))
                return X64UnwindMetadataStatus.UnwindRejected;
            byte* info = (byte*)(image.Base + unwind);
            byte version = (byte)(info[0] & 7), flags = (byte)(info[0] >> 3), prolog = info[1], count = info[2], frame = info[3];
            if (version != 1)
                return X64UnwindMetadataStatus.UnsupportedFormat;
            if (flags > 4 || ((flags & 4) != 0 && (flags & 3) != 0) || prolog > end - begin || ((frame & 15) == 0 ? frame != 0 : !Nonvolatile((uint)(frame & 15))))
                return X64UnwindMetadataStatus.UnwindRejected;
            if (depth == 0)
            {
                expectedFrame = frame;
                result.PrologBytes = prolog;
                result.CodeSlots = count;
                result.FrameRegister = (byte)(frame & 15);
                result.FrameOffset = (byte)(frame >> 4);
                result.Flags = flags;
            }
            else if (frame != expectedFrame)
                return X64UnwindMetadataStatus.UnwindRejected;
            uint prefix = 4U + (((uint)count + 1) & ~1U) * 2;
            uint bytes = prefix + ((flags & 4) != 0 ? 12U : (flags & 3) != 0 ? 4U : 0);
            if (!Metadata(image, unwind, bytes) || Overlap(unwind, bytes, image.Directory, image.DirectoryBytes))
                return X64UnwindMetadataStatus.UnwindRejected;
            X64UnwindMetadataStatus status = Codes(info, count, prolog, frame, (flags & 4) != 0, out uint allocation, out uint stack);
            if (status != X64UnwindMetadataStatus.Success)
                return status;
            if (!Transfer(info, bytes, storage, capacity, mode, ref used))
                return X64UnwindMetadataStatus.MetadataChanged;
            if ((flags & 4) == 0)
            {
                result.PrimaryUnwindRva = unwind;
                result.PrimaryFlags = flags;
                result.FixedAllocationBytes = allocation;
                result.StackBytes = stack;
                result.ChainDepth = depth;
                if ((flags & 3) != 0)
                {
                    uint handler = U32(info + prefix);
                    if (handler == 0 || !Executable(image, handler, 1))
                        return X64UnwindMetadataStatus.UnwindRejected;
                    result.HandlerRva = handler;
                    result.LanguageDataStartRva = unwind + bytes;
                }

                return X64UnwindMetadataStatus.Success;
            }

            byte* chained = info + prefix;
            begin = U32(chained);
            end = U32(chained + 4);
            unwind = U32(chained + 8);
            // A chain is a same-image runtime-function identity, never an absolute
            // pointer or an instruction address borrowed from another image.
            if (!Function(image, begin, end) || !Listed(image, begin, end, unwind))
                return X64UnwindMetadataStatus.FunctionRejected;
        }

        return X64UnwindMetadataStatus.CapacityExceeded;
    }

    private static X64UnwindMetadataStatus Codes(byte* info, uint count, byte prolog, byte frame, bool chained, out uint allocation, out uint stack)
    {
        allocation = 0;
        stack = 0;
        uint previous = 256, frameOffset = 0, frameSets = 0, firstSave = 256;
        bool pushes = false, machine = false;
        for (uint i = 0; i < count;)
        {
            byte* code = info + 4 + i * 2;
            uint offset = code[0], op = (uint)(code[1] & 15), operand = (uint)(code[1] >> 4), slots = 1;
            if (offset > prolog || offset > previous || (offset == 0 && op != 10))
                return X64UnwindMetadataStatus.UnwindRejected;
            previous = offset;
            if (op == 1)
                slots = operand == 0 ? 2U : operand == 1 ? 3U : 0;
            else if (op == 4 || op == 8)
                slots = 2;
            else if (op == 5 || op == 9)
                slots = 3;
            if (slots == 0 || slots > count - i)
                return X64UnwindMetadataStatus.UnwindRejected;
            if (chained && op != 4 && op != 5 && op != 8 && op != 9)
                return X64UnwindMetadataStatus.UnsupportedFormat; // secondary shrink-wrap cannot change RSP/FP.
            if (pushes && op != 0 && op != 10)
                return X64UnwindMetadataStatus.UnwindRejected;
            ulong amount = 0;
            switch (op)
            {
                case 0:
                    if (!Nonvolatile(operand))
                        return X64UnwindMetadataStatus.UnwindRejected;
                    pushes = true;
                    amount = 8;
                    break;
                case 1:
                    amount = operand == 0 ? (ulong)U16(code + 2) * 8 : U32(code + 2);
                    if ((amount & 7) != 0 || (operand == 0 ? amount < 136 : amount < 524288) || amount > 0xFFFFFFF8UL)
                        return X64UnwindMetadataStatus.UnwindRejected;
                    break;
                case 2:
                    amount = operand * 8 + 8;
                    break;
                case 3:
                    if (operand != 0 || frame == 0 || ++frameSets != 1)
                        return X64UnwindMetadataStatus.UnwindRejected;
                    frameOffset = offset;
                    break;
                case 4:
                case 5:
                case 8:
                case 9:
                    if (op <= 5 ? !Nonvolatile(operand) : operand < 6)
                        return X64UnwindMetadataStatus.UnwindRejected;
                    ulong scale = op <= 5 ? 8UL : 16UL;
                    ulong save = slots == 2 ? U16(code + 2) * scale : U32(code + 2);
                    if (save % scale != 0 || save > 0x100000000UL - scale)
                        return X64UnwindMetadataStatus.UnwindRejected;
                    firstSave = offset;
                    break;
                case 10:
                    if (operand > 1 || machine || i + 1 != count)
                        return X64UnwindMetadataStatus.UnwindRejected;
                    machine = true;
                    amount = operand == 0 ? 40UL : 48UL;
                    break;
                default:
                    return X64UnwindMetadataStatus.UnsupportedFormat;
            }

            if (amount > 0xFFFFFFFFUL - stack)
                return X64UnwindMetadataStatus.UnwindRejected;
            stack += (uint)amount;
            if (op == 1 || op == 2)
            {
                if (amount > 0xFFFFFFFFUL - allocation)
                    return X64UnwindMetadataStatus.UnwindRejected;
                allocation += (uint)amount;
            }

            i += slots;
        }

        if (!chained && ((frame == 0 ? frameSets != 0 : frameSets != 1) || (frame != 0 && (firstSave < frameOffset || (uint)(frame >> 4) * 16 > allocation))))
            return X64UnwindMetadataStatus.UnwindRejected;
        return X64UnwindMetadataStatus.Success;
    }

    private static bool Listed(Image image, uint begin, uint end, uint unwind)
    {
        for (uint i = 0; i < image.Count; i++)
        {
            byte* row = (byte*)(image.Base + image.Directory + i * 12);
            if (U32(row) == begin)
                return U32(row + 4) == end && U32(row + 8) == unwind;
        }

        return false;
    }

    private static X64UnwindMetadataStatus AdmitStorage(Image image, byte* storage, ulong capacity, ulong required)
    {
        ulong address = (ulong)storage;
        if (!Span(address, capacity) || (address & 7) != 0 || capacity < required)
            return X64UnwindMetadataStatus.StorageRejected;
        if (Overlap(address, capacity, (ulong)image.Owner, image.OwnerCapacity))
            return X64UnwindMetadataStatus.StorageOverlap;
        if (Overlap(address, capacity, image.Base, image.Bytes) && TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(image.Capture, image.Mapping, image.Owner, image.OwnerCapacity, image.Handle, address, capacity, TypeManagerRegisteredRangeKind.MutableData) != TypeManagerModuleStatus.Success)
            return X64UnwindMetadataStatus.StorageOverlap;
        return X64UnwindMetadataStatus.Success;
    }

    private static bool Transfer(byte* source, ulong bytes, byte* target, ulong capacity, byte mode, ref ulong used)
    {
        if (bytes > ~0UL - used)
            return false;
        if (mode != 0)
        {
            if (used > capacity || bytes > capacity - used)
                return false;
            for (ulong i = 0; i < bytes; i++)
                if (mode == 1)
                    target[used + i] = source[i];
                else if (target[used + i] != source[i])
                    return false;
        }

        used += bytes;
        return true;
    }

    private static bool Function(Image image, uint begin, uint end) => begin < end && Executable(image, begin, end - begin);
    private static bool Metadata(Image image, uint rva, ulong bytes) => Range(image, rva, bytes, TypeManagerRegisteredRangeKind.ImmutableMetadata);
    private static bool Executable(Image image, uint rva, ulong bytes) => Range(image, rva, bytes, TypeManagerRegisteredRangeKind.ExecutableEntry);
    private static bool Range(Image image, uint rva, ulong bytes, TypeManagerRegisteredRangeKind kind) => bytes != 0 && rva < image.Bytes && bytes <= image.Bytes - rva && TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(image.Capture, image.Mapping, image.Owner, image.OwnerCapacity, image.Handle, image.Base + rva, bytes, kind) == TypeManagerModuleStatus.Success;
    private static bool Nonvolatile(uint register) => register == 3 || register == 5 || register == 6 || register == 7 || (register >= 12 && register <= 15);
    private static bool Span(ulong address, ulong bytes) => address != 0 && bytes != 0 && address <= ~0UL - bytes;
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => an != 0 && bn != 0 && (a <= b ? b - a < an : a - b < bn);
    private static ushort U16(byte* p) => (ushort)((uint)p[0] | (uint)p[1] << 8);
    private static uint U32(byte* p) => (uint)p[0] | (uint)p[1] << 8 | (uint)p[2] << 16 | (uint)p[3] << 24;
    private static X64UnwindMetadataResult Failure(X64UnwindMetadataStatus status) => new()
    {
        Status = status
    };
    private static X64UnwindMetadataResult Summary(Image image, ulong required) => new()
    {
        Status = X64UnwindMetadataStatus.Success,
        RequiredStorage = required,
        FunctionCount = image.Count,
        ImageBase = image.Base,
        ModuleOwner = (ulong)image.Owner,
        ModuleHandle = image.Handle
    };
}
