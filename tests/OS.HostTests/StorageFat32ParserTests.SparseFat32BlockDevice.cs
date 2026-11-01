using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static partial class StorageFat32ParserTests
{
    private sealed class SparseFat32BlockDevice : IReadOnlyBlockDevice
    {
        private readonly Dictionary<ulong, byte[]> sectors = new();
        private readonly uint fatSectors;

        public SparseFat32BlockDevice(uint clusterCount)
        {
            fatSectors = (clusterCount + 2 + 127) / 128;
            uint totalSectors = ReservedSectorCount + FatCount * fatSectors + clusterCount;
            Info = BlockDeviceInfo.Create(SectorSize, PartitionStartLba + totalSectors).Info;
            BootSector = GetSector(PartitionStartLba);
            WriteUInt16(BootSector, 11, (ushort)SectorSize);
            BootSector[13] = 1;
            WriteUInt16(BootSector, 14, ReservedSectorCount);
            BootSector[16] = FatCount;
            WriteUInt32(BootSector, 32, totalSectors);
            WriteUInt32(BootSector, 36, fatSectors);
            WriteUInt32(BootSector, 44, RootCluster);
            BootSector[510] = 0x55;
            BootSector[511] = 0xAA;
        }

        public BlockDeviceInfo Info { get; }
        public byte[] BootSector { get; }
        public ulong FirstFatLba => PartitionStartLba + ReservedSectorCount;
        public ulong FirstDataLba => FirstFatLba + FatCount * fatSectors;
        public int ReadCount { get; private set; }
        public ulong? FailedLba { get; set; }

        public byte[] GetSector(ulong lba)
        {
            if (!sectors.TryGetValue(lba, out byte[]? sector))
            {
                sector = new byte[SectorSize];
                sectors.Add(lba, sector);
            }

            return sector;
        }

        public void WriteFatEntry(uint fatCopy, uint cluster, uint next)
        {
            ulong lba = FirstFatLba + fatCopy * fatSectors + cluster / 128;
            WriteUInt32(GetSector(lba), (int)(cluster % 128) * 4, next);
        }

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            ReadCount++;
            BlockReadResult result = BlockDeviceRead.Validate(Info, firstBlock, blockCount, destination);
            if (!result.Succeeded) return result;
            if (FailedLba is ulong failed && failed >= firstBlock && failed - firstBlock < blockCount)
                return BlockReadResult.Fail(BlockDeviceStatus.IoError);
            for (uint index = 0; index < blockCount; index++)
            {
                Span<byte> block = destination.Slice((int)(index * SectorSize), (int)SectorSize);
                if (sectors.TryGetValue(firstBlock + index, out byte[]? sector)) sector.AsSpan().CopyTo(block);
                else block.Clear();
            }

            return result;
        }
    }
}
