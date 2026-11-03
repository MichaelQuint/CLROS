using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;
public static partial class LinuxMemoryProofFixture
{
    // Same narrow C# byte writer as the earlier syscall fixture; six arguments
    // and local forward branches are needed for the Linux memory ABI.
    private sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public uint Calls;
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

        public void BranchFailure(byte condition)
        {
            Op(0x0f, 1);
            Op(condition, 1);
            Op(unchecked((uint)(FailureOffset - (Position + 4))), 4);
        }

        public int ForwardBranch(byte condition)
        {
            Op(0x0f, 1);
            Op(condition, 1);
            int at = Position;
            Op(0, 4);
            return at;
        }

        public void PatchBranch(int at) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), unchecked((uint)(Position - (at + 4))));
        public void CompareRax(long expected)
        {
            Op(0x3d48, 2);
            Op(unchecked((uint)expected), 4);
            BranchFailure(0x85);
        }

        public void CompareRegister(uint register, ulong expected)
        {
            if (register != 0)
                throw new ToolFailure("m16_006_fixture_compare_register");
            Immediate(2, expected);
            Op(0xd03948, 3);
            BranchFailure(0x85);
        }

        public void NonzeroRax()
        {
            Op(0xc08548, 3);
            BranchFailure(0x84);
        }

        public void NonzeroPair(int at)
        {
            Rip(0x058b48, 3, at);
            Rip(0x050b48, 3, at + 8);
            NonzeroRax();
        }

        public void ReadCompare(ulong address, ulong expected)
        {
            Immediate(0, address);
            Op(0x008b48, 3);
            CompareRegister(0, expected);
        }

        public void ZeroRegion(ulong address, ulong bytes) => CompareRegion(address, bytes, 0);
        public void CompareRegion(ulong address, ulong bytes, ulong value)
        {
            Immediate(0, value);
            Immediate(7, address);
            Immediate(1, bytes / 8);
            Op(0xfc, 1);
            Op(0xaf48f3, 3);
            BranchFailure(0x85);
        }

        public void FillRegion(ulong address, ulong bytes, ulong value)
        {
            Immediate(0, value);
            Immediate(7, address);
            Immediate(1, bytes / 8);
            Op(0xfc, 1);
            Op(0xab48f3, 3);
        }

        public void Map(ulong address, ulong size, ulong protection, ulong flags) => Syscall(9, address, size, protection, flags, ulong.MaxValue, 0);
        public void Syscall(ulong number, ulong a0, ulong a1, ulong a2, ulong? a3 = null, ulong? a4 = null, ulong? a5 = null)
        {
            Immediate(0, number);
            Immediate(7, a0);
            Immediate(6, a1);
            Immediate(2, a2);
            Immediate(10, a3 ?? 0);
            Immediate(8, a4 ?? 0);
            Immediate(9, a5 ?? 0);
            Op(0x050f, 2);
            Calls++;
        }
    }
}
