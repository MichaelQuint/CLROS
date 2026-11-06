using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.BoundedExceptionFrameSearchStatus;

internal static unsafe partial class BoundedExceptionFrameSearchTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly ExceptionTypeMatchTests.Fixture Owner = new(PrepareImage);
        private readonly byte* snapshot = (byte*)NativeMemory.AllocZeroed(0x4000);
        internal Fixture()
        {
            Need(snapshot != null, "snapshot allocation");
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000);
            Need(bound.Succeeded, "same-image binding " + bound.Status);
        }

        private static void PrepareImage(nint address)
        {
            byte* image = (byte*)address;
            W32(image + 0x188 + 3 * 40 + 8, 0x2000); // Four separate root/funclet contributions and a no-EH helper.
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
            for (int i = 1; i < 5; i++)
                Owner.Image[0x1904 + (frame * 5 + i) * 0x20] = 1;
            foreach (var row in rows)
                if (row.Kind == 2)
                    Owner.Image[0x1904 + (frame * 5 + row.Extra / 0x100) * 0x20] = 2;
            byte[] blob = NativeAotEhClauseMetadataTests.Table(rows);
            uint table = 0x1C00U + (uint)frame * 0x100;
            new Span<byte>(Owner.Image + table, 0x100).Clear();
            blob.CopyTo(new Span<byte>(Owner.Image + table, blob.Length));
            var result = new NativeAotEhClauseResult[rows.Length];
            for (uint i = 0; i < result.Length; i++)
            {
                result[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000, new(0x6000U + (uint)frame * 0x500, 0x500, table, (uint)blob.Length), i);
                Need(result[i].Succeeded, "original decoded row " + i + ": " + result[i].Status);
            }

            return result;
        }

        internal ExceptionTypeMatchResult[] Matches(NativeAotEhClauseResult[] rows)
        {
            var matches = new ExceptionTypeMatchResult[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].Kind == NativeAotEhClauseKind.Typed)
                    matches[i] = Owner.Match(rows[i].TypeAddress);
            return matches;
        }

        internal BoundedExceptionSearchFrame Frame(int frame, uint offset, uint count, uint pc = 25, ulong rsp = 0)
        {
            uint method = count == 0 ? 0x7F00U : 0x6000U + (uint)frame * 0x500;
            X64UnwindContext context = new()
            {
                Rip = Owner.Address((int)method) + pc + 1,
                Rsp = rsp == 0 ? 0x100000U + (uint)frame * 0x100 : rsp,
                Rbx = Owner.Receipt.ObjectReference,
                Rbp = 0x200000U + (uint)frame * 0x100,
                R12 = 42,
                Xmm6 = new()
                {
                    Low = 0x1234,
                    High = 0x5678
                }
            };
            return new(context, pc, count == 0 ? 0x80U : 0x500U, offset, count);
        }

        public void Dispose()
        {
            NativeMemory.Free(snapshot);
            Owner.Dispose();
        }
    }
}
