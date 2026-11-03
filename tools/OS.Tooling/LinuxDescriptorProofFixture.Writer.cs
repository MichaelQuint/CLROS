using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;
public static partial class LinuxDescriptorProofFixture
{
    // Same explicit byte-writer pattern as M16-005/006. Zero-extended imm32
    // arguments keep both positive programs within one RX page. All fixture
    // pointers and Linux integer arguments fit uint; pread uses Linux R10.
    private sealed class Writer(byte[] bytes, int position)
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

        public void Rip(ulong opcode, int count, int target)
        {
            Op(opcode, count);
            Op(unchecked((uint)(target - (Position + 4))), 4);
        }

        public void JumpFailure()
        {
            Op(0xe9, 1);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void BranchFailure(byte condition)
        {
            Op(0x0f, 1);
            Op(condition, 1);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void CompareRax(long expected)
        {
            Op(0x3d48, 2);
            Op(unchecked((uint)expected), 4);
            BranchFailure(0x85);
        }

        public void PositiveRax()
        {
            Op(0xc08548, 3);
            BranchFailure(0x8e);
        }

        public void MemoryByte(int at, byte value)
        {
            Rip(0x05b60f, 3, at);
            CompareRax(value);
        }

        public void MemoryDword(int at, uint value)
        {
            Rip(0x058b, 2, at);
            CompareRax(value);
        }

        public void MemoryQword(int at, ulong value)
        {
            Rip(0x058b48, 3, at);
            Op(0xba48, 2);
            Op(value, 8);
            Op(0xd03948, 3);
            BranchFailure(0x85);
        }

        public void Pair(uint read, uint write)
        {
            MemoryDword(PairOffset, read);
            MemoryDword(PairOffset + 4, write);
        }

        public void CompareBytes(ulong destination, ulong source, uint count)
        {
            Immediate(7, checked((uint)destination));
            Immediate(6, checked((uint)source));
            Immediate(1, count);
            Op(0xfc, 1);
            Op(0xa6f3, 2);
            BranchFailure(0x85);
        }

        public void Call(uint number, ulong a0, ulong a1, ulong a2, long expected)
        {
            Syscall(number, a0, a1, a2);
            CompareRax(expected);
        }

        public void Syscall(uint number, ulong a0, ulong a1, ulong a2, ulong? a3 = null)
        {
            Immediate(0, number);
            Immediate(7, checked((uint)a0));
            Immediate(6, checked((uint)a1));
            Immediate(2, checked((uint)a2));
            if (a3.HasValue)
                Immediate(10, checked((uint)a3.Value));
            Op(0x050f, 2);
            Rips.Add(ImageBase + (uint)Position);
            if (a3.HasValue)
                Immediate(10, 0);
        }
    }
}
