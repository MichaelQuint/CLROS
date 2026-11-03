namespace OS.Boot;

public static unsafe partial class M01OwnedCr3CompositeTransition
{
    private struct PlanWriter
    {
        internal PlanWriterMode Mode;
        internal byte* Bytes;
        internal ulong ByteCapacity;
        internal M01OwnedCr3CompositeTransitionSemanticRecord* Semantics;
        internal ulong SemanticCapacity;
        internal ulong Position;
        internal ulong InstructionCount;
        internal ulong SemanticCount;
        internal ulong SemanticStart;
        internal M01OwnedCr3CompositeTransitionOperation SemanticOperation;
        internal M01OwnedCr3CompositeTransitionRecoveryClass SemanticRecovery;
        internal bool SemanticOpen;
        internal bool Succeeded;
        internal bool SemanticMismatch;
        internal bool SkipPlaceholderComparison;
        internal ulong InvalidIndex;
        internal ulong ExpectedValue;
        internal ulong ObservedValue;
        internal ulong PreCliFailureTarget;
        internal PlanWriter(PlanWriterMode mode, byte* bytes, ulong byteCapacity, M01OwnedCr3CompositeTransitionSemanticRecord* semantics, ulong semanticCapacity)
        {
            Mode = mode;
            Bytes = bytes;
            ByteCapacity = byteCapacity;
            Semantics = semantics;
            SemanticCapacity = semanticCapacity;
            Position = 0;
            InstructionCount = 0;
            SemanticCount = 0;
            SemanticStart = 0;
            SemanticOperation = M01OwnedCr3CompositeTransitionOperation.Uninitialized;
            SemanticRecovery = M01OwnedCr3CompositeTransitionRecoveryClass.Uninitialized;
            SemanticOpen = false;
            Succeeded = true;
            SemanticMismatch = false;
            SkipPlaceholderComparison = false;
            InvalidIndex = M01OwnedCr3CompositeTransitionResult.NoIndex;
            ExpectedValue = 0;
            ObservedValue = 0;
            PreCliFailureTarget = M01OwnedCr3CompositeTransitionResult.NoIndex;
        }

        internal void Instruction()
        {
            if (Succeeded)
            {
                InstructionCount++;
            }
        }

        internal void RequirePosition(ulong expected)
        {
            if (Succeeded && Position != expected)
            {
                Fail(Position, expected, Position, semantic: false);
            }
        }

        internal void Byte(byte value)
        {
            if (!Succeeded)
            {
                return;
            }

            if (Position >= ByteCapacity)
            {
                Fail(Position, value, 0, semantic: false);
                return;
            }

            if (Mode == PlanWriterMode.Emit)
            {
                Bytes[Position] = value;
            }
            else if (Mode == PlanWriterMode.Compare && !SkipPlaceholderComparison && Bytes[Position] != value)
            {
                Fail(Position, value, Bytes[Position], semantic: false);
                return;
            }

            Position++;
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
            ulong offset = Position;
            SkipPlaceholderComparison = true;
            UInt32(0);
            SkipPlaceholderComparison = false;
            return offset;
        }

        internal ulong Rel8Placeholder()
        {
            ulong offset = Position;
            SkipPlaceholderComparison = true;
            Byte(0);
            SkipPlaceholderComparison = false;
            return offset;
        }

        internal void PatchRel8(ulong placeholder, ulong target)
        {
            if (!Succeeded || placeholder >= Position)
            {
                Fail(placeholder, target, Position, semantic: false);
                return;
            }

            long displacement = (long)target - (long)(placeholder + 1);
            if (displacement < -128 || displacement > 127)
            {
                Fail(placeholder, target, (ulong)displacement, semantic: false);
                return;
            }

            byte encoded = unchecked((byte)(sbyte)displacement);
            if (Mode == PlanWriterMode.Emit)
            {
                Bytes[placeholder] = encoded;
            }
            else if (Mode == PlanWriterMode.Compare && Bytes[placeholder] != encoded)
            {
                Fail(placeholder, encoded, Bytes[placeholder], semantic: false);
            }
        }

        internal void PatchRel32(ulong placeholder, ulong target)
        {
            if (!Succeeded || placeholder > Position || placeholder > ~0UL - 4)
            {
                Fail(placeholder, target, Position, semantic: false);
                return;
            }

            long displacement = (long)target - (long)(placeholder + 4);
            if (displacement < MinimumRel32Displacement || displacement > MaximumRel32Displacement)
            {
                Fail(placeholder, target, (ulong)displacement, semantic: false);
                return;
            }

            uint encoded = unchecked((uint)(int)displacement);
            if (Mode == PlanWriterMode.Emit)
            {
                Bytes[placeholder] = (byte)encoded;
                Bytes[placeholder + 1] = (byte)(encoded >> 8);
                Bytes[placeholder + 2] = (byte)(encoded >> 16);
                Bytes[placeholder + 3] = (byte)(encoded >> 24);
            }
            else if (Mode == PlanWriterMode.Compare)
            {
                uint observed = (uint)Bytes[placeholder] | ((uint)Bytes[placeholder + 1] << 8) | ((uint)Bytes[placeholder + 2] << 16) | ((uint)Bytes[placeholder + 3] << 24);
                if (observed != encoded)
                {
                    Fail(placeholder, encoded, observed, semantic: false);
                }
            }
        }

        internal void BeginSemantic(M01OwnedCr3CompositeTransitionOperation operation, M01OwnedCr3CompositeTransitionRecoveryClass recovery)
        {
            if (!Succeeded || SemanticOpen || operation == M01OwnedCr3CompositeTransitionOperation.Uninitialized || recovery == M01OwnedCr3CompositeTransitionRecoveryClass.Uninitialized)
            {
                Fail(SemanticCount, (ulong)operation, (ulong)recovery, semantic: true);
                return;
            }

            SemanticOpen = true;
            SemanticStart = Position;
            SemanticOperation = operation;
            SemanticRecovery = recovery;
        }

        internal void EndSemantic(ulong stateOffsetOrConstant = 0, uint branchTargetOffset = M01OwnedCr3CompositeTransitionSemanticRecord.NoBranchTarget)
        {
            if (!Succeeded || !SemanticOpen || SemanticCount >= SemanticCapacity || Position < SemanticStart || Position - SemanticStart > MaximumUInt32Value || SemanticStart > MaximumUInt32Value)
            {
                Fail(SemanticCount, SemanticStart, Position, semantic: true);
                return;
            }

            M01OwnedCr3CompositeTransitionSemanticRecord record = new(SemanticOperation, SemanticRecovery, (uint)SemanticStart, (uint)(Position - SemanticStart), branchTargetOffset, stateOffsetOrConstant);
            if (Mode == PlanWriterMode.Emit)
            {
                Semantics[SemanticCount] = record;
            }
            else if (Mode == PlanWriterMode.Compare)
            {
                M01OwnedCr3CompositeTransitionSemanticRecord observed = Semantics[SemanticCount];
                if (!SemanticEquals(record, observed))
                {
                    Fail(SemanticCount, PackSemantic(record), PackSemantic(observed), semantic: true);
                    return;
                }
            }

            SemanticCount++;
            SemanticOpen = false;
        }

        private static bool SemanticEquals(M01OwnedCr3CompositeTransitionSemanticRecord expected, M01OwnedCr3CompositeTransitionSemanticRecord observed)
        {
            return observed.ReservedFieldIsZero && expected.Operation == observed.Operation && expected.RecoveryClass == observed.RecoveryClass && expected.ByteOffset == observed.ByteOffset && expected.ByteLength == observed.ByteLength && expected.BranchTargetOffset == observed.BranchTargetOffset && expected.StateOffsetOrConstant == observed.StateOffsetOrConstant;
        }

        private static ulong PackSemantic(M01OwnedCr3CompositeTransitionSemanticRecord record)
        {
            return (ulong)record.ByteOffset | ((ulong)record.ByteLength << 32);
        }

        private void Fail(ulong invalidIndex, ulong expected, ulong observed, bool semantic)
        {
            if (!Succeeded)
            {
                return;
            }

            Succeeded = false;
            SemanticMismatch = semantic;
            InvalidIndex = invalidIndex;
            ExpectedValue = expected;
            ObservedValue = observed;
        }
    }
}
