using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;
public static partial class LinuxSignalProofFixture
{
    // Marks complete instructions, never operands, including temporary PUSHFQ
    // stack depth. Reuses only the existing RIP encoding/syscall writer.
    private sealed class IrqWriter(byte[] elf, int start, List<IrqPoint> points)
    {
        private readonly Writer _w = new(elf, start);
        internal readonly List<Invocation> Invocations = [];
        internal int Calls;
        internal bool Child, Handler, Direction, Loop;
        internal ulong Rsp, Fs, Gs;
        internal int Position => _w.Position;

        private void Mark() => points.Add(new(Address(Position), Calls, Child, Handler, Rsp, Fs, Gs, Direction, Loop));
        internal void I(ulong op, int bytes, ulong operand = 0, int operandBytes = 0)
        {
            Mark();
            _w.Op(op, bytes);
            _w.Op(operand, operandBytes);
        }

        internal void Mov(uint register, uint value)
        {
            Mark();
            _w.Immediate(register, value);
        }

        internal void Mov64(uint register, ulong value)
        {
            Mark();
            LinuxSignalProofFixture.Mov64(_w, register, value);
        }

        internal void ToRax(uint register)
        {
            Mark();
            LinuxSignalProofFixture.ToRax(_w, register);
        }

        internal void Branch(byte condition, int target = FailureOffset)
        {
            Mark();
            _w.Branch(condition, target);
        }

        internal void JumpFailure()
        {
            Mark();
            _w.JumpFailure();
        }

        internal void Compare(long value)
        {
            I(0x3d48, 2, unchecked((uint)value), 4);
            Branch(0x85);
        }

        internal void Compare64(ulong value)
        {
            Mov64(11, value);
            I(0xd8394c, 3);
            Branch(0x85);
        }

        internal void Rip(ulong op, int bytes, int target)
        {
            Mark();
            _w.Rip(op, bytes, target);
        }

        internal void Store(int offset) => Rip(0x0589, 2, offset);
        internal void Load(int offset, bool wide = false) => Rip(wide ? 0x058b48UL : 0x058bUL, wide ? 3 : 2, offset);
        internal void Store32(int offset, uint value)
        {
            Mark();
            _w.Store32(offset, value);
        }

        internal void Memory(int offset, uint value)
        {
            Load(offset);
            Compare(value);
        }

        internal void CmpMemoryZero(int offset)
        {
            Mark();
            _w.Rip(0x3d83, 2, offset, 1);
            _w.Op(0, 1);
        }

        internal void Context(uint offset) => I(0x828b48, 3, offset, 4);
        internal void Edit(uint offset, uint value)
        {
            Mark();
            _w.Op(0x82c748, 3);
            _w.Op(offset, 4);
            _w.Op(value, 4);
        }

        internal void Segment(bool gs)
        {
            I(gs ? 0x25048b4865UL : 0x25048b4864UL, 5, 0, 4);
        }

        internal void Fp(int offset)
        {
            Rip(0x3dd9, 2, offset);
            Rip(0x1dae0f, 3, offset + 4);
        }

        internal void CheckDirection(uint expected)
        {
            I(0x9c, 1);
            Rsp -= 8;
            I(0x58, 1);
            Rsp += 8;
            I(0x25, 1, 0x600, 4);
            Compare(expected);
        }

        internal void Syscall(uint number)
        {
            Mark();
            _w.Finish();
            Invocations.Add(new(number, _w.Rips[^1], Handler));
            Calls++;
        }

        internal void Call(uint number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0)
        {
            Mov(0, number);
            Mov(7, checked((uint)a0));
            Mov(6, checked((uint)a1));
            Mov(2, checked((uint)a2));
            Mov(10, checked((uint)a3));
            Mov(8, checked((uint)a4));
            Mov(9, 0);
            Syscall(number);
        }
    }
}
