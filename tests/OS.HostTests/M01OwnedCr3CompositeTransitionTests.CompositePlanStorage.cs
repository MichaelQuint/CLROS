using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static unsafe partial class M01OwnedCr3CompositeTransitionTests
{
    private sealed unsafe class CompositePlanStorage : IDisposable
    {
        private const int ByteTailLength = 32;
        private const int SemanticTailRecords = 2;
        private readonly int _byteStorageLength;
        private readonly int _semanticStorageLength;
        private readonly bool _ownsBytes;
        internal CompositePlanStorage()
        {
            _byteStorageLength = checked((int)M01OwnedCr3CompositeTransition.MaximumByteCapacity + ByteTailLength);
            _semanticStorageLength = checked((int)(M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount + SemanticTailRecords) * sizeof(M01OwnedCr3CompositeTransitionSemanticRecord));
            _ownsBytes = true;
            Bytes = (byte*)NativeMemory.AlignedAlloc((nuint)_byteStorageLength, 16);
            Semantics = (M01OwnedCr3CompositeTransitionSemanticRecord*)NativeMemory.AlignedAlloc((nuint)_semanticStorageLength, 16);
            if (Bytes == null || Semantics == null)
            {
                Dispose();
                throw new OutOfMemoryException();
            }
        }

        internal CompositePlanStorage(byte* canonicalCodePage)
        {
            if (canonicalCodePage == null || ((ulong)canonicalCodePage & (M01OwnedCr3CompositeTransition.EntryAlignment - 1)) != 0)
            {
                throw new ArgumentException("Canonical composite code page is missing or misaligned.", nameof(canonicalCodePage));
            }

            _byteStorageLength = checked((int)M01OwnedCr3CompositeTransition.MaterializedPageByteLength);
            _semanticStorageLength = checked((int)(M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount + SemanticTailRecords) * sizeof(M01OwnedCr3CompositeTransitionSemanticRecord));
            _ownsBytes = false;
            Bytes = canonicalCodePage;
            Semantics = (M01OwnedCr3CompositeTransitionSemanticRecord*)NativeMemory.AlignedAlloc((nuint)_semanticStorageLength, 16);
            if (Semantics == null)
            {
                Bytes = null;
                throw new OutOfMemoryException();
            }
        }

        internal byte* Bytes { get; private set; }
        internal M01OwnedCr3CompositeTransitionSemanticRecord* Semantics { get; private set; }

        internal void Fill(byte value)
        {
            new Span<byte>(Bytes, _byteStorageLength).Fill(value);
            new Span<byte>(Semantics, _semanticStorageLength).Fill(value);
        }

        internal byte[] CopyAllBytes()
        {
            return new ReadOnlySpan<byte>(Bytes, _byteStorageLength).ToArray();
        }

        internal byte[] CopyAllSemantics()
        {
            return new ReadOnlySpan<byte>(Semantics, _semanticStorageLength).ToArray();
        }

        internal byte[] CopyBytes(ulong count)
        {
            return new ReadOnlySpan<byte>(Bytes, checked((int)count)).ToArray();
        }

        internal byte[] CopySemantics(ulong count)
        {
            return new ReadOnlySpan<byte>(Semantics, checked((int)(count * M01OwnedCr3CompositeTransitionSemanticRecord.StructureSize))).ToArray();
        }

        internal bool AllStorageEquals(byte[] bytes, byte[] semantics)
        {
            return new ReadOnlySpan<byte>(Bytes, _byteStorageLength).SequenceEqual(bytes) && new ReadOnlySpan<byte>(Semantics, _semanticStorageLength).SequenceEqual(semantics);
        }

        internal bool ExactPrefixesEqual(byte[] bytes, byte[] semantics)
        {
            return new ReadOnlySpan<byte>(Bytes, bytes.Length).SequenceEqual(bytes) && new ReadOnlySpan<byte>(Semantics, semantics.Length).SequenceEqual(semantics);
        }

        internal bool TailsEqual(byte value)
        {
            ReadOnlySpan<byte> byteTail = (ulong)_byteStorageLength > M01OwnedCr3CompositeTransition.MaximumByteCapacity ? new ReadOnlySpan<byte>(Bytes + M01OwnedCr3CompositeTransition.MaximumByteCapacity, checked(_byteStorageLength - (int)M01OwnedCr3CompositeTransition.MaximumByteCapacity)) : ReadOnlySpan<byte>.Empty;
            ReadOnlySpan<byte> semanticTail = new((byte*)Semantics + M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount * M01OwnedCr3CompositeTransitionSemanticRecord.StructureSize, SemanticTailRecords * checked((int)M01OwnedCr3CompositeTransitionSemanticRecord.StructureSize));
            for (int index = 0; index < byteTail.Length; index++)
            {
                if (byteTail[index] != value)
                {
                    return false;
                }
            }

            for (int index = 0; index < semanticTail.Length; index++)
            {
                if (semanticTail[index] != value)
                {
                    return false;
                }
            }

            return true;
        }

        internal bool ZeroFilledPageTail(ulong instructionByteLength)
        {
            if (instructionByteLength == 0 || instructionByteLength > M01OwnedCr3CompositeTransition.MaterializedPageByteLength)
            {
                return false;
            }

            ReadOnlySpan<byte> tail = new(Bytes + instructionByteLength, checked((int)(M01OwnedCr3CompositeTransition.MaterializedPageByteLength - instructionByteLength)));
            for (int index = 0; index < tail.Length; index++)
            {
                if (tail[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            byte* bytes = Bytes;
            Bytes = null;
            if (_ownsBytes && bytes != null)
            {
                NativeMemory.AlignedFree(bytes);
            }

            M01OwnedCr3CompositeTransitionSemanticRecord* semantics = Semantics;
            Semantics = null;
            if (semantics != null)
            {
                NativeMemory.AlignedFree(semantics);
            }
        }
    }
}
