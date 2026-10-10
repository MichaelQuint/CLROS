using System;
using System.Runtime.CompilerServices;
using OS.Std.Binary;

namespace OS.Storage;

public enum Lba0Status : byte
{
    Uninitialized, Success, InvalidLength, InvalidCapacity, BadSignature,
    BadProtectiveEntry, HybridEntry, ReadFailed
}

public struct Lba0Result
{
    public Lba0Status Status;
    public BlockReadResult Read;
    public uint FirstLba, ProtectedSectors;
    public readonly bool Succeeded => Status == Lba0Status.Success && Read.Succeeded && Read.BlocksRead == 1 && Read.BytesRead == 512;
}

/// <summary>Read LBA0 through a closed constrained block contract and accept a
/// single protective MBR: signature55AA, first partition typeEE/start1, length
/// min(capacity-1, uint.MaxValue), inactive boot flag, no other partition entry.
/// Boot code, disk signature and legacy CHS are opaque. This is not GPT parsing.</summary>
public static unsafe class LBAN0Guest
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static BlockReadResult Read<T>(ref T device, Span<byte> destination)
        where T : struct, IReadOnlyBlockDevice => device.ReadBlocks(0, 1, destination);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static BlockDeviceInfo Info<T>(ref T device)
        where T : struct, IReadOnlyBlockDevice => device.Info;

    public static Lba0Result Validate(Span<byte> sector, ulong capacity, BlockReadResult read)
    {
        Lba0Result result = default; result.Read = read;
        if (!read.Succeeded || read.BlocksRead != 1 || read.BytesRead != 512)
        { result.Status = Lba0Status.ReadFailed; return result; }
        if (sector.Length != 512) { result.Status = Lba0Status.InvalidLength; return result; }
        if (capacity < 2 || capacity > ulong.MaxValue / 512)
        { result.Status = Lba0Status.InvalidCapacity; return result; }
        fixed (byte* bytes = sector)
        {
            if (bytes == null) { result.Status = Lba0Status.InvalidLength; return result; }
            if (bytes[510] != 0x55 || bytes[511] != 0xaa)
            { result.Status = Lba0Status.BadSignature; return result; }
            result.FirstLba = EndianPrimitives.ReadUInt32LittleEndian(bytes + 454);
            result.ProtectedSectors = EndianPrimitives.ReadUInt32LittleEndian(bytes + 458);
            ulong expected = capacity - 1;
            if (expected > 0xffffffffUL) expected = 0xffffffffUL;
            if (bytes[446] != 0 || bytes[450] != 0xee || result.FirstLba != 1 || result.ProtectedSectors != expected)
            { result.Status = Lba0Status.BadProtectiveEntry; return result; }
            for (uint i = 462; i < 510; i++)
                if (bytes[i] != 0) { result.Status = Lba0Status.HybridEntry; return result; }
        }
        result.Status = Lba0Status.Success; return result;
    }
}
