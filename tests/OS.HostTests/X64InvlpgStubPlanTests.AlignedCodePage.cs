using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OS.Boot;
using OS.Kernel.Memory;

internal static unsafe partial class X64InvlpgStubPlanTests
{
    private sealed unsafe class AlignedCodePage : IDisposable
    {
        private readonly byte* _allocation;
        internal AlignedCodePage()
        {
            _allocation = (byte*)NativeMemory.AlignedAlloc((nuint)(X64InvlpgStubPlan.PageSize + X64InvlpgStubPlan.RequiredPointerAlignment), (nuint)X64InvlpgStubPlan.RequiredPointerAlignment);
            if (_allocation == null)
            {
                throw new OutOfMemoryException("Unable to allocate INVLPG host fixture.");
            }
        }

        internal byte* Pointer => _allocation;

        internal void Fill(byte value)
        {
            new Span<byte>(_allocation, checked((int)X64InvlpgStubPlan.PageSize)).Fill(value);
        }

        internal bool IsAll(byte value)
        {
            ReadOnlySpan<byte> bytes = new(_allocation, checked((int)X64InvlpgStubPlan.PageSize));
            foreach (byte current in bytes)
            {
                if (current != value)
                    return false;
            }

            return true;
        }

        internal bool IsSentinelExcept(ulong offset, ulong length)
        {
            ReadOnlySpan<byte> bytes = new(_allocation, checked((int)X64InvlpgStubPlan.PageSize));
            for (int index = 0; index < bytes.Length; index++)
            {
                bool inside = (ulong)index >= offset && (ulong)index < offset + length;
                if (!inside && bytes[index] != Sentinel)
                    return false;
            }

            return true;
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(_allocation);
        }
    }
}
