using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static unsafe partial class ExistingIn8Out8StubBytesRWPagesFinalizeTests
{
    private sealed class AlignedPage : IDisposable
    {
        private byte* _pointer;
        internal AlignedPage()
        {
            _pointer = (byte*)NativeMemory.AlignedAlloc((nuint)ExistingIn8Out8StubBytesRWPagesFinalize.PageSize, (nuint)ExistingIn8Out8StubBytesRWPagesFinalize.RequiredPointerAlignment);
            if (_pointer == null)
            {
                throw new OutOfMemoryException("Could not allocate the M02-002 aligned test page.");
            }
        }

        internal byte* Pointer => _pointer;
        internal Span<byte> Span => new(_pointer, (int)ExistingIn8Out8StubBytesRWPagesFinalize.PageSize);
        internal ReadOnlySpan<byte> ReadOnlySpan => Span;

        internal void Fill(byte value)
        {
            Span.Fill(value);
        }

        internal bool IsAll(byte value)
        {
            foreach (byte item in Span)
            {
                if (item != value)
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (_pointer != null)
            {
                NativeMemory.AlignedFree(_pointer);
                _pointer = null;
            }
        }
    }
}
