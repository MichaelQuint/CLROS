using System.Buffers.Binary;
using System.Text;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Tooling;
public static partial class LinuxWritableDiskProofFixture
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

        public void Instruction(ulong value, int count)
        {
            Start();
            Op(value, count);
        }

        private void Immediate(uint register, ulong value)
        {
            Start();
            if (register >= 8)
                Op(0x41, 1);
            Op(0xb8 + (register & 7), 1);
            Op(checked((uint)value), 4);
        }

        private void Rip(ulong opcode, int count, ulong address)
        {
            Start();
            Op(opcode, count);
            Op(unchecked((uint)((long)(address - ImageBase) - (Position + 4))), 4);
        }

        private void BranchFailure()
        {
            Start();
            Op(0x850f, 2);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void JumpFailure()
        {
            Start();
            Op(0xe9, 1);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void Compare(long value)
        {
            Start();
            Op(0x3d48, 2);
            Op(unchecked((uint)value), 4);
            BranchFailure();
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

        public void CompareBytes(ulong destination, ulong source, uint count)
        {
            Immediate(7, destination);
            Immediate(6, source);
            Immediate(1, count);
            Instruction(0xfc, 1);
            Instruction(0xa6f3, 2);
            BranchFailure();
        }

        public void Call(string role, uint number, ulong a0, ulong a1, ulong a2, long result, uint output = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            Syscall(role, number, a0, a1, a2, result, output, a3, a4, a5);
            Compare(result);
        }

        public void Syscall(string role, uint number, ulong a0, ulong a1, ulong a2, long result, uint output = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            Immediate(0, number);
            Immediate(7, a0);
            Immediate(6, a1);
            Immediate(2, a2);
            Immediate(10, a3);
            Immediate(8, a4);
            Immediate(9, a5);
            Instruction(0x050f, 2);
            Calls.Add(new(role, number, a0, a1, a2, a3, a4, a5, ImageBase + (uint)Position, result, output));
        }
    }
}
