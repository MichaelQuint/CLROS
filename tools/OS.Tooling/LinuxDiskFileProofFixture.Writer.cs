using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;
public static partial class LinuxDiskFileProofFixture
{
    private sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public readonly List<ulong> Rips = [];
        public readonly List<ulong> Instructions = [];
        private void Start() => Instructions.Add(ImageBase + (uint)Position);
        public void Instruction(ulong opcode, int count)
        {
            Start();
            Op(opcode, count);
        }

        public void Op(ulong value, int count)
        {
            for (int i = 0; i < count; i++)
                bytes[Position++] = (byte)(value >> (i * 8));
        }

        public void Immediate(uint register, uint value)
        {
            Start();
            if (register >= 8)
                Op(0x41, 1);
            Op(0xb8 + (register & 7), 1);
            Op(value, 4);
        }

        public void Rip(ulong opcode, int count, int target)
        {
            Start();
            Op(opcode, count);
            Op(unchecked((uint)(target - (Position + 4))), 4);
        }

        public void JumpFailure()
        {
            Start();
            Op(0xe9, 1);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void BranchFailure(byte condition)
        {
            Start();
            Op(0x0f, 1);
            Op(condition, 1);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void CompareRax(long expected)
        {
            Start();
            Op(0x3d48, 2);
            Op(unchecked((uint)expected), 4);
            BranchFailure(0x85);
        }

        public void PositiveRax()
        {
            Instruction(0xc08548, 3);
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
            Start();
            Op(0xba48, 2);
            Op(value, 8);
            Instruction(0xd03948, 3);
            BranchFailure(0x85);
        }

        public void CompareBytes(ulong destination, ulong source, uint count)
        {
            Immediate(7, checked((uint)destination));
            Immediate(6, checked((uint)source));
            Immediate(1, count);
            Instruction(0xfc, 1);
            Instruction(0xa6f3, 2);
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
            Instruction(0x050f, 2);
            Rips.Add(ImageBase + (uint)Position);
            if (a3.HasValue)
                Immediate(10, 0);
        }
    }
}
