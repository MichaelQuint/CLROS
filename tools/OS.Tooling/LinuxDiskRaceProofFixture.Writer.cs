using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;

public static partial class LinuxDiskRaceProofFixture
{
    private sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public readonly List<CallSpec> Calls = [];
        public readonly List<ulong> Instructions = [];
        private void Start() => Instructions.Add(ImageBase + (uint)Position);
        private void Op(ulong value, int count)
        {
            for (int i = 0; i < count; i++)
                bytes[Position++] = (byte)(value >> (i * 8));
        }

        public void Instruction(ulong opcode, int count)
        {
            Start();
            Op(opcode, count);
        }

        public void Immediate(uint register, uint value)
        {
            Start();
            if (register >= 8)
                Op(0x41, 1);
            Op(0xb8 + (register & 7), 1);
            Op(value, 4);
        }

        private void Rip(ulong opcode, int count, ulong address)
        {
            Start();
            Op(opcode, count);
            Op(unchecked((uint)((long)(address - ImageBase) - (Position + 4))), 4);
        }

        public void Branch(byte condition, int target)
        {
            Start();
            Op(0x0f, 1);
            Op(condition, 1);
            Op(unchecked((uint)(target - (Position + 4))), 4);
        }

        public void JumpFailure()
        {
            Start();
            Op(0xe9, 1);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void Compare(long expected)
        {
            Start();
            Op(0x3d48, 2);
            Op(unchecked((uint)expected), 4);
            Branch(0x85, FailureOffset);
        }

        public void MemoryByte(ulong address, byte value)
        {
            Rip(0x05b60f, 3, address);
            Compare(value);
        }

        public void MemoryDword(ulong address, uint value)
        {
            Rip(0x058b, 2, address);
            Compare(value);
        }

        public void MemoryQword(ulong address, uint value)
        {
            Rip(0x058b48, 3, address);
            Compare(value);
        }

        public void StoreDword(ulong address, uint value)
        {
            Start();
            Op(0x05c7, 2);
            Op(unchecked((uint)((long)(address - ImageBase) - (Position + 8))), 4);
            Op(value, 4);
        }

        public void Context(uint offset, long value)
        {
            Start();
            Op(0x828b48, 3);
            Op(offset, 4);
            Compare(value);
        }

        public void Fill(ulong address, uint count, byte value)
        {
            Immediate(0, value);
            Immediate(7, (uint)address);
            Immediate(1, count);
            Instruction(0xfc, 1);
            Instruction(0xaef3, 2);
            Branch(0x85, FailureOffset);
        }

        public ulong Syscall(string role, uint number, ulong a0, ulong a1, ulong a2, long result, WaitPolicy wait = WaitPolicy.None, uint output = 0, ulong a3 = 0, ulong a4 = 0)
        {
            Immediate(0, number);
            Immediate(7, checked((uint)a0));
            Immediate(6, checked((uint)a1));
            Immediate(2, checked((uint)a2));
            Immediate(10, checked((uint)a3));
            Immediate(8, checked((uint)a4));
            Instruction(0x050f, 2);
            ulong rip = ImageBase + (uint)Position;
            Calls.Add(new(role, number, a0, a1, a2, a3, a4, 0, rip, result, wait, output));
            return rip;
        }

        public void Call(string role, uint number, ulong a0, ulong a1, ulong a2, long result, WaitPolicy wait = WaitPolicy.None, uint output = 0, ulong a3 = 0)
        {
            Syscall(role, number, a0, a1, a2, result, wait, output, a3);
            Compare(result);
        }

        public void Join()
        {
            Syscall("main.join", 202, ClearTid, 0, 2, 0, WaitPolicy.Join);
            Instruction(0xc08548, 3);
            int jump = Position;
            Branch(0x84, 0);
            Compare(-11);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(jump + 2), Position - (jump + 6));
        }
    }
}
