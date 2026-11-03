namespace OS.Kernel.Memory;

public static unsafe partial class X64ReversibleCr3TransitionStubPlan
{
    private struct StubBuilder
    {
        private readonly byte* _buffer;
        private readonly ulong _capacity;
        private readonly StubBuilderMode _mode;
        private ulong _position;
        private ulong _instructionCount;
        private ulong _invalidByteIndex;
        private byte _failed;
        internal StubBuilder(byte* buffer, ulong capacity, StubBuilderMode mode)
        {
            _buffer = buffer;
            _capacity = capacity;
            _mode = mode;
            _position = 0;
            _instructionCount = 0;
            _invalidByteIndex = X64ReversibleCr3TransitionStubPlanResult.NoIndex;
            _failed = 0;
        }

        internal bool Succeeded => _failed == 0;
        internal ulong Position => _position;
        internal ulong InstructionCount => _instructionCount;
        internal ulong InvalidByteIndex => _invalidByteIndex;

        internal void Instruction()
        {
            _instructionCount++;
        }

        internal void Byte(byte value)
        {
            ulong index = _position;
            _position++;
            if (index >= _capacity)
            {
                Fail(index);
                return;
            }

            if (_mode == StubBuilderMode.Measure)
            {
                return;
            }

            if (_mode == StubBuilderMode.Emit)
            {
                _buffer[index] = value;
                return;
            }

            if (_mode != StubBuilderMode.Validate || _buffer[index] != value)
            {
                Fail(index);
            }
        }

        internal void UInt16(ushort value)
        {
            Byte((byte)value);
            Byte((byte)(value >> 8));
        }

        internal void UInt32(uint value)
        {
            Byte((byte)value);
            Byte((byte)(value >> 8));
            Byte((byte)(value >> 16));
            Byte((byte)(value >> 24));
        }

        internal void UInt64(ulong value)
        {
            UInt32((uint)value);
            UInt32((uint)(value >> 32));
        }

        internal ulong Rel32Placeholder()
        {
            ulong offset = _position;
            for (byte index = 0; index < 4; index++)
            {
                if (_mode == StubBuilderMode.Emit)
                {
                    Byte(0);
                }
                else if (_mode == StubBuilderMode.Measure)
                {
                    Byte(0);
                }
                else
                {
                    ulong current = _position;
                    _position++;
                    if (current >= _capacity)
                    {
                        Fail(current);
                    }
                }
            }

            return offset;
        }

        internal void PatchRel32(ulong displacementOffset, ulong target)
        {
            if (displacementOffset + 4 < displacementOffset || displacementOffset + 4 > _capacity)
            {
                Fail(displacementOffset);
                return;
            }

            long displacement = (long)target - (long)(displacementOffset + 4);
            if (displacement < -2147483648L || displacement > 2147483647L)
            {
                Fail(displacementOffset);
                return;
            }

            uint encoded = unchecked((uint)displacement);
            PatchByte(displacementOffset, (byte)encoded);
            PatchByte(displacementOffset + 1, (byte)(encoded >> 8));
            PatchByte(displacementOffset + 2, (byte)(encoded >> 16));
            PatchByte(displacementOffset + 3, (byte)(encoded >> 24));
        }

        private void PatchByte(ulong index, byte value)
        {
            if (_mode == StubBuilderMode.Measure)
            {
                return;
            }

            if (_mode == StubBuilderMode.Emit)
            {
                _buffer[index] = value;
                return;
            }

            if (_mode != StubBuilderMode.Validate || _buffer[index] != value)
            {
                Fail(index);
            }
        }

        private void Fail(ulong index)
        {
            if (_failed == 0)
            {
                _failed = 1;
                _invalidByteIndex = index;
            }
        }
    }
}
