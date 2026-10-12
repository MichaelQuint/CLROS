using OS.Std.Hash;

namespace OS.Manifest;

public enum LinuxElfStatus : byte
{
    Uninitialized, Success, InvalidLength, InvalidHeader, UnsupportedProfile,
    InvalidCharacter, MalformedLine, UnknownField, DuplicateField, MissingField,
    InvalidValue, InvalidPath, InvalidEndpoint, InvalidEnvironment, DigestMismatch,
    UnsupportedElf, InvalidDestination
}

public struct LinuxElfField { public uint Offset, Length; }

/// <summary>Value-only offsets into stable manifest bytes, never firmware handles.</summary>
public unsafe struct LinuxElfManifest
{
    public LinuxElfStatus Status;
    public uint ErrorOffset, ByteCount, MemoryBytes, StackBytes, Threads, Descriptors, Ipv4, Port;
    public LinuxElfField Path, Args, Digest;
    public uint EnvironmentCount;
    public fixed uint EnvironmentRanges[64]; // key offset/length, value offset/length
    public readonly bool Succeeded => Status == LinuxElfStatus.Success;
}

public struct LinuxElfPackageView
{
    public LinuxElfStatus Status;
    public uint ByteCount, ManifestOffset, ManifestBytes, PayloadOffset, PayloadBytes;
    public LinuxElfManifest Manifest;
    public readonly bool Succeeded => Status == LinuxElfStatus.Success;
}

/// <summary>Explicit Linux ELF v1 admission; legacy OSAPPv0 is never dispatched
/// here automatically. All input pointers must refer to trusted readable storage.
/// Staging validates data only: no load mapping, runtime entry or execution.</summary>
public static unsafe class LinuxElfPackage
{
    public const uint HeaderBytes = 64, MaximumManifestBytes = 4096, MaximumBytes = 16 * 1024 * 1024;
    public const ulong Magic = 0x3130464c45524c43; // CLRELF01, little-endian

#if !NOSTDLIB_EFI
    public static LinuxElfPackageView Validate(ReadOnlySpan<byte> bytes)
    {
        fixed (byte* p = bytes) return Validate(p, (uint)bytes.Length);
    }
#endif

    public static LinuxElfPackageView Validate(byte* bytes, uint length)
    {
        LinuxElfPackageView result = default;
        if (!Readable(bytes, length) || length < HeaderBytes || length > MaximumBytes) return Fail(LinuxElfStatus.InvalidLength);
        if (U64(bytes) != Magic || U32(bytes + 16) != HeaderBytes) return Fail(LinuxElfStatus.InvalidHeader);
        if (U32(bytes + 8) != 1 || U32(bytes + 12) != 1) return Fail(LinuxElfStatus.UnsupportedProfile);
        for (int i = 32; i < HeaderBytes; i++) if (bytes[i] != 0) return Fail(LinuxElfStatus.InvalidHeader);
        uint manifestBytes = U32(bytes + 20); ulong payloadBytes = U64(bytes + 24);
        if (manifestBytes == 0 || manifestBytes > MaximumManifestBytes || payloadBytes < 64 ||
            payloadBytes > MaximumBytes || HeaderBytes + (ulong)manifestBytes + payloadBytes != length) return Fail(LinuxElfStatus.InvalidLength);
        result.Manifest = ParseManifest(bytes + HeaderBytes, manifestBytes);
        if (!result.Manifest.Succeeded) { result.Status = result.Manifest.Status; return result; }
        byte* payload = bytes + HeaderBytes + manifestBytes;
        // A complete mapped-image admission belongs to M16-004. This rejects
        // obvious cross-profile images before preserving bytes as nonexecuting data.
        if (U32(payload) != 0x464c457f || payload[4] != 2 || payload[5] != 1 || payload[6] != 1 ||
            (payload[7] != 0 && payload[7] != 3) || U16(payload + 16) != 2 || U16(payload + 18) != 62 || U32(payload + 20) != 1)
            return Fail(LinuxElfStatus.UnsupportedElf);
        if (!StaticDataHeader(payload, (uint)payloadBytes, result.Manifest.MemoryBytes)) return Fail(LinuxElfStatus.UnsupportedElf);
        byte* digest = stackalloc byte[32];
        if (!Sha256.TryCompute(payload, (uint)payloadBytes, digest, 32)) return Fail(LinuxElfStatus.InvalidLength);
        byte* expected = bytes + HeaderBytes + result.Manifest.Digest.Offset;
        uint different = 0;
        for (uint i = 0; i < 32; i++) different |= (uint)(digest[i] ^ ((Hex(expected[i * 2]) << 4) | Hex(expected[i * 2 + 1])));
        if (different != 0) return Fail(LinuxElfStatus.DigestMismatch);
        result.Status = LinuxElfStatus.Success; result.ByteCount = length;
        result.ManifestOffset = HeaderBytes; result.ManifestBytes = manifestBytes;
        result.PayloadOffset = HeaderBytes + manifestBytes; result.PayloadBytes = (uint)payloadBytes;
        return result;
    }

    public static LinuxElfManifest ParseManifest(byte* bytes, uint length)
    {
        LinuxElfManifest m = default;
        if (!Readable(bytes, length) || length == 0 || length > MaximumManifestBytes) return Bad(m, LinuxElfStatus.InvalidLength, 0);
        uint offset = 0, seen = 0;
        while (offset < length)
        {
            uint start = offset, split = length;
            while (offset < length && bytes[offset] != '\n')
            {
                byte c = bytes[offset];
                if (c == '=' && split == length) split = offset;
                if (c < 32 || c > 126)
                {
                    if (c != '\r' || offset + 1 >= length || bytes[offset + 1] != '\n') return Bad(m, LinuxElfStatus.InvalidCharacter, offset);
                }
                offset++;
            }
            uint end = offset; if (end > start && bytes[end - 1] == '\r') end--;
            if (offset < length) offset++;
            if (split <= start || split >= end) return Bad(m, LinuxElfStatus.MalformedLine, start);
            uint keyLength = split - start, valueStart = split + 1, valueLength = end - valueStart;
            byte* key = bytes + start; byte* value = bytes + valueStart;
            int field = -1;
            if (Equal(key, keyLength, "format")) { field = 0; if (!Equal(value, valueLength, "clros-linux-elf")) return Bad(m, LinuxElfStatus.UnsupportedProfile, valueStart); }
            else if (Equal(key, keyLength, "version")) { field = 1; if (!Equal(value, valueLength, "1")) return Bad(m, LinuxElfStatus.UnsupportedProfile, valueStart); }
            else if (Equal(key, keyLength, "profile")) { field = 2; if (!Equal(value, valueLength, "linux-musl-x64-static-v1")) return Bad(m, LinuxElfStatus.UnsupportedProfile, valueStart); }
            else if (Equal(key, keyLength, "app.path")) { field = 3; if (!SafePath(value, valueLength)) return Bad(m, LinuxElfStatus.InvalidPath, valueStart); m.Path = new LinuxElfField { Offset = valueStart, Length = valueLength }; }
            else if (Equal(key, keyLength, "args")) { field = 4; if (valueLength > 256 || !ValidArgs(value, valueLength)) return Bad(m, LinuxElfStatus.InvalidValue, valueStart); m.Args = new LinuxElfField { Offset = valueStart, Length = valueLength }; }
            else if (Equal(key, keyLength, "digest")) { field = 5; if (valueLength != 64) return Bad(m, LinuxElfStatus.InvalidValue, valueStart); for (uint i = 0; i < 64; i++) if (Hex(value[i]) < 0) return Bad(m, LinuxElfStatus.InvalidValue, valueStart + i); m.Digest = new LinuxElfField { Offset = valueStart, Length = valueLength }; }
            else if (Equal(key, keyLength, "memory")) { field = 6; if (!Number(value, valueLength, 1024 * 1024 * 1024, out m.MemoryBytes) || m.MemoryBytes < 4096 || (m.MemoryBytes & 4095) != 0) return Bad(m, LinuxElfStatus.InvalidValue, valueStart); }
            else if (Equal(key, keyLength, "stack")) { field = 7; if (!Number(value, valueLength, 8 * 1024 * 1024, out m.StackBytes) || m.StackBytes < 4096 || (m.StackBytes & 4095) != 0) return Bad(m, LinuxElfStatus.InvalidValue, valueStart); }
            else if (Equal(key, keyLength, "threads")) { field = 8; if (!Number(value, valueLength, 64, out m.Threads) || m.Threads == 0) return Bad(m, LinuxElfStatus.InvalidValue, valueStart); }
            else if (Equal(key, keyLength, "descriptors")) { field = 9; if (!Number(value, valueLength, 1024, out m.Descriptors) || m.Descriptors == 0) return Bad(m, LinuxElfStatus.InvalidValue, valueStart); }
            else if (Equal(key, keyLength, "listen")) { field = 10; if (!Endpoint(value, valueLength, out m.Ipv4, out m.Port)) return Bad(m, LinuxElfStatus.InvalidEndpoint, valueStart); }
            else if (keyLength > 4 && key[0] == 'e' && key[1] == 'n' && key[2] == 'v' && key[3] == '.')
            {
                uint n = keyLength - 4; byte* name = key + 4;
                if (n > 64 || valueLength > 256 || m.EnvironmentCount == 16 || name[0] < 'A' || name[0] > 'Z') return Bad(m, LinuxElfStatus.InvalidEnvironment, start);
                for (uint i = 1; i < n; i++) if (!((name[i] >= 'A' && name[i] <= 'Z') || (name[i] >= '0' && name[i] <= '9') || name[i] == '_')) return Bad(m, LinuxElfStatus.InvalidEnvironment, start + 4 + i);
                for (uint i = 0; i < m.EnvironmentCount; i++) if (BytesEqual(bytes + m.EnvironmentRanges[i * 4], m.EnvironmentRanges[i * 4 + 1], name, n)) return Bad(m, LinuxElfStatus.DuplicateField, start);
                uint index = m.EnvironmentCount++ * 4;
                m.EnvironmentRanges[index] = start + 4; m.EnvironmentRanges[index + 1] = n;
                m.EnvironmentRanges[index + 2] = valueStart; m.EnvironmentRanges[index + 3] = valueLength;
                continue;
            }
            else return Bad(m, LinuxElfStatus.UnknownField, start);
            uint bit = 1u << field; if ((seen & bit) != 0) return Bad(m, LinuxElfStatus.DuplicateField, start); seen |= bit;
        }
        if (seen != 0x7ff) return Bad(m, LinuxElfStatus.MissingField, length);
        if (m.StackBytes > m.MemoryBytes) return Bad(m, LinuxElfStatus.InvalidValue, length);
        m.Status = LinuxElfStatus.Success; m.ByteCount = length; return m;
    }

    // Reject before touching destination. The destination must be exclusive;
    // the firmware adapter allocates/reserves it and makes it read-only/NX.
    public static LinuxElfStatus Copy(byte* source, uint length, byte* destination, uint capacity, out LinuxElfPackageView view)
    {
        view = default;
        if (!Readable(destination, capacity) || capacity < length || capacity > MaximumBytes ||
            !Readable(source, length) || ((ulong)source < (ulong)destination + capacity && (ulong)destination < (ulong)source + length)) return LinuxElfStatus.InvalidDestination;
        view = Validate(source, length); if (!view.Succeeded) return view.Status;
        for (uint i = 0; i < length; i++) destination[i] = source[i];
        LinuxElfPackageView copied = Validate(destination, length);
        if (!copied.Succeeded) { view = default; return copied.Status; }
        view = copied; return LinuxElfStatus.Success;
    }

    // Read-only future process namespace: the sole executable path is bound to
    // the manifest. Data is copied out; no writable alias or firmware pointer.
    public static LinuxElfStatus ReadFile(byte* package, uint length, byte* path, uint pathLength,
        uint offset, byte* destination, uint capacity, out uint actual)
    {
        actual = 0; var view = Validate(package, length); if (!view.Succeeded) return view.Status;
        if (!Readable(path, pathLength) || !BytesEqual(path, pathLength, package + view.ManifestOffset + view.Manifest.Path.Offset, view.Manifest.Path.Length)) return LinuxElfStatus.InvalidPath;
        if (offset > view.PayloadBytes || capacity > MaximumBytes || !Readable(destination, capacity) ||
            ((ulong)destination < (ulong)package + length && (ulong)package < (ulong)destination + capacity)) return LinuxElfStatus.InvalidDestination;
        uint n = view.PayloadBytes - offset; if (n > capacity) n = capacity;
        for (uint i = 0; i < n; i++) destination[i] = package[view.PayloadOffset + offset + i];
        actual = n; return LinuxElfStatus.Success;
    }

    private static bool Readable(byte* p, uint n) => p != null && (ulong)p <= ulong.MaxValue - n;
    private static bool StaticDataHeader(byte* p, uint bytes, uint memoryLimit)
    {
        if (p[8] != 0 || U32(p + 48) != 0 || U16(p + 52) != 64 || U16(p + 54) != 56) return false;
        for (int i = 9; i < 16; i++) if (p[i] != 0) return false;
        ulong table = U64(p + 32), entry = U64(p + 24); uint count = U16(p + 56);
        if (table < 64 || table > bytes || count == 0 || count > 128 || (ulong)count * 56 > bytes - table || entry == 0) return false;
        bool entryCovered = false; ulong memory = 0;
        for (uint i = 0; i < count; i++)
        {
            byte* row = p + table + i * 56; uint type = U32(row), flags = U32(row + 4);
            if (type == 2 || type == 3 || (type == 0x6474e551 && (flags & 1) != 0)) return false;
            if (type != 1) continue;
            ulong offset = U64(row + 8), address = U64(row + 16), file = U64(row + 32), size = U64(row + 40);
            if (size == 0 || file > size || offset > bytes || file > bytes - offset || address >= 0x800000000000 || size > 0x800000000000 - address ||
                (flags & ~7u) != 0 || (flags & 3) == 3 || size > memoryLimit - memory) return false;
            memory += size;
            if ((flags & 1) != 0 && entry >= address && entry - address < file) entryCovered = true;
        }
        return entryCovered;
    }
    private static LinuxElfPackageView Fail(LinuxElfStatus status) => new LinuxElfPackageView { Status = status };
    private static LinuxElfManifest Bad(LinuxElfManifest m, LinuxElfStatus s, uint at) { m.Status = s; m.ErrorOffset = at; return m; }
    private static bool Equal(byte* p, uint n, string value) { if (n != (uint)value.Length) return false; for (uint i = 0; i < n; i++) if (p[i] != value[(int)i]) return false; return true; }
    private static bool BytesEqual(byte* a, uint an, byte* b, uint bn) { if (an != bn) return false; for (uint i = 0; i < an; i++) if (a[i] != b[i]) return false; return true; }
    private static int Hex(byte b) => b >= '0' && b <= '9' ? b - '0' : b >= 'a' && b <= 'f' ? b - 'a' + 10 : -1;
    private static bool Number(byte* p, uint n, uint max, out uint result)
    {
        result = 0; if (n == 0 || n > 10 || (n > 1 && p[0] == '0')) return false;
        for (uint i = 0; i < n; i++) { if (p[i] < '0' || p[i] > '9') return false; uint digit = (uint)(p[i] - '0'); if (result > max / 10 || (result == max / 10 && digit > max % 10)) return false; result = result * 10 + digit; } return true;
    }
    private static bool Endpoint(byte* p, uint n, out uint address, out uint port)
    {
        address = 0; port = 0; uint at = 0;
        for (int part = 0; part < 4; part++) { uint start = at; while (at < n && p[at] >= '0' && p[at] <= '9') at++; if (!Number(p + start, at - start, 255, out uint octet)) return false; address = (address << 8) | octet; if (at == n || p[at++] != (part == 3 ? ':' : '.')) return false; }
        return Number(p + at, n - at, 65535, out port) && port != 0;
    }
    private static bool SafePath(byte* p, uint n)
    {
        if (n == 0 || n > 128) return false; uint segment = 0;
        for (uint i = 0; i <= n; i++)
        {
            if (i == n || p[i] == '/') { uint size = i - segment; if (size == 0 || (size == 1 && p[segment] == '.') || (size == 2 && p[segment] == '.' && p[segment + 1] == '.')) return false; segment = i + 1; }
            else if (!((p[i] >= 'a' && p[i] <= 'z') || (p[i] >= 'A' && p[i] <= 'Z') || (p[i] >= '0' && p[i] <= '9') || p[i] == '_' || p[i] == '-' || p[i] == '.')) return false;
        }
        return true;
    }
    private static bool ValidArgs(byte* p, uint n) { bool quote = false; for (uint i = 0; i < n; i++) { if (p[i] == '"') quote = !quote; if (p[i] == '\\') return false; } return !quote; }
    private static ushort U16(byte* p) => (ushort)(p[0] | (p[1] << 8));
    private static uint U32(byte* p) => (uint)p[0] | ((uint)p[1] << 8) | ((uint)p[2] << 16) | ((uint)p[3] << 24);
    private static ulong U64(byte* p) => U32(p) | ((ulong)U32(p + 4) << 32);
}
