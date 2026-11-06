using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.BoundedExceptionDispatchStatus;

internal static unsafe partial class BoundedExceptionDispatchPlanTests
{
    internal sealed class Fixture : IDisposable
    {
        internal readonly ExceptionTypeMatchTests.Fixture Owner = new(PrepareImage);
        private readonly byte* snapshot = (byte*)NativeMemory.AllocZeroed(0x4000);
        internal Fixture()
        {
            Need(snapshot != null, "snapshot allocation");
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000);
            Need(bound.Succeeded, "same-image unwind binding " + bound.Status);
        }

        private static void PrepareImage(nint address)
        {
            byte* image = (byte*)address;
            W32(image + 0x104, 16);
            W32(image + 0x120, 0x1800);
            W32(image + 0x124, 60);
            for (int i = 0; i < 5; i++)
            {
                uint begin = 0x6000U + (uint)i * 0x100, unwind = 0x1900U + (uint)i * 0x20;
                W32(image + 0x1800 + i * 12, begin);
                W32(image + 0x1804 + i * 12, begin + 0x80);
                W32(image + 0x1808 + i * 12, unwind);
                image[unwind] = (byte)(i == 0 ? 9 : 1);
                if (i != 0)
                    image[unwind + 4] = 1;
            }

            W32(image + 0x1904, 0x6500);
            image[0x1908] = 4;
            W32(image + 0x1909, 0x1A00);
        }

        internal NativeAotEhClauseResult[] Rows(params Row[] rows)
        {
            for (int i = 1; i < 5; i++)
                Owner.Image[0x1904 + i * 0x20] = 1;
            foreach (var row in rows)
                if (row.Kind == 2)
                    Owner.Image[0x1904 + (row.Extra / 0x100) * 0x20] = 2;
            byte[] blob = NativeAotEhClauseMetadataTests.Table(rows);
            new Span<byte>(Owner.Image + 0x1A00, 0x100).Clear();
            blob.CopyTo(new Span<byte>(Owner.Image + 0x1A00, blob.Length));
            var result = new NativeAotEhClauseResult[rows.Length];
            for (uint i = 0; i < result.Length; i++)
            {
                result[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000, new(0x6000, 0x500, 0x1A00, (uint)blob.Length), i);
                Need(result[i].Succeeded, "decoded original row " + i + ": " + result[i].Status);
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

        public void Dispose()
        {
            NativeMemory.Free(snapshot);
            Owner.Dispose();
        }
    }
}
