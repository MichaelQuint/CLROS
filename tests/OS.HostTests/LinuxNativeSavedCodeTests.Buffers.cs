using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

internal static unsafe partial class LinuxNativeSavedCodeTests
{
    private sealed class Buffers : IDisposable
    {
        private byte* codeAllocation;
        private byte* copyAllocation;
        private readonly int allocationBytes;
        private readonly byte[] originalCode;
        private readonly byte[] originalCopy;
        internal int Length { get; }
        internal byte* Code => codeAllocation + 8;
        internal byte* Copy => copyAllocation + 8;

        internal Buffers(int length)
        {
            Length = length;
            allocationBytes = checked((length + 16 + 7) & ~7);
            codeAllocation = (byte*)NativeMemory.AlignedAlloc((nuint)allocationBytes, 8);
            try
            {
                copyAllocation = (byte*)NativeMemory.AlignedAlloc((nuint)allocationBytes, 8);
                // Unequal guards before and after the extent expose comparisons
                // beyond it, without claiming to detect unobserved machine loads.
                new Span<byte>(codeAllocation, allocationBytes).Fill(0xA5);
                new Span<byte>(copyAllocation, allocationBytes).Fill(0x5A);
                for (int i = 0; i < length; i++)
                    Code[i] = Copy[i] = unchecked((byte)(i * 73 + 19));
                originalCode = new ReadOnlySpan<byte>(codeAllocation, allocationBytes).ToArray();
                originalCopy = new ReadOnlySpan<byte>(copyAllocation, allocationBytes).ToArray();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void AssertUnchanged()
        {
            HostCheck.True(new ReadOnlySpan<byte>(codeAllocation, allocationBytes).SequenceEqual(originalCode), "live bytes and guards unchanged");
            HostCheck.True(new ReadOnlySpan<byte>(copyAllocation, allocationBytes).SequenceEqual(originalCopy), "saved bytes and guards unchanged");
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(codeAllocation);
            NativeMemory.AlignedFree(copyAllocation);
            codeAllocation = copyAllocation = null;
        }
    }
}
