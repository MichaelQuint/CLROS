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
    private sealed class CanonicalTranscript : IDisposable
    {
        private readonly MemoryStream _stream = new();
        private bool _finished;
        public void AddByte(byte value)
        {
            EnsureOpen();
            _stream.WriteByte(value);
        }

        public void AddBool(bool value)
        {
            AddByte(value ? (byte)1 : (byte)0);
        }

        public void AddInt32(int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            AddBytes(bytes);
        }

        public void AddUInt64(ulong value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            AddBytes(bytes);
        }

        public string Finish()
        {
            EnsureOpen();
            _finished = true;
            _stream.Position = 0;
            return Convert.ToHexString(SHA256.HashData(_stream));
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private void AddBytes(ReadOnlySpan<byte> bytes)
        {
            EnsureOpen();
            _stream.Write(bytes);
        }

        private void EnsureOpen()
        {
            if (_finished)
            {
                throw new InvalidOperationException("transcript_already_finished");
            }
        }
    }
}
