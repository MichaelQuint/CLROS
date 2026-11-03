using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.BoundedExceptionFrameDispatchStatus;
using SearchStatus = OS.Kernel.Runtime.BoundedExceptionFrameSearchStatus;

internal static unsafe partial class BoundedExceptionFrameDispatchTests
{
    // Independent decoded physical EH contributions, reusing the retained-object matcher fixture.
    private sealed class Fixture : IDisposable
    {
        internal readonly ExceptionTypeMatchTests.Fixture Owner = new(PrepareImage);
        private readonly byte* snapshot = (byte*)NativeMemory.AllocZeroed(0x4000);
        internal Fixture()
        {
            HostCheck.True(snapshot != null, "snapshot allocation");
            HostCheck.True(X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000).Succeeded, "real same-image unwind binding");
        }

        private static void PrepareImage(nint address)
        {
            byte* image = (byte*)address;
            W32(image + 0x188 + 3 * 40 + 8, 0x2000);
            W32(image + 0x104, 16);
            W32(image + 0x120, 0x1800);
            W32(image + 0x124, 21 * 12);
            for (int i = 0; i < 21; i++)
            {
                uint begin = i == 20 ? 0x7F00U : 0x6000U + (uint)i * 0x100, unwind = 0x1900U + (uint)i * 0x20;
                W32(image + 0x1800 + i * 12, begin);
                W32(image + 0x1804 + i * 12, begin + 0x80);
                W32(image + 0x1808 + i * 12, unwind);
                image[unwind] = (byte)(i < 20 && i % 5 == 0 ? 9 : 1);
                if (i < 20 && i % 5 == 0)
                {
                    W32(image + unwind + 4, 0x7F00);
                    image[unwind + 8] = 4;
                    W32(image + unwind + 9, 0x1C00U + (uint)(i / 5) * 0x100);
                }
                else if (i < 20)
                    image[unwind + 4] = 1;
            }
        }

        internal NativeAotEhClauseResult[] Rows(int frame, params Row[] rows)
        {
            byte[] bytes = NativeAotEhClauseMetadataTests.Table(rows);
            uint table = 0x1C00U + (uint)frame * 0x100;
            new Span<byte>(Owner.Image + table, 0x100).Clear();
            bytes.CopyTo(new Span<byte>(Owner.Image + table, bytes.Length));
            var decoded = new NativeAotEhClauseResult[rows.Length];
            for (uint i = 0; i < decoded.Length; i++)
            {
                decoded[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000, new(0x6000U + (uint)frame * 0x500, 0x500, table, (uint)bytes.Length), i);
                HostCheck.True(decoded[i].Succeeded, "real decoded physical row " + i);
            }

            return decoded;
        }

        internal ExceptionTypeMatchResult[] Matches(NativeAotEhClauseResult[] rows)
        {
            var matches = new ExceptionTypeMatchResult[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].Kind == NativeAotEhClauseKind.Typed)
                    matches[i] = Owner.Match(rows[i].TypeAddress);
            return matches;
        }

        internal BoundedExceptionSearchFrame Frame(int frame, uint offset, uint count)
        {
            uint method = count == 0 ? 0x7F00U : 0x6000U + (uint)frame * 0x500;
            ulong seed = 0xF123456789ABCD00UL + (uint)frame;
            X64UnwindContext context = new()
            {
                Rip = Owner.Address((int)method) + 26,
                Rsp = 0x100000U + (uint)frame * 0x100,
                Rbx = seed + 3,
                Rbp = seed + 5,
                Rsi = seed + 6,
                Rdi = seed + 7,
                R12 = seed + 12,
                R13 = seed + 13,
                R14 = seed + 14,
                R15 = seed + 15
            };
            for (byte register = 6; register <= 15; register++)
                context.SetVector(register, new() { Low = seed + register, High = ~seed - register });
            return new(context, 25, count == 0 ? 0x80U : 0x500U, offset, count);
        }

        public void Dispose()
        {
            NativeMemory.Free(snapshot);
            Owner.Dispose();
        }
    }
}
