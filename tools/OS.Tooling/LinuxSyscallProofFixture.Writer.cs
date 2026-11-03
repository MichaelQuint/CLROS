using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;
public static partial class LinuxSyscallProofFixture
{
    private sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public void Op(ulong value, int count)
        {
            for (int i = 0; i < count; i++)
                bytes[Position++] = (byte)(value >> (i * 8));
        }

        public void Immediate(uint register, ulong value)
        {
            Op(register < 8 ? 0x48UL : 0x49UL, 1);
            Op(0xb8 + (register & 7), 1);
            Op(value, 8);
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

        private void NotEqual()
        {
            Op(0x850f, 2);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public void CompareRax(long expected)
        {
            Op(0x3d48, 2);
            Op(unchecked((uint)expected), 4);
            NotEqual();
        }

        public void CompareRegister(uint register, ulong expected)
        {
            if (register == 0)
            {
                Immediate(2, expected);
                Op(0xd03948, 3);
            }
            else
            {
                Immediate(0, expected);
                Op(register < 8 ? 0x48UL : 0x49UL, 1);
                Op(0x39, 1);
                Op(0xc0 + (register & 7), 1);
            }

            NotEqual();
        }

        public void Syscall(ulong number, ulong fd, ulong buffer, ulong count)
        {
            Immediate(0, number);
            Immediate(7, fd);
            Immediate(6, buffer);
            Immediate(2, count);
            Op(0x050f, 2);
        }
    }
}
