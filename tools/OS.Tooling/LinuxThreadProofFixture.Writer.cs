using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;

public static partial class LinuxThreadProofFixture
{
    // All syscall arguments are explicit. Only pipe fd arguments are replaced
    // with actual pair loads; no allocation order or initial TID is assumed.
    internal sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public readonly List<ulong> Rips = [];
        public void Op(ulong value, int count)
        {
            for (int i = 0; i < count; i++)
                bytes[Position++] = (byte)(value >> (i * 8));
        }

        public void Immediate(uint register, uint value)
        {
            if (register >= 8)
                Op(0x41, 1);
            Op(0xb8 + (register & 7), 1);
            Op(value, 4);
        }

        internal void Rip(ulong op, int length, int target, int tail = 0)
        {
            Op(op, length);
            Op(unchecked((uint)(target - (Position + 4 + tail))), 4);
        }

        public void Branch(byte condition, int target = FailureOffset)
        {
            Op(0x0f, 1);
            Op(condition, 1);
            Op(unchecked((uint)(target - (Position + 4))), 4);
        }

        public void JumpFailure()
        {
            Op(0xe9, 1);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void Compare(long value)
        {
            Op(0x3d48, 2);
            Op(unchecked((uint)value), 4);
            Branch(0x85);
        }

        public void Positive()
        {
            Op(0xc08548, 3);
            Branch(0x8e);
        }

        public void CompareR12(byte condition = 0x85)
        {
            Op(0xe0394c, 3);
            Branch(condition);
        }

        public void StoreRax(int offset) => Rip(0x0589, 2, offset);
        public void CompareMemory(int offset)
        {
            Rip(0x053b, 2, offset);
            Branch(0x85);
        }

        public void Store32(int offset, uint value)
        {
            Rip(0x05c7, 2, offset, 4);
            Op(value, 4);
        }

        public void Memory(int offset, ulong value, bool qword = false)
        {
            Rip(qword ? 0x058b48UL : 0x058bUL, qword ? 3 : 2, offset);
            Compare((long)value);
        }

        public void Fs()
        {
            Op(0x64, 1);
            Op(0x25048b48, 8);
        } // MOV RAX,QWORD PTR FS:[0].

        public void Call(uint number, ulong a0, ulong a1, ulong a2, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            Arguments(number, a0, a1, a2, a3, a4, a5);
            Finish();
        }

        public void FdCall(uint number, int fdOffset, ulong address, uint count)
        {
            Arguments(number, 0, address, count, 0, 0, 0);
            Rip(0x3d8b, 2, fdOffset);
            Finish();
        }

        internal void Arguments(uint number, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong a5)
        {
            Immediate(0, number);
            Immediate(7, checked((uint)a0));
            Immediate(6, checked((uint)a1));
            Immediate(2, checked((uint)a2));
            Immediate(10, checked((uint)a3));
            Immediate(8, checked((uint)a4));
            Immediate(9, checked((uint)a5));
        }

        internal void Finish()
        {
            Op(0x050f, 2);
            Rips.Add(ImageBase + (uint)Position);
        }
    }
}
