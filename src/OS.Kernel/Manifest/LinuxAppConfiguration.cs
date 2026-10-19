using OS.Std.Text;
using OS.Kernel.Memory;

namespace OS.Manifest;

/// <summary>Value-only GC policy for one validated immutable Linux manifest.
/// Pool pages and static/native reservations come from the serialized boot
/// owner, never advertised system RAM. No allocation, mapping or runtime entry
/// occurs. The caller retains the readable manifest for each call; returned
/// budget values retain no pointer or package authority.</summary>
public static unsafe class LinuxAppConfiguration
{
    public enum Status : byte { Uninitialized, Unconfigured, Success, InvalidManifest, MissingLimit, UnsupportedOverride, InvalidLimit, InvalidEntitlement }

    public readonly struct GcBudget
    {
        internal GcBudget(Status status, ulong pool = 0, ulong statics = 0, ulong native = 0, ulong gc = 0, ulong entitlement = 0)
        { Result = status; PoolBytes = pool; StaticBytes = statics; NativeReserveBytes = native; GcLimitBytes = gc; EntitlementBytes = entitlement; }
        public readonly Status Result;
        public readonly ulong PoolBytes, StaticBytes, NativeReserveBytes, GcLimitBytes, EntitlementBytes;
        public bool Succeeded => Result == Status.Success;
    }

    public const ulong MinimumGcLimitBytes = 8 * 1024 * 1024;
    /// <summary>Explicit configured-service capacity. The retained guest's
    /// conservative ordinary non-GC peak exceeds 18 MiB; reserve 20 MiB alongside
    /// its 16 MiB GC limit. The boot owner still charges the actual pool plus all
    /// static reservations against the immutable manifest before publication.</summary>
    public const uint ConfiguredServicePoolPages = 9216;
    /// <summary>Conservative non-GC backing allowance within the explicit service
    /// pool. This is an admission charge, not a separately allocated arena.</summary>
    public const ulong ConfiguredServiceNativeReserveBytes = 20 * 1024 * 1024;

    /// <summary>Derive static charges from the owner's sealed retained ledger,
    /// including package/image, tables, kernel/descriptor and worker reservations.
    /// Exclude exactly one LinuxElfBacking range matching the actual dynamic pool.
    /// Borrow immutable readable inputs under serialized boot ownership; retain
    /// no addresses and mutate nothing. Invalid/ambiguous provenance fails closed.
    /// This accounts declared headroom; guest pressure must establish sufficiency.</summary>
    public static GcBudget ReadGcBudgetFromReservations(byte* bytes, uint length, LinuxElfManifest manifest,
        BootReservedRange* reservations, uint count, uint capacity, ulong poolStart, uint poolPages,
        ulong nativeReserveBytes, bool required = false)
    {
        if (capacity > 32 || count == 0 || count > capacity || poolPages == 0 ||
            !BootReservationTableValidator.Validate(reservations, count, capacity).Succeeded)
            return new(Status.InvalidEntitlement);
        ulong statics = 0; uint found = 0;
        for (uint i = 0; i < count; i++)
        {
            var range = reservations[i];
            if (range.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong start) != OS.Boot.AddressValueStatus.Success)
                return new(Status.InvalidEntitlement);
            if (range.Kind == BootReservationKind.LinuxElfBacking && start == poolStart && range.PageCount == poolPages)
            { found++; continue; }
            if (range.ByteLength > ulong.MaxValue - statics) return new(Status.InvalidEntitlement);
            statics += range.ByteLength;
        }
        if (found != 1) return new(Status.InvalidEntitlement);
        return ReadGcBudget(bytes, length, manifest, poolPages, statics, nativeReserveBytes, required);
    }

    /// <summary>Recognize only the pinned runtime's exact case-sensitive
    /// DOTNET_GCHeapHardLimit name. This permits its mixed case without widening
    /// generic environment syntax or claiming a valid value/budget.</summary>
    public static bool IsGcHeapHardLimitKey(byte* key, uint length) =>
        AsciiPointerMatcher.Matches(key, length, 22, 0x475F54454E544F44, 0x7261487061654843, 0x74696D694C64);

    /// <summary>Read the sole absolute hexadecimal GC setting and prove
    /// GC+native reserve fits the actual page pool and pool+static reservations
    /// fit the manifest entitlement. Require page-aligned reservations and a
    /// measured8MiB-or-larger GC limit. Percentages, aliases and other GC
    /// overrides are unsupported. Missing optional configuration returns
    /// Unconfigured without applying this new policy to legacy profiles.
    /// Malformed metadata/settings fail without changing any ownership.</summary>
    public static GcBudget ReadGcBudget(byte* bytes, uint length, LinuxElfManifest manifest,
        uint poolPages, ulong staticBytes, ulong nativeReserveBytes, bool required = false)
    {
        if (bytes == null || length == 0 || length > LinuxElfPackage.MaximumManifestBytes ||
            (ulong)bytes > ulong.MaxValue - length || !manifest.Succeeded || manifest.ByteCount != length ||
            manifest.EnvironmentCount > 16) return new(Status.InvalidManifest);
        byte* setting = null; uint settingBytes = 0;
        for (uint i = 0; i < manifest.EnvironmentCount; i++)
        {
            uint at = i * 4, key = manifest.EnvironmentRanges[at], keyBytes = manifest.EnvironmentRanges[at + 1],
                value = manifest.EnvironmentRanges[at + 2], valueBytes = manifest.EnvironmentRanges[at + 3];
            if (keyBytes == 0 || keyBytes > 64 || key > length || keyBytes > length - key ||
                valueBytes > 256 || value > length || valueBytes > length - value) return new(Status.InvalidManifest);
            if (IsGcHeapHardLimitKey(bytes + key, keyBytes))
            {
                if (setting != null) return new(Status.UnsupportedOverride);
                setting = bytes + value; settingBytes = valueBytes;
            }
            else if (keyBytes >= 9 && AsciiPointerMatcher.Matches(bytes + key, 9, 9, 0x475F54454E544F44, 0x43) ||
                keyBytes >= 10 && AsciiPointerMatcher.Matches(bytes + key, 10, 10, 0x5F53554C504D4F43, 0x4347))
                return new(Status.UnsupportedOverride);
        }
        if (setting == null) return new(required ? Status.MissingLimit : Status.Unconfigured);
        if (!AsciiHexParser.TryParseUInt64(setting, settingBytes, out ulong gc) || gc < MinimumGcLimitBytes || (gc & 4095) != 0)
            return new(Status.InvalidLimit);
        ulong pool = poolPages * 4096UL, entitlement = manifest.MemoryBytes;
        if (entitlement == 0 || entitlement > 1024UL * 1024 * 1024 || (entitlement & 4095) != 0 ||
            pool == 0 || pool > entitlement || staticBytes == 0 || (staticBytes & 4095) != 0 || staticBytes > entitlement - pool ||
            nativeReserveBytes == 0 || (nativeReserveBytes & 4095) != 0 || nativeReserveBytes >= pool || gc > pool - nativeReserveBytes)
            return new(Status.InvalidEntitlement);
        return new(Status.Success, pool, staticBytes, nativeReserveBytes, gc, entitlement);
    }
}
