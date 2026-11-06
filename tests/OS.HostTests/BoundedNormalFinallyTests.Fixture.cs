using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using OS.Tooling;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.NativeAotNormalFinallyStatus;
using PlanStatus = OS.Kernel.Runtime.BoundedExceptionDispatchStatus;
using PhysicalFixture = BoundedExceptionCallerWindowTests.Fixture;
using PlanFixture = BoundedExceptionDispatchPlanTests.Fixture;

internal static unsafe partial class BoundedNormalFinallyTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly PhysicalFixture Original = new();
        internal readonly X64UnwindContext Current, Expected;
        internal Fixture()
        {
            // Move the later root's unwind record, leaving room for the finally's own saved GP/XMM codes.
            new ReadOnlySpan<byte>(Original.Image + 0x12c0, 32).CopyTo(new Span<byte>(Original.Image + 0x13c0, 32));
            W32(Original.Image + 0x112c, 0x13c0);
            new Span<byte>(Original.Image + 0x1280, 68).Clear();
            byte* info = Original.Image + 0x1280;
            info[0] = 1;
            info[1] = 8;
            info[2] = 30;
            int slot = 0;
            for (byte register = 6; register <= 15; register++)
            {
                byte* code = info + 4 + slot * 2;
                code[0] = 8;
                code[1] = (byte)(register << 4 | 8);
                code[2] = (byte)(register - 6);
                slot += 2;
            }

            byte* allocation = info + 4 + slot * 2;
            allocation[0] = 8;
            allocation[1] = 1;
            allocation[2] = 20;
            slot += 2;
            byte[] registers = [3, 5, 6, 7, 12, 13, 14, 15];
            foreach (byte register in registers)
            {
                info[4 + slot * 2] = 8;
                info[5 + slot * 2] = (byte)(register << 4);
                slot++;
            }

            info[64] = 1;
            var current = Original.Initial;
            current.Rip = Original.Address(0x6150);
            current.Rsp = (ulong)Original.Stack + 0x108;
            var expected = Original.Initial;
            expected.Rip = Original.Address(0x6120);
            expected.Rsp = current.Rsp + 232;
            for (int i = 0; i < registers.Length; i++)
            {
                W64((byte*)current.Rsp + 160 + i * 8, expected.GetInteger(registers[i]));
                current.SetInteger(registers[i], 0);
            }

            for (byte register = 6; register <= 15; register++)
            {
                var vector = expected.GetVector(register);
                byte* saved = (byte*)current.Rsp + (register - 6) * 16;
                W64(saved, vector.Low);
                W64(saved + 8, vector.High);
                current.SetVector(register, default);
            }

            W64((byte*)current.Rsp + 224, expected.Rip);
            Current = current;
            Expected = expected;
            Rebind();
        }

        internal void Rebind()
        {
            NativeMemory.Clear(Original.Snapshot, (nuint)PhysicalFixture.Capacity);
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Original.Capture, Original.Mapping, Original.Owner, PhysicalFixture.Capacity, Original.Handle, Original.Snapshot, PhysicalFixture.Capacity);
            Need(bound.Succeeded, "normal-finally original snapshot " + bound.Status);
        }

        internal void Table(params Row[] rows)
        {
            byte[] blob = NativeAotEhClauseMetadataTests.Table(rows);
            new Span<byte>(Original.Image + 0x1600, 0x100).Clear();
            blob.CopyTo(new Span<byte>(Original.Image + 0x1600, blob.Length));
            W32(Original.Image + 0x191c, (uint)blob.Length);
        }

        internal NativeAotNormalFinallyResult Recover(X64UnwindContext? context = null, ulong? handle = null, byte* owner = null, byte* snapshot = null, ulong stackBytes = PhysicalFixture.StackBytes, NativeAotLayoutProfile profile = Profile) => NativeAotEhClauseMetadata.RecoverNormalFinallyParent(profile, Original.Capture, Original.Mapping, owner == null ? Original.Owner : owner, PhysicalFixture.Capacity, handle ?? Original.Handle, snapshot == null ? Original.Snapshot : snapshot, PhysicalFixture.Capacity, 0x1900, 48, Original.Stack, stackBytes, context ?? Current);
        public void Dispose() => Original.Dispose();
    }
}
