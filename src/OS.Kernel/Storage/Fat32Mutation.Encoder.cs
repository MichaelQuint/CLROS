using System;
using OS.Std.Binary;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

/// <summary>Bounded FAT32 mutation format contracts shared by snapshot planning
/// and the continuing owner. This partial contains only copied values and
/// caller-buffer entry encoding; it retains no storage or device owner.
/// Snapshot transaction APIs remain in the host-only planning partial.</summary>
public static unsafe partial class Fat32Mutation
{
    public const int SectorBytes = 512, MaximumChanges = 4096, MaximumClusters = 4096;
    public const int ScratchClusters = MaximumClusters * 3, MaximumWriteBytes = 65536;
    public const uint MaximumWork = 1000000;
    public enum Status : byte
    {
        Uninitialized, Success, InvalidStorage, InvalidVolume, InvalidName, InvalidAlias,
        CorruptDirectory, CorruptChain, MirrorMismatch, NotFound, AlreadyExists,
        NotFile, ReadOnly, CapacityExceeded, TraversalBudgetExceeded, DiskFull,
        StaleToken, Busy, Planned, Pending, Cancelled, RecoveryRequired,
        BlockFailure, SnapshotChanged, GenerationExhausted, InvalidJournal
    }
    public enum Stage : byte { Data = 1, AllocateFat, PublishDirectory, RetireDirectory, ReleaseFat, FsInfo }

    /// <summary>Copied write identity; Before/After sector bytes live at the same
    /// index in the caller's two journal buffers. Repeated LBAs at different
    /// stages are deliberate. Before always denotes the original snapshot.</summary>
    public struct Change { public ulong Sector; public Stage Phase; }

    /// <summary>Encode the existing CLROS VFAT 13-word layout/checksum into a
    /// caller-owned run, descending long ordinals followed by the supplied short
    /// entry. Supports1..255 UTF16 characters (at most21 entries), with exact
    /// decoder validation. The caller serializes access and keeps inputs and
    /// output stable for the call. Output is unspecified on failure;
    /// entriesWritten is0. No I/O, alias selection, allocation or normalization
    /// is performed.</summary>
    public static Status EncodeEntryRun(ReadOnlySpan<char> name, ReadOnlySpan<byte> shortEntry,
        Span<byte> destination, out int entriesWritten)
    {
        entriesWritten = 0;
        if (!Name(name) || shortEntry.Length != 32 || shortEntry[11] == 15) return Status.InvalidName;
        int slots = (name.Length + 12) / 13, entries = slots + 1;
        if (destination.Length < entries * 32 || Overlaps(name, destination) || Overlaps(shortEntry, destination)) return Status.InvalidStorage;
        byte checksum = 0;
        for (int i = 0; i < 11; i++) checksum = unchecked((byte)((checksum >> 1) + ((checksum & 1) << 7) + shortEntry[i]));
        for (int ordinal = slots; ordinal != 0; ordinal--)
        {
            Span<byte> entry = destination.Slice((slots - ordinal) * 32, 32); entry.Clear();
            entry[0] = (byte)(ordinal | (ordinal == slots ? 0x40 : 0)); entry[11] = 15; entry[13] = checksum;
            for (int i = 0; i < 13; i++)
            {
                int position = (ordinal - 1) * 13 + i;
                ushort value = position < name.Length ? name[position] : position == name.Length ? (ushort)0 : ushort.MaxValue;
                int offset = i < 5 ? 1 + i * 2 : i < 11 ? 14 + (i - 5) * 2 : 28 + (i - 11) * 2;
                Put16(entry, offset, value);
            }
        }
        OS.Std.Memory.BufferOperations.MemoryCopy(shortEntry, destination.Slice(slots * 32, 32));
        Span<ushort> words = stackalloc ushort[260]; LiveVfatState state = default;
        for (int i = 0; i < entries; i++)
        {
            var result = Vfat.Consume(destination.Slice(i * 32, 32), ref state, words, out var parsed);
            if (result != (i == slots ? LiveDirectoryStatus.Entry : LiveDirectoryStatus.LongName) ||
                (i == slots && parsed.NameLength != name.Length)) return Status.InvalidAlias;
        }
        entriesWritten = entries; return Status.Success;
    }

    private static bool Name(ReadOnlySpan<char> name)
    {
        if (name.Length is < 1 or > 255 || name[name.Length - 1] is '.' or ' ') return false;
        bool high = false;
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c < 32 || c is '\uffff' or '"' or '\\' or '/' or ':' or '|' or '<' or '>' or '*' or '?' ||
                (high ? c is < '\udc00' or > '\udfff' : c is >= '\udc00' and <= '\udfff')) return false;
            high = c is >= '\ud800' and <= '\udbff';
        }
        return !high;
    }
    private static void Put16(Span<byte> bytes, int offset, ushort value) => EndianPrimitives.WriteUInt16LittleEndian(bytes.Slice(offset), value);
    private static bool Overlaps(ReadOnlySpan<char> a, Span<byte> b)
    { fixed (char* x = a) fixed (byte* y = b) return Range((ulong)x, (ulong)a.Length * 2, (ulong)y, (ulong)b.Length); }
    private static bool Overlaps(ReadOnlySpan<byte> a, Span<byte> b)
    { fixed (byte* x = a, y = b) return Range((ulong)x, (ulong)a.Length, (ulong)y, (ulong)b.Length); }
    private static bool Range(ulong a, ulong bytes, ulong b, ulong other) =>
        bytes != 0 && other != 0 && (a <= b ? b - a < bytes : a - b < other);
}
