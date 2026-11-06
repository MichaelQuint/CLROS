using System;
using OS.Std.Binary;

namespace OS.Storage;

/// <summary>Locate exactly one EFI System Partition in a validated bounded GPT.
/// The caller retains exclusive ownership of the bytes throughout this call.
/// Names and attributes are opaque; location does not establish FAT/BPB validity.</summary>
public static unsafe class FAT32ApplicationEFIPartition
{
    // On-disk bytes for c12a7328-f81f-11d2-ba4b-00a0c93ec93b.
    public const ulong EfiTypeLow = 0x11d2f81fc12a7328UL;
    public const ulong EfiTypeHigh = 0x3bc93ec9a0004bbaUL;

    public static LiveEfiPartitionResult Select(LiveGptHeaderResult header, Span<byte> entries)
    {
        LiveEfiPartitionResult result = default;
        result.TableStatus = PrimaryGPTIndependentlyBoundsCRC.ValidateArray(header, entries, out _);
        if (result.TableStatus != LiveGptStatus.Success)
        {
            result.Status = LiveEfiPartitionStatus.InvalidTable;
            return result;
        }
        uint found = 0, selected = 0;
        fixed (byte* bytes = entries)
        {
            for (uint i = 0; i < PrimaryGPTIndependentlyBoundsCRC.EntryCount; i++)
            {
                byte* entry = bytes + i * PrimaryGPTIndependentlyBoundsCRC.EntrySize;
                if (EndianPrimitives.ReadUInt64LittleEndian(entry) == EfiTypeLow &&
                    EndianPrimitives.ReadUInt64LittleEndian(entry + 8) == EfiTypeHigh)
                {
                    selected = i;
                    found++;
                }
            }
            if (found != 1)
            {
                result.Status = found == 0 ? LiveEfiPartitionStatus.Missing : LiveEfiPartitionStatus.Ambiguous;
                return result;
            }
            byte* partition = bytes + selected * PrimaryGPTIndependentlyBoundsCRC.EntrySize;
            result.EntryIndex = selected;
            result.FirstLba = EndianPrimitives.ReadUInt64LittleEndian(partition + 32);
            result.LastLba = EndianPrimitives.ReadUInt64LittleEndian(partition + 40);
            result.BlockCount = result.LastLba - result.FirstLba + 1;
            result.ByteOffset = result.FirstLba * PrimaryGPTIndependentlyBoundsCRC.SectorBytes;
            result.Attributes = EndianPrimitives.ReadUInt64LittleEndian(partition + 48);
        }
        result.Status = LiveEfiPartitionStatus.Success;
        return result;
    }
}
