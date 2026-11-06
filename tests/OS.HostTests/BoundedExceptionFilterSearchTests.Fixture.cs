using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.BoundedExceptionFilterSearchStatus;

internal static unsafe partial class BoundedExceptionFilterSearchTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly ExceptionTypeMatchTests.Fixture Owner = new(PrepareImage);
        internal readonly byte* Snapshot = (byte*)NativeMemory.AllocZeroed(0x4000);
        internal Fixture()
        {
            HostCheck.True(Snapshot != null, "snapshot allocation");
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, Snapshot, 0x4000);
            HostCheck.True(bound.Succeeded, "original image unwind binding " + bound.Status);
        }

        private static void PrepareImage(nint address)
        {
            byte* image = (byte*)address;
            W32(image + 0x104, 16);
            W32(image + 0x120, 0x1800);
            W32(image + 0x124, 108);
            for (int i = 0; i < 9; i++)
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
            W32(image + 0x1909, 0x1B00);
        }

        internal NativeAotEhClauseResult[] Four() => Rows(new(20, 16, 2, 0x100, 0x200), new(20, 16, 2, 0x300, 0x400), new(20, 16, 2, 0x500, 0x600), new(20, 16, 2, 0x700, 0x800));
        internal NativeAotEhClauseResult[] Rows(params Row[] rows)
        {
            for (int i = 1; i < 9; i++)
                Owner.Image[0x1904 + i * 0x20] = 1;
            foreach (var row in rows)
                if (row.Kind == 2)
                    Owner.Image[0x1904 + (row.Extra / 0x100) * 0x20] = 2;
            byte[] blob = NativeAotEhClauseMetadataTests.Table(rows);
            new Span<byte>(Owner.Image + 0x1B00, 0x100).Clear();
            blob.CopyTo(new Span<byte>(Owner.Image + 0x1B00, blob.Length));
            var result = new NativeAotEhClauseResult[rows.Length];
            for (uint i = 0; i < result.Length; i++)
            {
                result[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, Snapshot, 0x4000, new(0x6000, 0x900, 0x1B00, (uint)blob.Length), i);
                HostCheck.True(result[i].Succeeded, "decoded physical filter fixture row " + i + ": " + result[i].Status);
            }

            return result;
        }

        public void Dispose()
        {
            NativeMemory.Free(Snapshot);
            Owner.Dispose();
        }
    }
}
