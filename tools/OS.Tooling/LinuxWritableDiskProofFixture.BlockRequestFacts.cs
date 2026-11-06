using System.Buffers.Binary;
using System.Text;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Tooling;

public static partial class LinuxWritableDiskProofFixture
{
    /// <summary>Immutable copied facts for one published generation. Original
    /// sample indexes permit later independent joins; -1 means not observed.
    /// A device error or unresolved publication never authorizes DMA release,
    /// retry, reset, file visibility, execution or physical persistence.</summary>

    public sealed class BlockRequestFacts
    {
        private readonly byte[] payload;
        internal BlockRequestFacts(Block.Observation value, int first, int completion, int copy, int last, byte[] data)
        {
            Request = value.Request;
            Kind = value.Kind;
            Sector = value.Sector;
            Flags = value.Flags;
            Cancelled = value.Cancelled;
            DeviceResult = value.DeviceResult;
            CompletionBytes = value.CompletionBytes;
            FirstSampleIndex = first;
            CompletionSampleIndex = completion;
            ReadCopySampleIndex = copy;
            LastSampleIndex = last;
            AvailableIndex = value.AvailableIndex;
            ConsumedIndex = value.ConsumedIndex;
            SubmittedCount = value.SubmittedCount;
            CompletedCount = value.CompletedCount;
            SubmittedBytes = value.SubmittedBytes;
            CompletedBytes = value.CompletedBytes;
            FlushTargetBytes = value.FlushTargetBytes;
            FlushAcknowledgedBytes = value.FlushAcknowledgedBytes;
            payload = data;
        }

        public Block.RequestToken Request
        {
            get;
        }
        public Block.Operation Kind
        {
            get;
        }
        public ulong Sector
        {
            get;
        }
        public Block.ObservationFlags Flags
        {
            get;
        }
        public bool Cancelled
        {
            get;
        }
        public Block.Status DeviceResult
        {
            get;
        }
        public uint CompletionBytes
        {
            get;
        }
        public int FirstSampleIndex
        {
            get;
        }
        public int CompletionSampleIndex
        {
            get;
        }
        public int ReadCopySampleIndex
        {
            get;
        }
        public int LastSampleIndex
        {
            get;
        }
        public ushort AvailableIndex
        {
            get;
        }
        public ushort ConsumedIndex
        {
            get;
        }
        public ulong SubmittedCount
        {
            get;
        }
        public ulong CompletedCount
        {
            get;
        }
        public ulong SubmittedBytes
        {
            get;
        }
        public ulong CompletedBytes
        {
            get;
        }
        public ulong FlushTargetBytes
        {
            get;
        }
        public ulong FlushAcknowledgedBytes
        {
            get;
        }
        public bool CompletionValidated => CompletionSampleIndex >= 0;
        public bool ReadCopied => ReadCopySampleIndex >= 0;
        public bool HasHeldRead => Kind == Block.Operation.Read && CompletionValidated && DeviceResult == Block.Status.Success && !ReadCopied && !Cancelled;
        public int PayloadBytes => payload.Length;

        public byte[] CopyPayload() => (byte[])payload.Clone();
    }
}
