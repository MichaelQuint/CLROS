using System.Globalization;

namespace OS.Tooling;

public static partial class NativeDiskReadObservations
{
    /// <summary>Copied immutable facts. Copied and CopyChunk each carry512
    /// committed bytes. Bytes is the physical request length, Sector its first
    /// LBA, and ChunkIndex identifies the payload sector within that request.
    /// Empty DataHex on the other stages grants no data-copy authority.</summary>

    public readonly record struct Observation(Kind Kind, ulong Owner, ulong Epoch, ulong Generation, ulong Sector, ulong Sequence, uint Bytes, ushort AvailableIndex, ushort ConsumedIndex, ulong Clock, string DataHex)
    {
        public uint ChunkIndex
        {
            get; init;
        }
        public ulong DataSector => Sector + ChunkIndex;
    }
}
