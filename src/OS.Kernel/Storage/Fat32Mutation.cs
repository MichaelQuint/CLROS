using System;
using OS.Std.Binary;
using OS.Std.Hash;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Storage;

/// <summary>Bounded regular-file mutation planning over an exclusively retained,
/// caller-owned FAT32 snapshot. This host-tested leaf activates no mount/device.
/// The caller supplies every journal/scratch byte, serializes access, and keeps
/// the snapshot, journal and block owner stable until completion or recovery.
/// A volatile before-image is NOT a power-loss journal. Recovery after restart
/// requires independently durable retention of that exact journal and volume
/// identity; this leaf does not provide that persistence or claim atomic rename.
/// Directory creation/move, overwrite-rename and general OEM case folding are
/// unsupported. Existing directories are traversed from the admitted root.</summary>
public static unsafe partial class Fat32Mutation
{



    private static bool Alias(ReadOnlySpan<byte> alias)
    {
        Span<byte> entry = stackalloc byte[32]; entry.Clear(); alias.CopyTo(entry); entry[11] = 0x20;
        Span<ushort> words = stackalloc ushort[260]; LiveVfatState state = default;
        return Vfat.Consume(entry, ref state, words, out _) == LiveDirectoryStatus.Entry && alias[0] != '.';
    }
    private static bool Equal(ReadOnlySpan<char> a, ReadOnlySpan<char> b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
    private static bool EqualAlias(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    { for (int i = 0; i < 11; i++) if (Fold(a[i]) != Fold(b[i])) return false; return true; }
    private static byte Fold(byte c) => c is >= (byte)'a' and <= (byte)'z' ? (byte)(c - 32) : c;
    private static int ShortName(ReadOnlySpan<byte> raw, Span<char> name)
    {
        int length = 0; for (int i = 0; i < 8 && raw[i] != ' '; i++) name[length++] = (char)(i == 0 && raw[i] == 5 ? 0xe5 : raw[i]);
        if (raw[8] != ' ') { name[length++] = '.'; for (int i = 8; i < 11 && raw[i] != ' '; i++) name[length++] = (char)raw[i]; }
        return length;
    }
    private static bool AliasNameEquals(ReadOnlySpan<char> a, ReadOnlySpan<char> b) => Equal(a, b);
    private static bool LongAliasEquals(Span<ushort> words, ushort length, ReadOnlySpan<byte> alias)
    {
        if (length == 0) return false; Span<char> name = stackalloc char[260]; for (int i = 0; i < length; i++) name[i] = (char)words[i];
        Span<byte> raw = stackalloc byte[32]; raw.Clear(); alias.CopyTo(raw); Span<char> shortName = stackalloc char[12];
        return Equal(name[..length], shortName[..ShortName(raw, shortName)]);
    }
    private static ushort Get16(ReadOnlySpan<byte> bytes, int offset) { fixed (byte* p = bytes) return EndianPrimitives.ReadUInt16LittleEndian(p + offset); }
    private static uint Get32(ReadOnlySpan<byte> bytes, int offset) { fixed (byte* p = bytes) return EndianPrimitives.ReadUInt32LittleEndian(p + offset); }
    private static void Put32(Span<byte> bytes, int offset, uint value) => EndianPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);
    private static bool Ordinary(ulong start, ulong bytes) => start >= 4096 && start < 0x0000800000000000UL && bytes <= 0x0000800000000000UL - start;
    private static bool DeviceStorage(Block.State* device) => Ordinary((ulong)device, (ulong)sizeof(Block.State)) && ((ulong)device & 7) == 0 &&
        device->Self == (ulong)device && Ordinary((ulong)device->Input.Pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)) &&
        device->Input.Pool->Self == (ulong)device->Input.Pool && device->Input.Pool->Count is > 0 and <= 64 &&
        Ordinary((ulong)device->Input.Pool->Slots, device->Input.Pool->Count * (ulong)sizeof(OS.Drivers.DmaBufferSlot));
    private static bool DeviceDisjoint(ulong start, ulong bytes, Block.State* device)
    {
        if (!DeviceStorage(device)) return false;
        var input = device->Input; var pool = input.Pool;
        return !Range(start, bytes, (ulong)device, (ulong)sizeof(Block.State)) &&
            !Range(start, bytes, (ulong)pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)) &&
            !Range(start, bytes, (ulong)pool->Slots, pool->Count * (ulong)sizeof(OS.Drivers.DmaBufferSlot)) &&
            !Range(start, bytes, pool->Window.CpuVirtualBase, pool->Window.ByteLength) &&
            !Range(start, bytes, input.Common.VirtualStart, input.Common.Bytes) &&
            !Range(start, bytes, input.Notify.VirtualStart, input.Notify.Bytes) &&
            !Range(start, bytes, input.Device.VirtualStart, input.Device.Bytes);
    }
    private static bool Storage(Span<byte> image, Span<Change> changes, Span<byte> before, Span<byte> after, Span<uint> scratch, Block.State* device)
    {
        fixed (byte* i = image, b = before, a = after) fixed (Change* c = changes) fixed (uint* s = scratch)
        {
            ulong* starts = stackalloc ulong[5] { (ulong)i, (ulong)c, (ulong)b, (ulong)a, (ulong)s };
            ulong* lengths = stackalloc ulong[5] { (ulong)image.Length, (ulong)changes.Length * (ulong)sizeof(Change), (ulong)before.Length, (ulong)after.Length, (ulong)scratch.Length * 4 };
            for (int x = 0; x < 5; x++)
            {
                if (!Ordinary(starts[x], lengths[x]) || !DeviceDisjoint(starts[x], lengths[x], device)) return false;
                for (int y = 0; y < x; y++) if (Range(starts[x], lengths[x], starts[y], lengths[y])) return false;
            }
            return true;
        }
    }
}
