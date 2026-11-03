using OS.Tools.QemuRunner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static partial class Program
{
    private sealed partial class ObservationScanner
    {
        private readonly List<PreflightCandidate> _preflight = new List<PreflightCandidate>();
        private readonly List<RuntimeCandidate> _runtime = new List<RuntimeCandidate>();
        private readonly HashSet<long> _preflightAddresses = new HashSet<long>();
        private readonly HashSet<long> _runtimeAddresses = new HashSet<long>();
        public static ObservationEvidence Capture(QmpConnection qmp, string scratchDirectory, int captureTimeoutSeconds)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(captureTimeoutSeconds);
            ObservationScanner scanner = new ObservationScanner();
            using IncrementalHash memoryHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] carry = Array.Empty<byte>();
            for (int chunkIndex = 0; chunkIndex < GuestMemoryBytes / CaptureChunkBytes; chunkIndex++)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException("observation_capture_timeout");
                }

                long physicalAddress = (long)chunkIndex * CaptureChunkBytes;
                string chunkPath = Path.Combine(scratchDirectory, "memory-" + chunkIndex.ToString("D2", CultureInfo.InvariantCulture) + ".bin");
                if (File.Exists(chunkPath))
                {
                    throw new IOException("capture_chunk_already_exists");
                }

                try
                {
                    qmp.Pmemsave(physicalAddress, CaptureChunkBytes, chunkPath, chunkIndex);
                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new TimeoutException("observation_capture_timeout");
                    }

                    FileInfo info = new FileInfo(chunkPath);
                    if (!info.Exists || info.Length != CaptureChunkBytes)
                    {
                        throw new InvalidDataException("capture_chunk_size_mismatch_" + chunkIndex.ToString(CultureInfo.InvariantCulture));
                    }

                    byte[] bytes = File.ReadAllBytes(chunkPath);
                    memoryHash.AppendData(bytes);
                    byte[] scanBytes = new byte[carry.Length + bytes.Length];
                    Buffer.BlockCopy(carry, 0, scanBytes, 0, carry.Length);
                    Buffer.BlockCopy(bytes, 0, scanBytes, carry.Length, bytes.Length);
                    scanner.Scan(scanBytes, physicalAddress - carry.Length);
                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new TimeoutException("observation_capture_timeout");
                    }

                    int carryLength = Math.Min(ScanCarryBytes, scanBytes.Length);
                    carry = new byte[carryLength];
                    Buffer.BlockCopy(scanBytes, scanBytes.Length - carryLength, carry, 0, carryLength);
                }
                finally
                {
                    if (File.Exists(chunkPath))
                    {
                        File.Delete(chunkPath);
                    }
                }
            }

            string memorySha256 = Convert.ToHexString(memoryHash.GetHashAndReset());
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("observation_capture_timeout");
            }

            return scanner.Complete(memorySha256);
        }

        private void Scan(byte[] bytes, long physicalBase)
        {
            long firstAligned8 = (physicalBase + 7L) & ~7L;
            for (long physical = firstAligned8; physical >= 0 && physical + PreflightCandidate.Size <= physicalBase + bytes.Length; physical += 8)
            {
                int offset = checked((int)(physical - physicalBase));
                if (ReadUInt32(bytes, offset + 48) == 0x31464c50U && ReadUInt32(bytes, offset + 52) == 0 && _preflightAddresses.Add(physical))
                {
                    _preflight.Add(PreflightCandidate.Read(bytes, offset, physical));
                }
            }

            long firstPage = (physicalBase + 4095L) & ~4095L;
            for (long physical = firstPage; physical >= 0 && physical + RuntimeCandidate.TotalSpan <= physicalBase + bytes.Length; physical += 4096)
            {
                int offset = checked((int)(physical - physicalBase));
                RuntimeCandidate? candidate = RuntimeCandidate.TryRead(bytes, offset, physical);
                if (candidate is not null && _runtimeAddresses.Add(physical))
                {
                    _runtime.Add(candidate);
                }
            }
        }

        private ObservationEvidence Complete(string memorySha256)
        {
            if (_runtime.Count != 1)
            {
                throw new InvalidDataException("runtime_candidate_count_" + _runtime.Count.ToString(CultureInfo.InvariantCulture));
            }

            RuntimeCandidate runtime = _runtime[0];
            List<PreflightCandidate> matching = new List<PreflightCandidate>();
            foreach (PreflightCandidate candidate in _preflight)
            {
                if (candidate.Matches(runtime))
                {
                    matching.Add(candidate);
                }
            }

            if (matching.Count == 0)
            {
                throw new InvalidDataException("preflight_candidate_count_" + matching.Count.ToString(CultureInfo.InvariantCulture));
            }

            PreflightCandidate preflight = matching[0];
            for (int index = 1; index < matching.Count; index++)
            {
                if (!preflight.Bytes.AsSpan().SequenceEqual(matching[index].Bytes))
                {
                    throw new InvalidDataException("preflight_candidate_bytes_conflict");
                }
            }

            preflight.ValidateModeAndAddress(runtime);
            return new ObservationEvidence(memorySha256, matching, preflight, runtime);
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            return bytes[offset] | ((ulong)bytes[offset + 1] << 8) | ((ulong)bytes[offset + 2] << 16) | ((ulong)bytes[offset + 3] << 24) | ((ulong)bytes[offset + 4] << 32) | ((ulong)bytes[offset + 5] << 40) | ((ulong)bytes[offset + 6] << 48) | ((ulong)bytes[offset + 7] << 56);
        }

        private static byte[] Copy(byte[] source, int offset, int count)
        {
            byte[] copy = new byte[count];
            Buffer.BlockCopy(source, offset, copy, 0, count);
            return copy;
        }
    }
}
