using OS.Boot;

namespace OS.Kernel.Runtime;

public enum TypeManagerModuleStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ImageAuthorityRejected = 2,
    ImageFormatRejected = 3,
    ModuleListRejected = 4,
    MetadataRejected = 5,
    UnsupportedFormat = 6,
    StorageRejected = 7,
    StorageOverlap = 8,
    AlreadyRegistered = 9,
    RegistrationChanged = 10,
    SectionAbsent = 11,
    RangeRejected = 12,
    RangePermissionRejected = 13,
    RangeProtectedOverlap = 14,
    UnsupportedRangeKind = 15
}

/// <summary>Finite registered-image observations; neither kind grants mapping or write authority.</summary>
public enum TypeManagerRegisteredRangeKind : byte
{
    Uninitialized = 0,
    ImmutableMetadata = 1,
    MutableData = 2,
    ReadableSectionData = 3
}

/// <summary>
/// The pinned .NET 10.0.8 x64 TypeManager data layout, not the CLROS owner.
/// Class-library fields stay zero; recording GC/TLS addresses initializes neither.
/// Explicit trailing padding makes all 48 bytes deterministic.
/// </summary>
public struct NativeAotTypeManagerAbi
{
    public ulong OsModule;
    public ulong ReadyToRunHeader;
    public ulong GcStaticRegion;
    public ulong ThreadStaticRegion;
    public ulong ClassLibraryFunctions;
    public uint ClassLibraryFunctionCount;
    public uint Padding;
}

public readonly struct TypeManagerModuleResult
{
    internal TypeManagerModuleResult(TypeManagerModuleStatus status,
        ulong requiredStorage = 0, ulong handle = 0, ulong section = 0, int length = 0)
    {
        Status = status;
        RequiredStorage = requiredStorage;
        Handle = handle;
        Section = section;
        Length = length;
    }

    public TypeManagerModuleStatus Status { get; }
    public ulong RequiredStorage { get; }
    public ulong Handle { get; }
    public ulong Section { get; }
    public int Length { get; }
    public bool Succeeded => Status == TypeManagerModuleStatus.Success;
}

/// <summary>
/// One-module, exclusive-access host contract over an already mapped loaded PE.
/// Unsafe callers retain readable image and writable storage/slot mapping and
/// lifetime obligations. Numeric admission is not an OS mapping probe. The image
/// authority is the original successful M01 capture plus its typed identity map.
/// This does not install the upstream runtime registry or initialize GC, TLS,
/// cctors, module initializers or class-library functions. No callbacks or runtime
/// helper exports are invoked. There is no concurrency or hardware-fault rollback.
/// </summary>
public static unsafe class TypeManagerModuleMetadataLiveNativeAOTImage
{
    public const uint PinnedMajorVersion = 16;
    public const uint PinnedMinorVersion = 0;
    public const ulong SlotBytes = 16;
    public const ulong AbiBytes = 48;
    private const ulong Registered = 0x4D30343031315231;
    private const uint Read = 0x40000000;
    private const uint Write = 0x80000000;
    private const uint Execute = 0x20000000;

    private struct Image
    {
        internal ulong Base, Bytes, Headers, Sections, SectionCount;
        internal ulong Modules, ModuleBytes, Header, TableBytes, Slot, Gc, Tls;
        internal uint Rows;
    }

    // ABI storage follows this private POD. The immutable byte snapshot follows
    // the ABI. No native consumer receives a pointer to this CLROS state.
    private struct State
    {
        internal ulong Phase, Owner, Capacity;
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal Image Image;
    }

    public static ulong StateBytes => (ulong)sizeof(State);

    /// <summary>Read-only validation; no plan object can authorize later writes.</summary>
    public static TypeManagerModuleResult Validate(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping)
    {
        TypeManagerModuleStatus status = Decode(capture, mapping, out Image image);
        return new(status, status == TypeManagerModuleStatus.Success ? Required(image) : 0);
    }

    /// <summary>
    /// Revalidates all inputs; on rejection neither storage nor slot is written.
    /// Storage is an aligned, initially zero, caller-owned retained byte buffer.
    /// Successful publication fills the ABI and index before the handle store.
    /// </summary>
    public static TypeManagerModuleResult RegisterOnce(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* storage, ulong capacity)
    {
        TypeManagerModuleStatus status = Decode(capture, mapping, out Image image);
        if (status != TypeManagerModuleStatus.Success) return new(status);
        status = AdmitStorage(image, storage, capacity);
        if (status != TypeManagerModuleStatus.Success) return new(status, Required(image));
        ulong required = Required(image);
        if (!Zero(storage, required) || !Zero((byte*)image.Slot, SlotBytes))
            return new(TypeManagerModuleStatus.AlreadyRegistered, required);

        // All admission is complete. These writes cannot invoke user code.
        State candidate = default;
        candidate.Phase = Registered;
        candidate.Owner = (ulong)storage;
        candidate.Capacity = capacity;
        candidate.Capture = capture;
        candidate.Image = image;
        NativeAotTypeManagerAbi abi = ExpectedAbi(image);
        byte* snapshot = storage + sizeof(State) + sizeof(NativeAotTypeManagerAbi);
        Copy(snapshot, (byte*)image.Base, image.Headers);
        Copy(snapshot + image.Headers, (byte*)image.Modules, image.ModuleBytes);
        Copy(snapshot + image.Headers + image.ModuleBytes, (byte*)image.Header, image.TableBytes);
        *(State*)storage = candidate;
        NativeAotTypeManagerAbi* handle = (NativeAotTypeManagerAbi*)(storage + sizeof(State));
        *handle = abi;
        *(uint*)(image.Slot + 8) = 0; // Int32 module index, not a second handle.
        *(uint*)(image.Slot + 12) = 0;
        *(ulong*)image.Slot = (ulong)handle; // Publish last; exclusive caller only.
        return new(TypeManagerModuleStatus.Success, required, (ulong)handle);
    }

    /// <summary>
    /// Checks the exact owner, original capture, ABI, slot and retained metadata
    /// before returning a section address. Success with length zero is present;
    /// SectionAbsent is distinct and returns no pointer. Payloads are not decoded.
    /// </summary>
    public static TypeManagerModuleResult TryGetSection(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* storage, ulong capacity,
        ulong handle, int sectionId)
    {
        TypeManagerModuleStatus status = ValidateRegistered(capture, mapping, storage, capacity, handle, out Image image);
        if (status != TypeManagerModuleStatus.Success) return new(status);
        for (uint i = 0; i < image.Rows; i++)
        {
            byte* row = (byte*)image.Header + 16 + i * 24;
            if ((int)U32(row) == sectionId)
                return new(TypeManagerModuleStatus.Success, Required(image), handle,
                    U64(row + 8), U32(row + 4) == 0 ? 8 : (int)(U64(row + 16) - U64(row + 8)));
        }
        return new(TypeManagerModuleStatus.SectionAbsent);
    }

    /// <summary>
    /// Revalidates the original registration before observing section permissions.
    /// Does not read the candidate payload or grant mapping/lifetime/write authority.
    /// Mutable data excludes the entire owner capacity and every registered extent.
    /// </summary>
    public static TypeManagerModuleStatus TryValidateRegisteredRange(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity,
        ulong handle, ulong address, ulong bytes, TypeManagerRegisteredRangeKind kind)
    {
        TypeManagerModuleStatus status = ValidateRegistered(capture, mapping, owner, ownerCapacity, handle, out Image image);
        if (status != TypeManagerModuleStatus.Success) return status;
        if (kind != TypeManagerRegisteredRangeKind.ImmutableMetadata && kind != TypeManagerRegisteredRangeKind.MutableData &&
            kind != TypeManagerRegisteredRangeKind.ReadableSectionData)
            return TypeManagerModuleStatus.UnsupportedRangeKind;
        if (!Span(address, bytes) || address < image.Base || !Within(image.Bytes, address - image.Base, bytes))
            return TypeManagerModuleStatus.RangeRejected;
        if (kind == TypeManagerRegisteredRangeKind.ImmutableMetadata)
            return InSection(image, address, bytes, Read, Write | Execute)
                ? TypeManagerModuleStatus.Success : TypeManagerModuleStatus.RangePermissionRejected;
        if (kind == TypeManagerRegisteredRangeKind.ReadableSectionData)
        {
            // NativeAOT puts the GC-static entry table in writable .data.
            // This permits reads only within a registered row, never writes;
            // consumers retain their own payload snapshot under exclusive access.
            if (!InSection(image, address, bytes, Read, Execute))
                return TypeManagerModuleStatus.RangePermissionRejected;
            for (uint i = 0; i < image.Rows; i++)
            {
                byte* row = (byte*)image.Header + 16 + i * 24;
                ulong start = U64(row + 8);
                ulong length = U32(row + 4) == 0 ? 8 : U64(row + 16) - start;
                if (address >= start && Within(length, address - start, bytes))
                    return TypeManagerModuleStatus.Success;
            }
            return TypeManagerModuleStatus.RangeRejected;
        }
        if (Overlap(address, bytes, (ulong)owner, ownerCapacity) ||
            Overlap(address, bytes, image.Base, image.Headers) ||
            Overlap(address, bytes, image.Modules, image.ModuleBytes) ||
            Overlap(address, bytes, image.Header, image.TableBytes) ||
            Overlap(address, bytes, image.Slot, SlotBytes))
            return TypeManagerModuleStatus.RangeProtectedOverlap;
        for (uint i = 0; i < image.Rows; i++)
        {
            byte* row = (byte*)image.Header + 16 + i * 24;
            ulong length = U32(row + 4) == 0 ? 8 : U64(row + 16) - U64(row + 8);
            if (Overlap(address, bytes, U64(row + 8), length)) return TypeManagerModuleStatus.RangeProtectedOverlap;
        }
        return InSection(image, address, bytes, Read | Write, Execute)
            ? TypeManagerModuleStatus.Success : TypeManagerModuleStatus.RangePermissionRejected;
    }

    private static TypeManagerModuleStatus ValidateRegistered(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* storage, ulong capacity,
        ulong handle, out Image image)
    {
        TypeManagerModuleStatus status = Decode(capture, mapping, out image);
        if (status != TypeManagerModuleStatus.Success) return status;
        status = AdmitStorage(image, storage, capacity);
        if (status != TypeManagerModuleStatus.Success) return status;
        State* state = (State*)storage;
        ulong expectedHandle = (ulong)storage + (ulong)sizeof(State);
        if (handle != expectedHandle || state->Phase != Registered ||
            state->Owner != (ulong)storage || state->Capacity != capacity ||
            !state->Capture.Equals(capture) || !Same(state->Image, image) ||
            U64((byte*)image.Slot) != expectedHandle || U64((byte*)image.Slot + 8) != 0 ||
            !Same(*(NativeAotTypeManagerAbi*)expectedHandle, ExpectedAbi(image)))
            return TypeManagerModuleStatus.RegistrationChanged;
        byte* snapshot = (byte*)expectedHandle + sizeof(NativeAotTypeManagerAbi);
        if (!Equal(snapshot, (byte*)image.Base, image.Headers) ||
            !Equal(snapshot + image.Headers, (byte*)image.Modules, image.ModuleBytes) ||
            !Equal(snapshot + image.Headers + image.ModuleBytes, (byte*)image.Header, image.TableBytes))
            return TypeManagerModuleStatus.RegistrationChanged;
        return TypeManagerModuleStatus.Success;
    }

    private static NativeAotTypeManagerAbi ExpectedAbi(Image image)
    {
        NativeAotTypeManagerAbi abi = default;
        abi.OsModule = image.Base;
        abi.ReadyToRunHeader = image.Header;
        abi.GcStaticRegion = image.Gc;
        abi.ThreadStaticRegion = image.Tls;
        return abi;
    }

    private static TypeManagerModuleStatus Decode(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, out Image image)
    {
        image = default;
        if (sizeof(void*) != 8 || !capture.Succeeded || !mapping.IsValidFor(capture) ||
            mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong address) != AddressValueStatus.Success ||
            capture.Range.ImageSize > 0xFFFFFFFFUL || !Span(address, capture.Range.ImageSize))
            return TypeManagerModuleStatus.ImageAuthorityRejected;
        image.Base = address;
        image.Bytes = capture.Range.ImageSize;
        if (image.Bytes < 64 || U16((byte*)address) != 0x5A4D)
            return TypeManagerModuleStatus.ImageFormatRejected;
        ulong nt = U32((byte*)address + 60);
        if (nt < 64 || !Within(image.Bytes, nt, 24)) return TypeManagerModuleStatus.ImageFormatRejected;
        byte* pe = (byte*)(address + nt);
        ulong optionalBytes = U16(pe + 20), sections = U16(pe + 6);
        if (U32(pe) != 0x00004550 || U16(pe + 4) != 0x8664 || sections == 0 ||
            optionalBytes < 112 || !Within(image.Bytes, nt + 24, optionalBytes) ||
            U16(pe + 24) != 0x20B || U32(pe + 24 + 56) != image.Bytes)
            return TypeManagerModuleStatus.ImageFormatRejected;
        ulong tableOffset = nt + 24 + optionalBytes;
        if (!Within(image.Bytes, tableOffset, sections * 40)) return TypeManagerModuleStatus.ImageFormatRejected;
        image.Headers = tableOffset + sections * 40;
        image.Sections = address + tableOffset;
        image.SectionCount = sections;
        ulong headerExtent = U32(pe + 24 + 60);
        if (headerExtent < image.Headers || headerExtent > image.Bytes)
            return TypeManagerModuleStatus.ImageFormatRejected;
        for (ulong i = 0; i < sections; i++)
        {
            byte* s = (byte*)image.Sections + i * 40;
            ulong bytes = U32(s + 8), start = U32(s + 12);
            uint flags = U32(s + 36);
            if ((flags & (Write | Execute)) == (Write | Execute) ||
                (bytes != 0 && (start < headerExtent || !Within(image.Bytes, start, bytes))))
                return TypeManagerModuleStatus.ImageFormatRejected;
            for (ulong j = 0; j < i && bytes != 0; j++)
            {
                byte* other = (byte*)image.Sections + j * 40;
                if (Overlap(start, bytes, U32(other + 12), U32(other + 8)))
                    return TypeManagerModuleStatus.ImageFormatRejected;
            }
            if (U64(s) == 0x73656C75646F6D2E) // Exact eight-byte .modules name.
            {
                if (image.Modules != 0 || bytes < 8 || (bytes & 7) != 0 ||
                    ((address + start) & 7) != 0 || (flags & Read) == 0 || (flags & Execute) != 0)
                    return TypeManagerModuleStatus.ModuleListRejected;
                image.Modules = address + start;
                image.ModuleBytes = bytes;
            }
        }
        if (image.Modules == 0) return TypeManagerModuleStatus.ModuleListRejected;
        for (ulong i = 0; i < image.ModuleBytes; i += 8)
        {
            ulong header = U64((byte*)(image.Modules + i));
            if (header == 0) continue; // Only padding inside admitted virtual extent.
            if (image.Header != 0) return TypeManagerModuleStatus.ModuleListRejected;
            image.Header = header;
        }
        if ((image.Header & 7) != 0 || !InSection(image, image.Header, 16, Read, Write))
            return TypeManagerModuleStatus.MetadataRejected;
        byte* rtr = (byte*)image.Header;
        if (U32(rtr) != 0x00525452 || U16(rtr + 4) != PinnedMajorVersion ||
            U16(rtr + 6) != PinnedMinorVersion || U32(rtr + 8) != 0 || rtr[14] != 24 || rtr[15] != 1)
            return TypeManagerModuleStatus.UnsupportedFormat;
        image.Rows = U16(rtr + 12);
        image.TableBytes = 16 + (ulong)image.Rows * 24;
        if (image.Rows == 0 || !InSection(image, image.Header, image.TableBytes, Read, Write) ||
            Overlap(image.Header, image.TableBytes, image.Modules, image.ModuleBytes))
            return TypeManagerModuleStatus.MetadataRejected;
        uint previous = 0;
        for (uint i = 0; i < image.Rows; i++)
        {
            byte* row = rtr + 16 + i * 24;
            uint id = U32(row), flags = U32(row + 4);
            ulong start = U64(row + 8), end = U64(row + 16);
            if (id == 0 || id > 0x7FFFFFFF || id <= previous || flags > 1 ||
                (flags == 0 && end != 0) || (flags == 1 && (end < start || end - start > 0x7FFFFFFF)))
                return TypeManagerModuleStatus.MetadataRejected;
            previous = id;
            if (id == 207) return TypeManagerModuleStatus.UnsupportedFormat; // DehydratedData requires startup work.
            ulong length = flags == 0 ? 8 : end - start;
            if (!InSection(image, start, length, Read, 0)) return TypeManagerModuleStatus.MetadataRejected;
            if (id == 201) image.Gc = start;
            if (id == 202) image.Tls = start;
            if (id == 204)
            {
                if (flags != 0 || (start & 7) != 0 || !InSection(image, start, SlotBytes, Read | Write, Execute))
                    return TypeManagerModuleStatus.MetadataRejected;
                image.Slot = start;
            }
        }
        if (image.Slot == 0 || Overlap(image.Slot, SlotBytes, image.Modules, image.ModuleBytes) ||
            Overlap(image.Slot, SlotBytes, image.Header, image.TableBytes))
            return TypeManagerModuleStatus.MetadataRejected;
        // Row payload aliases may be legitimate. The registration writes must not
        // overwrite a different section's payload, including a partial slot alias.
        for (uint i = 0; i < image.Rows; i++)
        {
            byte* row = rtr + 16 + i * 24;
            if (U32(row) != 204 && Overlap(image.Slot, SlotBytes, U64(row + 8),
                U32(row + 4) == 0 ? 8 : U64(row + 16) - U64(row + 8)))
                return TypeManagerModuleStatus.MetadataRejected;
        }
        return TypeManagerModuleStatus.Success;
    }

    private static TypeManagerModuleStatus AdmitStorage(Image image, byte* storage, ulong capacity)
    {
        ulong address = (ulong)storage;
        if ((address & 7) != 0 || !Span(address, capacity) || capacity < Required(image))
            return TypeManagerModuleStatus.StorageRejected;
        // The complete capacity is admitted, not merely the written prefix. Static
        // storage inside the image is allowed only within one writable NX section.
        if (Overlap(address, capacity, image.Base, image.Bytes) &&
            !InSection(image, address, capacity, Read | Write, Execute))
            return TypeManagerModuleStatus.StorageOverlap;
        if (Overlap(address, capacity, image.Base, image.Headers) ||
            Overlap(address, capacity, image.Modules, image.ModuleBytes) ||
            Overlap(address, capacity, image.Header, image.TableBytes) ||
            Overlap(address, capacity, image.Slot, SlotBytes))
            return TypeManagerModuleStatus.StorageOverlap;
        for (uint i = 0; i < image.Rows; i++)
        {
            byte* row = (byte*)image.Header + 16 + i * 24;
            ulong length = U32(row + 4) == 0 ? 8 : U64(row + 16) - U64(row + 8);
            if (Overlap(address, capacity, U64(row + 8), length)) return TypeManagerModuleStatus.StorageOverlap;
        }
        return TypeManagerModuleStatus.Success;
    }

    private static ulong Required(Image image) =>
        (ulong)sizeof(State) + (ulong)sizeof(NativeAotTypeManagerAbi) + image.Headers + image.ModuleBytes + image.TableBytes;

    private static bool InSection(Image image, ulong address, ulong bytes, uint required, uint forbidden)
    {
        if (address < image.Base || !Within(image.Bytes, address - image.Base, bytes)) return false;
        for (ulong i = 0; i < image.SectionCount; i++)
        {
            byte* s = (byte*)image.Sections + i * 40;
            ulong start = image.Base + U32(s + 12), length = U32(s + 8);
            uint flags = U32(s + 36);
            if (length != 0 && address >= start && Within(length, address - start, bytes) &&
                (flags & required) == required && (flags & forbidden) == 0) return true;
        }
        return false;
    }

    private static bool Same(Image a, Image b) => a.Base == b.Base && a.Bytes == b.Bytes &&
        a.Headers == b.Headers && a.Sections == b.Sections && a.SectionCount == b.SectionCount &&
        a.Modules == b.Modules && a.ModuleBytes == b.ModuleBytes && a.Header == b.Header &&
        a.TableBytes == b.TableBytes && a.Slot == b.Slot && a.Gc == b.Gc && a.Tls == b.Tls && a.Rows == b.Rows;
    private static bool Same(NativeAotTypeManagerAbi a, NativeAotTypeManagerAbi b) =>
        a.OsModule == b.OsModule && a.ReadyToRunHeader == b.ReadyToRunHeader &&
        a.GcStaticRegion == b.GcStaticRegion && a.ThreadStaticRegion == b.ThreadStaticRegion &&
        a.ClassLibraryFunctions == b.ClassLibraryFunctions && a.ClassLibraryFunctionCount == b.ClassLibraryFunctionCount &&
        a.Padding == b.Padding;
    private static bool Span(ulong start, ulong bytes) => start != 0 && bytes != 0 && start <= ~0UL - bytes;
    private static bool Within(ulong length, ulong offset, ulong bytes) => offset <= length && bytes <= length - offset;
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => an != 0 && bn != 0 &&
        (a <= b ? b - a < an : a - b < bn);
    private static ushort U16(byte* p) => (ushort)((uint)p[0] | (uint)p[1] << 8);
    private static uint U32(byte* p) => (uint)p[0] | (uint)p[1] << 8 | (uint)p[2] << 16 | (uint)p[3] << 24;
    private static ulong U64(byte* p) => U32(p) | (ulong)U32(p + 4) << 32;
    private static bool Zero(byte* p, ulong bytes)
    {
        for (ulong i = 0; i < bytes; i++) if (p[i] != 0) return false;
        return true;
    }
    private static bool Equal(byte* a, byte* b, ulong bytes)
    {
        for (ulong i = 0; i < bytes; i++) if (a[i] != b[i]) return false;
        return true;
    }
    private static void Copy(byte* destination, byte* source, ulong bytes)
    {
        for (ulong i = 0; i < bytes; i++) destination[i] = source[i];
    }
}
