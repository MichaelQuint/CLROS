using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.X64FrameUnwind;
using Metadata = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;
using Status = OS.Kernel.Runtime.X64FrameUnwindStatus;

internal static unsafe partial class X64FrameUnwindTests
{
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
    private static readonly byte[] Registers = [3, 5, 6, 7, 12, 13, 14, 15];
    [HostTest("m04.one_frame_unwind.body", HostLane.Fast, 0)]
    private static void Body()
    {
        using (Fixture f = new())
        {
            List<byte> codes = [];
            for (int i = 7; i >= 0; i--)
            {
                codes.Add((byte)(i + 1));
                codes.Add((byte)(Registers[i] << 4));
            }

            f.Info(8, 0, codes.ToArray());
            f.Bind();
            var input = f.Context();
            var expected = input;
            for (int i = 0; i < 8; i++)
            {
                ulong value = 0xA000UL + (ulong)i;
                f.Word(input.Rsp + (ulong)i * 8, value);
                expected.SetInteger(Registers[7 - i], value);
            }

            f.Word(input.Rsp + 64, f.ReturnPc);
            expected.Rsp += 72;
            expected.Rip = f.ReturnPc;
            Success(f, input, expected, input.Rsp + 64, input.Rsp, false, "all pushed nonvolatile registers");
        }

        using (Fixture f = new())
        {
            List<byte> codes = [];
            var input = f.Context();
            var expected = input;
            for (int i = 0; i < 8; i++)
            {
                bool far = (i & 1) != 0;
                uint offset = (uint)i * 8;
                Code(codes, 30, far ? (byte)5 : (byte)4, Registers[i], offset, far ? 1U : 8U);
                ulong value = 0xAB000UL + (ulong)i;
                f.Word(input.Rsp + offset, value);
                expected.SetInteger(Registers[i], value);
            }

            for (byte r = 6; r <= 15; r++)
            {
                bool far = (r & 1) != 0;
                uint offset = 64U + (uint)(r - 6) * 16;
                Code(codes, 30, far ? (byte)9 : (byte)8, r, offset, far ? 1U : 16U);
                var value = new X64UnwindVector
                {
                    Low = 0xC000UL + r,
                    High = 0xD000UL + r
                };
                f.Vector(input.Rsp + offset, value);
                expected.SetVector(r, value);
            }

            codes.AddRange([5, 0x01, 64, 0]); // ALLOC_LARGE, scaled 512.
            f.Info(30, 0, codes.ToArray());
            f.Bind();
            f.Word(input.Rsp + 512, f.ReturnPc);
            expected.Rsp += 520;
            expected.Rip = f.ReturnPc;
            Success(f, input, expected, input.Rsp + 512, input.Rsp, false, "near/far integer and full XMM saves");
        }

        foreach (var item in new (byte[] Codes, ulong Bytes)[]
        {
            ([5, 0xF2], 128),
            ([5, 0x01, 17, 0], 136),
            ([5, 0x11, 0, 0, 8, 0], 524288)
        }

        )
        {
            using Fixture f = new();
            f.Info(5, 0, item.Codes);
            f.Bind();
            var input = f.Context();
            f.Word(input.Rsp + item.Bytes, f.ReturnPc);
            var expected = input;
            expected.Rsp += item.Bytes + 8;
            expected.Rip = f.ReturnPc;
            Success(f, input, expected, input.Rsp + item.Bytes, input.Rsp, false, "allocation encoding boundary");
        }

        using (Fixture f = new())
        {
            // SAVE FP restores a deliberately different caller value before
            // SET_FPREG is undone. All saves and SET use the ORIGINAL frame base.
            f.Info(16, 0x25, [16, 0x54, 0, 0, 15, 0xC4, 2, 0, 14, 0x68, 2, 0, 8, 3, 4, 0x72]);
            f.Bind();
            var input = f.Context();
            ulong frame = input.Rsp + 64;
            input.Rbp = frame + 32;
            f.Word(frame, 0xCA11E2);
            f.Word(frame + 16, 0xAABBCC);
            X64UnwindVector vector = new()
            {
                Low = 0x12345678,
                High = 0xFEDCBA98
            };
            f.Vector(frame + 32, vector);
            f.Word(frame + 64, f.ReturnPc);
            var expected = input;
            expected.Rbp = 0xCA11E2;
            expected.R12 = 0xAABBCC;
            expected.Xmm6 = vector;
            expected.Rsp = frame + 72;
            expected.Rip = f.ReturnPc;
            Success(f, input, expected, frame + 64, frame, false, "dynamic RSP and stable original frame base");
        }

        using (Fixture f = new())
        {
            f.Info(0, 0, [], header: 9);
            W32(f.Image + 0x1204, 0x6500);
            f.Bind();
            var input = f.Context();
            f.Word(input.Rsp, f.ReturnPc);
            var expected = input;
            expected.Rsp += 8;
            expected.Rip = f.ReturnPc;
            Success(f, input, expected, input.Rsp, input.Rsp, false, "handler metadata is not invoked");
        }
    }

    [HostTest("m04.one_frame_unwind.prolog", HostLane.Fast, 1)]
    private static void Prolog()
    {
        using Fixture f = new();
        f.Info(12, 0x25, [12, 0xC4, 2, 0, 8, 3, 5, 0x01, 17, 0, 1, 0x50]);
        f.Bind();
        ulong entrySp = f.StackAddress + 0x500, frame = entrySp - 8 - 136;
        f.Word(entrySp, f.ReturnPc);
        f.Word(entrySp - 8, 0x112233);
        f.Word(frame + 16, 0x556677);
        foreach (uint offset in new uint[]
        {
            0,
            1,
            4,
            5,
            7,
            8,
            11,
            12,
            13
        }

        )
        {
            var input = f.Context();
            input.Rip = f.Address(0x6000 + offset);
            input.Rsp = offset < 1 ? entrySp : offset < 5 ? entrySp - 8 : frame;
            input.Rbp = offset < 8 ? ulong.MaxValue : frame + 32;
            var expected = input;
            expected.Rsp = entrySp + 8;
            expected.Rip = f.ReturnPc;
            if (offset >= 1)
                expected.Rbp = 0x112233;
            if (offset >= 12)
                expected.R12 = 0x556677;
            Success(f, input, expected, entrySp, offset < 8 ? input.Rsp : frame, false, "partial prolog offset " + offset);
        }

        // At entry only the return slot is readable; skipped multi-slot saves
        // and allocations must not use operands as operations or read the stack.
        var atEntry = f.Context();
        atEntry.Rip = f.Address(0x6000);
        atEntry.Rsp = f.StackAddress + Fixture.StackBytes - 8;
        atEntry.Rbp = 0;
        f.Word(atEntry.Rsp, f.ReturnPc);
        var caller = atEntry;
        caller.Rsp += 8;
        caller.Rip = f.ReturnPc;
        Success(f, atEntry, caller, atEntry.Rsp, atEntry.Rsp, false, "entry read at exact stack end");
    }

    [HostTest("m04.one_frame_unwind.epilog", HostLane.Fast, 2)]
    private static void Epilog()
    {
        using (Fixture f = new())
        {
            f.Info(6, 0, [6, 0x32, 2, 0xC0, 1, 0x50]);
            f.Bind();
            f.Code(0x6060, [0x48, 0x83, 0xC4, 32, 0x41, 0x5C, 0x5D, 0xC3]);
            ulong sp = f.StackAddress + 0x100;
            f.Word(sp + 32, 0x1212);
            f.Word(sp + 40, 0xBABA);
            f.Word(sp + 48, f.ReturnPc);
            foreach (var part in new (uint Offset, ulong Sp)[]
            {
                (0, sp),
                (4, sp + 32),
                (6, sp + 40),
                (7, sp + 48)
            }

            )
            {
                var input = f.Context();
                input.Rip = f.Address(0x6060 + part.Offset);
                input.Rsp = part.Sp;
                if (part.Offset >= 6)
                    input.R12 = 0x1212;
                if (part.Offset >= 7)
                    input.Rbp = 0xBABA;
                var expected = input;
                expected.R12 = 0x1212;
                expected.Rbp = 0xBABA;
                expected.Rsp = sp + 56;
                expected.Rip = f.ReturnPc;
                Success(f, input, expected, sp + 48, 0, true, "every ADD/POP/RET suffix");
            }

            f.Code(0x6080, [0x48, 0x81, 0xC4, 32, 0, 0, 0, 0x41, 0x5C, 0x5D, 0xF3, 0xC3]);
            var full = f.Context();
            full.Rip = f.Address(0x6080);
            var restored = full;
            restored.R12 = 0x1212;
            restored.Rbp = 0xBABA;
            restored.Rsp = sp + 56;
            restored.Rip = f.ReturnPc;
            Success(f, full, restored, sp + 48, 0, true, "imm32 ADD and REP RET");
            var rep = full;
            rep.Rip = f.Address(0x608A);
            rep.Rsp = sp + 48;
            rep.Rbp = restored.Rbp;
            rep.R12 = restored.R12;
            Success(f, rep, restored, sp + 48, 0, true, "remaining REP RET");
        }

        foreach (var item in new (byte Frame, byte[] Code, int Disp)[]
        {
            (0x25, [0x48, 0x8D, 0x65, 32, 0x5D, 0xC3], 32),
            (0x2D, [0x49, 0x8D, 0xA5, 32, 0, 0, 0, 0x41, 0x5D, 0xC3], 32),
            (0x25, [0x48, 0x8D, 0x65, 0xF0, 0x5D, 0xC3], -16)
        }

        )
        {
            using Fixture f = new();
            byte register = (byte)(item.Frame & 15);
            f.Info(8, item.Frame, [8, 3, 4, 0x72, 1, (byte)(register << 4)]);
            f.Bind();
            f.Code(0x6060, item.Code);
            var input = f.Context();
            input.Rip = f.Address(0x6060);
            input.SetInteger(register, input.Rsp + 128);
            ulong slot = input.Rsp + (ulong)(128 + item.Disp);
            f.Word(slot, 0xA5B6C7);
            f.Word(slot + 8, f.ReturnPc);
            var expected = input;
            expected.SetInteger(register, 0xA5B6C7);
            expected.Rsp = slot + 16;
            expected.Rip = f.ReturnPc;
            Success(f, input, expected, slot + 8, 0, true, "LEA low/high FP and signed displacement");
            // Frame register is already popped: the RET suffix must not try to
            // dereference its caller value or reapply metadata allocation.
            input = expected;
            input.Rip = f.Address(0x6060 + (uint)item.Code.Length - 1);
            input.Rsp = slot + 8;
            Success(f, input, expected, slot + 8, 0, true, "restored FP at RET suffix");
        }

        using (Fixture f = new())
        {
            f.Info(0, 0, []);
            f.Bind();
            var input = f.Context();
            f.Word(input.Rsp, f.ReturnPc);
            var expected = input;
            expected.Rsp += 8;
            expected.Rip = f.ReturnPc;
            foreach (byte[] code in new byte[][]
            {
                [0x90],
                [0x48, 0x89, 0xC0],
                [0x48, 0x8D, 0x45, 0],
                [0xFF, 0xD0],
                [0xE8, 0, 0, 0, 0]
            }

            )
            {
                f.Code(0x6020, code);
                Success(f, input, expected, input.Rsp, input.Rsp, false, "unambiguous body opcode");
            }
        }
    }

    [HostTest("m04.one_frame_unwind.provenance", HostLane.Fast, 3)]
    private static void Provenance()
    {
        using Fixture f = new();
        using Fixture other = new();
        f.Info(0, 0, []);
        other.Info(0, 0, []);
        f.Bind();
        other.Bind();
        var input = f.Context();
        f.Word(input.Rsp, f.ReturnPc);
        var expected = input;
        expected.Rip = f.ReturnPc;
        expected.Rsp += 8;
        Success(f, input, expected, input.Rsp, input.Rsp, false, "first owning image");
        var second = other.Context();
        other.Word(second.Rsp, other.ReturnPc);
        var secondExpected = second;
        secondExpected.Rip = other.ReturnPc;
        secondExpected.Rsp += 8;
        Success(other, second, secondExpected, second.Rsp, second.Rsp, false, "second owning image");
        Failure(f, () => f.Invoke(input, handle: f.Handle + 8), Status.ModuleRejected, "wrong handle");
        Failure(f, () => f.Invoke(input, ownerCapacity: Fixture.Capacity - 1), Status.ModuleRejected, "original capacity changed");
        Failure(f, () => Core.UnwindOne(Profile, other.Capture, other.Mapping, other.Owner, Fixture.Capacity, other.Handle, f.Index, Fixture.Capacity, f.Stack, Fixture.StackBytes, input), Status.ModuleRejected, "different module authority");
        Failure(f, () => f.Invoke(input, indexCapacity: Fixture.Capacity - 8), Status.MetadataRejected, "index capacity changed");
        new ReadOnlySpan<byte>(f.Index, (int)Fixture.Capacity).CopyTo(new Span<byte>(other.Index, (int)Fixture.Capacity));
        Failure(f, () => Core.UnwindOne(Profile, f.Capture, f.Mapping, f.Owner, Fixture.Capacity, f.Handle, other.Index, Fixture.Capacity, f.Stack, Fixture.StackBytes, input), Status.MetadataRejected, "copied index owner");
        f.Index[Metadata.StateBytes] ^= 1;
        Failure(f, () => f.Invoke(input), Status.MetadataRejected, "snapshot changed");
        f.Index[Metadata.StateBytes] ^= 1;
        f.Image[0x1201] = 1;
        Failure(f, () => f.Invoke(input), Status.MetadataRejected, "well-formed metadata changed");
        f.Image[0x1201] = 0;
        f.Owner[0] ^= 1;
        Failure(f, () => f.Invoke(input), Status.MetadataRejected, "registration changed");
        f.Owner[0] ^= 1;
        foreach (ulong pc in new[]
        {
            f.Address(0x6400),
            other.Address(0x6020),
            ulong.MaxValue
        }

        )
        {
            var noRecord = input;
            noRecord.Rip = pc;
            Failure(f, () => f.Invoke(noRecord), Status.MetadataRejected, "no leaf inference");
        }

        f.Word(input.Rsp, other.ReturnPc);
        Failure(f, () => f.Invoke(input), Status.ReturnPcRejected, "foreign return PC");
    }

    [HostTest("m04.one_frame_unwind.hostile", HostLane.Fast, 4)]
    private static void Hostile()
    {
        using (Fixture f = new())
        {
            f.Info(0, 0, []);
            f.Bind();
            var input = f.Context();
            f.Word(input.Rsp, f.ReturnPc);
            Failure(f, () => f.Invoke(input, profile: default), Status.UnsupportedProfile, "profile required");
            foreach (byte[] code in new byte[][]
            {
                [0xE9, 0, 0, 0, 0],
                [0xEB, 0],
                [0xFF, 0x25, 0, 0, 0, 0],
                [0x48, 0xFF, 0xE0],
                [0xF2, 0xC3],
                [0xF3, 0x90],
                [0xC2, 8, 0],
                [0x48, 0xC3],
                [0x66, 0xC3],
                [0x67, 0xC3],
                [0x41, 0x48, 0xC3],
                [0x58, 0xC3],
                [0x5C, 0xC3],
                [0x5D, 0x90],
                [0x48, 0x83, 0xC4, 0xF8, 0xC3],
                [0x48, 0x83, 0xC4, 8, 0x90],
                [0x48, 0x8D, 0x64, 0x24, 8, 0xC3]
            }

            )
            {
                f.Code(0x6020, code);
                Failure(f, () => f.Invoke(input), Status.UnsupportedEpilog, "no unsupported epilog body fallback");
            }

            f.Code(0x6020, Enumerable.Repeat((byte)0x5D, 16).Append((byte)0xC3).ToArray());
            Failure(f, () => f.Invoke(input), Status.UnsupportedEpilog, "instruction budget");
            f.Code(0x6020, Enumerable.Range(0, 16).SelectMany(_ => new byte[] { 0x41, 0x5C }).Append((byte)0xC3).ToArray());
            Failure(f, () => f.Invoke(input), Status.UnsupportedEpilog, "byte/instruction budget");
            foreach (byte[] code in new byte[][]
            {
                [0x48],
                [0x48, 0x83, 0xC4],
                [0x5D],
                [0xF3]
            }

            )
            {
                uint pc = 0x6400 - (uint)code.Length;
                f.Code(pc, code);
                var truncated = input;
                truncated.Rip = f.Address(pc);
                Failure(f, () => f.Invoke(truncated), Status.CodeRangeRejected, "function-end instruction window");
            }

            f.Code(0x6020, [0x90]);
            foreach (ulong sp in new[]
            {
                input.Rsp + 1,
                f.StackAddress - 8,
                f.StackAddress + Fixture.StackBytes,
                ulong.MaxValue - 7
            }

            )
            {
                var bad = input;
                bad.Rsp = sp;
                Failure(f, () => f.Invoke(bad), Status.StackRangeRejected, "invalid stack transition/read");
            }

            foreach (ulong pc in new[]
            {
                0UL,
                f.Address(0x1200),
                f.Address(0x2000),
                f.Address(0x8000)
            }

            )
            {
                f.Word(input.Rsp, pc);
                Failure(f, () => f.Invoke(input), Status.ReturnPcRejected, "non-executable return");
            }

            var edge = input;
            edge.Rsp = f.StackAddress + Fixture.StackBytes - 8;
            f.Word(edge.Rsp, f.ReturnPc);
            var edgeExpected = edge;
            edgeExpected.Rip = f.ReturnPc;
            edgeExpected.Rsp += 8;
            Success(f, edge, edgeExpected, edge.Rsp, edge.Rsp, false, "return slot exact end");
            Failure(f, () => f.Invoke(edge, stackBytes: Fixture.StackBytes - 1), Status.StackRangeRejected, "return slot one byte short");
            var overlap = input;
            overlap.Rsp = f.Address(0x6000);
            Failure(f, () => Core.UnwindOne(Profile, f.Capture, f.Mapping, f.Owner, Fixture.Capacity, f.Handle, f.Index, Fixture.Capacity, f.Image, 0x8000, overlap), Status.StorageOverlap, "stack cannot alias image");
            Failure(f, () => Core.UnwindOne(Profile, f.Capture, f.Mapping, f.Owner, Fixture.Capacity, f.Handle, f.Index, Fixture.Capacity, (byte*)(ulong.MaxValue - 7), 16, input), Status.StackRangeRejected, "stack extent overflow");
        }

        foreach (bool vector in new[]
        {
            false,
            true
        }

        )
        {
            using Fixture f = new();
            uint size = vector ? 16U : 8U;
            uint offset = (uint)Fixture.StackBytes - 0x100 - size;
            List<byte> codes = [];
            Code(codes, 5, vector ? (byte)9 : (byte)5, vector ? (byte)6 : (byte)3, offset, 1);
            f.Info(5, 0, codes.ToArray());
            f.Bind();
            var input = f.Context();
            f.Word(input.Rsp, f.ReturnPc);
            f.Word(f.StackAddress + Fixture.StackBytes - size, 123);
            if (vector)
                f.Word(f.StackAddress + Fixture.StackBytes - 8, 456);
            var expected = input;
            expected.Rip = f.ReturnPc;
            expected.Rsp += 8;
            if (vector)
                expected.Xmm6 = new()
                {
                    Low = 123,
                    High = 456
                };
            else
                expected.Rbx = 123;
            Success(f, input, expected, input.Rsp, input.Rsp, false, "save extent exact end");
            Failure(f, () => f.Invoke(input, stackBytes: Fixture.StackBytes - 1), Status.StackRangeRejected, "saved value one byte short");
        }

        using (Fixture f = new())
        {
            f.Info(5, 0x15, [5, 3, 1, 0x12]);
            f.Bind();
            var input = f.Context();
            input.Rbp = 8;
            Failure(f, () => f.Invoke(input), Status.ArithmeticOverflow, "FP subtraction underflow");
            input.Rbp = f.StackAddress - 8 + 16;
            Failure(f, () => f.Invoke(input), Status.StackRangeRejected, "FP outside stack");
            input.Rsp += 0x100;
            input.Rbp = f.StackAddress + 0x110;
            f.Word(f.StackAddress + 0x110, f.ReturnPc);
            Failure(f, () => f.Invoke(input), Status.FrameTransitionRejected, "nonadvancing caller RSP");
        }

        using (Fixture f = new())
        {
            f.Info(0, 0, [0, 0x0A]);
            f.Bind();
            var input = f.Context();
            Failure(f, () => f.Invoke(input), Status.UnsupportedFrame, "machine frame");
        }

        using (Fixture f = new())
        {
            f.Info(0, 0, [], header: 33);
            W32(f.Image + 0x1204, 0x6500);
            W32(f.Image + 0x1208, 0x6600);
            W32(f.Image + 0x120C, 0x1400);
            f.Bind();
            var input = f.Context();
            Failure(f, () => f.Invoke(input), Status.UnsupportedFrame, "listed chain");
        }
    }

    [HostTest("m04.one_frame_unwind.atomic_allocation", HostLane.Fast, 5)]
    private static void AtomicAllocation()
    {
        using Fixture f = new();
        f.Info(5, 0, [5, 0x68, 2, 0, 4, 0x34, 1, 0, 1, 0x72]);
        f.Bind();
        var input = f.Context();
        f.Word(input.Rsp + 8, 0x1234);
        f.Vector(input.Rsp + 32, new()
        {
            Low = 0xAAAA,
            High = 0xBBBB
        });
        f.Word(input.Rsp + 64, 0);
        Failure(f, () => f.Invoke(input), Status.ReturnPcRejected, "late failure after scalar and vector restoration");
        f.Word(input.Rsp + 64, f.ReturnPc);
        var expected = input;
        expected.Rbx = 0x1234;
        expected.Xmm6 = new()
        {
            Low = 0xAAAA,
            High = 0xBBBB
        };
        expected.Rsp += 72;
        expected.Rip = f.ReturnPc;
        Success(f, input, expected, input.Rsp + 64, input.Rsp, false, "complete result after valid return");
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<X64UnwindContext>() && !RuntimeHelpers.IsReferenceOrContainsReferences<X64FrameUnwindResult>(), "POD context and result");
        for (int i = 0; i < 64; i++)
            _ = f.Invoke(input);
        var before = f.Snapshot();
        // Match the other host allocation fixtures: exclude collection-time
        // counter accounting, while still counting every real allocation.
        Require(GC.TryStartNoGCRegion(1024 * 1024, disallowFullBlockingGC: false), "allocation measurement region admission");
        long allocated;
        int successes = 0;
        try
        {
            long positiveBefore = GC.GetAllocatedBytesForCurrentThread();
            byte[] positive = AllocationCounterPositive();
            long positiveBytes = GC.GetAllocatedBytesForCurrentThread() - positiveBefore;
            GC.KeepAlive(positive);
            Require(positiveBytes == 64, "allocation counter retains a real 37-byte array");
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                if (f.Invoke(input).Succeeded)
                    successes++;
                if (f.Invoke(input, stackBytes: 0).Succeeded)
                    successes++;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        }
        finally
        {
            GC.EndNoGCRegion();
        }
        f.Unchanged(before);
        Require(successes == 64 && allocated == 0, $"zero allocated bytes for success and failure successes={successes} allocatedBytes={allocated}");
        Console.WriteLine("CLROS_ONE_FRAME_UNWIND_HOST allocatedBytes=0 iterations=64 backingUnchanged=true liveRegisters=false");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte[] AllocationCounterPositive() => new byte[37];

    private static void Success(Fixture f, X64UnwindContext input, X64UnwindContext expected, ulong slot, ulong frame, bool epilog, string why)
    {
        var before = f.Snapshot();
        var copy = input;
        var result = f.Invoke(input);
        Require(result.Succeeded && Equal(result.Caller, expected) && Equal(input, copy) && result.ReturnSlot == slot && result.EstablisherFrame == frame && result.InEpilog == epilog && result.ImageBase == (ulong)f.Image && result.ModuleOwner == (ulong)f.Owner && result.ModuleHandle == f.Handle && result.FunctionIndex == 0, why + ": " + result.Status);
        f.Unchanged(before);
    }

    private static void Failure(Fixture f, Func<X64FrameUnwindResult> invoke, Status expected, string why)
    {
        var before = f.Snapshot();
        var result = invoke();
        Require(result.Status == expected && !result.Succeeded && Equal(result.Caller, default) && result.ImageBase == 0 && result.ModuleOwner == 0 && result.ModuleHandle == 0 && result.ReturnSlot == 0 && result.EstablisherFrame == 0 && result.FunctionIndex == 0 && !result.InEpilog, why + ": expected " + expected + ", got " + result.Status);
        f.Unchanged(before);
    }

    private static bool Equal(X64UnwindContext a, X64UnwindContext b) => new ReadOnlySpan<byte>(&a, sizeof(X64UnwindContext)).SequenceEqual(new ReadOnlySpan<byte>(&b, sizeof(X64UnwindContext)));
    private static void Code(List<byte> list, byte offset, byte opcode, byte register, uint value, uint scale)
    {
        list.Add(offset);
        list.Add((byte)(register << 4 | opcode));
        uint encoded = value / scale;
        list.Add((byte)encoded);
        list.Add((byte)(encoded >> 8));
        if (opcode == 5 || opcode == 9)
        {
            list.Add((byte)(encoded >> 16));
            list.Add((byte)(encoded >> 24));
        }
    }

    private static byte[] Bytes(byte* p, int bytes) => new ReadOnlySpan<byte>(p, bytes).ToArray();
    private static void Require(bool condition, string why)
    {
        if (!condition)
            throw new InvalidOperationException(why);
    }

    private static void W16(byte* p, ushort value)
    {
        p[0] = (byte)value;
        p[1] = (byte)(value >> 8);
    }

    private static void W32(byte* p, uint value)
    {
        W16(p, (ushort)value);
        W16(p + 2, (ushort)(value >> 16));
    }

    private static void W64(byte* p, ulong value)
    {
        W32(p, (uint)value);
        W32(p + 4, (uint)(value >> 32));
    }
}
