using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class BootPageAllocatorStressTests
{
    private readonly struct ReplaySummary
    {
        public ReplaySummary(ulong seed, int churnSteps, int operationCount, int successfulAllocations, int successfulFrees, int expectedAllocationFailures, int exactRestorationCount, int maximumDepth, bool firstBoundaryPageSeen, bool lastBoundaryPageSeen, bool capacityTailPreserved, bool reservationsPreserved, bool returnedToBaseline, string transcriptHash)
        {
            Seed = seed;
            ChurnSteps = churnSteps;
            OperationCount = operationCount;
            SuccessfulAllocations = successfulAllocations;
            SuccessfulFrees = successfulFrees;
            ExpectedAllocationFailures = expectedAllocationFailures;
            ExactRestorationCount = exactRestorationCount;
            MaximumDepth = maximumDepth;
            FirstBoundaryPageSeen = firstBoundaryPageSeen;
            LastBoundaryPageSeen = lastBoundaryPageSeen;
            CapacityTailPreserved = capacityTailPreserved;
            ReservationsPreserved = reservationsPreserved;
            ReturnedToBaseline = returnedToBaseline;
            TranscriptHash = transcriptHash;
        }

        public ulong Seed
        {
            get;
        }
        public int ChurnSteps
        {
            get;
        }
        public int OperationCount
        {
            get;
        }
        public int SuccessfulAllocations
        {
            get;
        }
        public int SuccessfulFrees
        {
            get;
        }
        public int ExpectedAllocationFailures
        {
            get;
        }
        public int ExactRestorationCount
        {
            get;
        }
        public int MaximumDepth
        {
            get;
        }
        public bool FirstBoundaryPageSeen
        {
            get;
        }
        public bool LastBoundaryPageSeen
        {
            get;
        }
        public bool CapacityTailPreserved
        {
            get;
        }
        public bool ReservationsPreserved
        {
            get;
        }
        public bool ReturnedToBaseline
        {
            get;
        }
        public string TranscriptHash
        {
            get;
        }

        public bool Equals(ReplaySummary other)
        {
            return Seed == other.Seed && ChurnSteps == other.ChurnSteps && OperationCount == other.OperationCount && SuccessfulAllocations == other.SuccessfulAllocations && SuccessfulFrees == other.SuccessfulFrees && ExpectedAllocationFailures == other.ExpectedAllocationFailures && ExactRestorationCount == other.ExactRestorationCount && MaximumDepth == other.MaximumDepth && FirstBoundaryPageSeen == other.FirstBoundaryPageSeen && LastBoundaryPageSeen == other.LastBoundaryPageSeen && CapacityTailPreserved == other.CapacityTailPreserved && ReservationsPreserved == other.ReservationsPreserved && ReturnedToBaseline == other.ReturnedToBaseline && string.Equals(TranscriptHash, other.TranscriptHash, StringComparison.Ordinal);
        }
    }
}
