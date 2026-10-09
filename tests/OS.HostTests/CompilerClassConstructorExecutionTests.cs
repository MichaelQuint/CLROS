using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.CompilerClassConstructorExecution;
using Status = OS.Kernel.Runtime.CompilerClassConstructorStatus;
using Module = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;

internal static unsafe class CompilerClassConstructorExecutionTests
{

    private static void Check(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
    }

    private static void Expect(Status actual, Status expected) => Check(actual == expected, "expected=" + expected + " actual=" + actual);

    [HostTest("m04.024 caller-owned bounds and prebind calls reject", HostLane.Fast, 0)]
    private static void Prebind()
    {
        Check(Core.RequiredStorage(0, 0) == 0 && Core.RequiredStorage(65, 1) == 0 &&
            Core.RequiredStorage(1, 65) == 0, "bounded capacities");
        Check(Core.RequiredStorage(2, 3) == Core.StateBytes + 2 * 16 + 3 * 24 &&
            Core.RequiredStorage(64, 64) <= Fixture.Capacity, "POD sizing");
        using var f = new Fixture();
        Expect(Core.RunEager(f.Runner, Fixture.Capacity), Status.NotBound);
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, f.Context), Status.NotBound);
        Expect(Core.RunEager(null, 0), Status.StorageRejected);
        Expect(Core.RunEager(f.Runner + 1, Fixture.Capacity - 1), Status.StorageRejected);
        Expect(Core.Bind(default, default, f.Owner, Fixture.OwnerCapacity, f.Handle,
            f.Runner, Fixture.Capacity, 1, 1), Status.ModuleRejected);
        Check(f.RunnerBytes().All(b => b == 0), "prebind calls wrote storage");
        f.Guards();
    }

    [HostTest("m04.024 empty and absent eager tables retain explicit once state", HostLane.Fast, 1)]
    private static void Empty()
    {
        foreach (bool absent in new[] { false, true })
        foreach (bool inline in new[] { false, true })
        {
            using var f = new Fixture(absent: absent, inline: inline);
            Expect(f.Bind(), Status.Success);
            Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, f.Context), Status.EagerNotStarted);
            Expect(Core.Observe(f.Runner, Fixture.Capacity, out var p), Status.Success);
            Check(p.EagerCount == 0 && !p.EagerFinished, "binding pretended to execute");
            Expect(Core.RunEager(f.Runner, Fixture.Capacity), Status.Success);
            Expect(Core.RunEager(f.Runner, Fixture.Capacity), Status.Success);
            Expect(Core.Observe(f.Runner, Fixture.Capacity, out p), Status.Success);
            Check(p.EagerFinished && p.EagerCompleted == 0 && p.LazyCount == 0, "empty phase completion");
            Expect(f.Bind(), Status.AlreadyBound);
            f.Guards();
        }
        // Empty-table bookkeeping is not evidence that a real cctor executed.
    }

    [HostTest("m04.024 signed eager entries snapshot both displacement directions", HostLane.Fast, 2)]
    private static void SignedEntries()
    {
        foreach (int table in new[] { 0x1800, 0xA800 })
        {
            using var f = new Fixture(eagerCount: 2, table: table);
            // Distinct table entries may share a folded callable address.
            *(int*)(f.Table + 4) = *(int*)f.Table - 4;
            Expect(f.Bind(2, 1), Status.Success);
            Expect(Core.Observe(f.Runner, Fixture.Capacity, out var p), Status.Success);
            Check(p.EagerCount == 2 && p.EagerCompleted == 0 && !p.EagerFinished, "snapshot must not invoke");
            Check((*(int*)f.Table < 0) == (table == 0xA800), "signed-rel32 fixture");
            // These are admitted synthetic PE addresses, not executable host code.
            // Deliberately corrupt the later cell so RunEager must reject before calling either.
            *(int*)(f.Table + 4) += 1;
            Expect(Core.RunEager(f.Runner, Fixture.Capacity), Status.StateChanged);
            f.Guards();
        }
    }

    [HostTest("m04.024 entire eager table rejects before binding writes", HostLane.Fast, 3)]
    private static void Malformed()
    {
        using (var f = new Fixture(eagerCount: 2))
        {
            Expect(f.Bind(1, 1), Status.CapacityExceeded);
            Check(f.RunnerBytes().All(b => b == 0), "capacity rejection wrote storage");
            *(int*)(f.Table + 4) = unchecked((int)((long)f.Context - (long)(f.Table + 4)));
            Expect(f.Bind(2, 1), Status.TargetRejected);
            Check(f.RunnerBytes().All(b => b == 0), "later invalid target wrote storage");
        }
        using (var f = new Fixture(eagerCount: 1, bytes: 3))
            Expect(f.Bind(), Status.EagerTableRejected);
        using (var f = new Fixture(eagerCount: 1, table: 0x1801))
            Expect(f.Bind(), Status.EagerTableRejected);
        using (var f = new Fixture(eagerCount: 1, textFlags: 0x40000020))
            Expect(f.Bind(), Status.TargetRejected);
        using (var f = new Fixture(eagerCount: 1))
        {
            *(int*)f.Table = int.MinValue;
            Expect(f.Bind(), Status.TargetRejected);
        }
    }

    [HostTest("m04.024 runner storage rejects protected aliases", HostLane.Fast, 4)]
    private static void Storage()
    {
        using var f = new Fixture();
        foreach (ulong address in new[] { (ulong)f.Owner, f.Address(0x1000), f.Address(0x8000), f.Address(0x2000) })
        {
            byte[] before = f.ImageBytes();
            Expect(Core.Bind(f.Capture, f.Mapping, f.Owner, Fixture.OwnerCapacity, f.Handle,
                (byte*)address, Fixture.Capacity, 1, 1), Status.StorageRejected);
            Check(before.AsSpan().SequenceEqual(f.ImageBytes()), "storage rejection modified image");
        }
        f.Runner[Fixture.Capacity / 2] = 1;
        // Required bytes, not spare capacity, form the zero-initialized state prefix.
        f.Runner[0] = 1;
        Expect(f.Bind(), Status.AlreadyBound);
        f.Guards();
    }

    [HostTest("m04.024 eager snapshot and retained owner reject stale use", HostLane.Fast, 5)]
    private static void Stale()
    {
        using var f = new Fixture(eagerCount: 1);
        Expect(f.Bind(), Status.Success);
        byte[] snapshot = f.RunnerBytes();
        NativeMemory.Copy(f.Runner, f.Image + 0x3000, (nuint)Fixture.Capacity);
        Expect(Core.RunEager(f.Image + 0x3000, Fixture.Capacity), Status.StateChanged);
        Expect(Core.RunEager(f.Runner, Fixture.Capacity - 8), Status.StateChanged);
        *(ulong*)(f.Image + 0x2000) = 0;
        Expect(Core.RunEager(f.Runner, Fixture.Capacity), Status.ModuleRejected);
        Check(snapshot.AsSpan().SequenceEqual(f.RunnerBytes()), "stale owner mutated runner");
        f.Guards();
    }

    [HostTest("m04.024 lazy contexts and targets reject before invocation", HostLane.Fast, 6)]
    private static void LazyAdmission()
    {
        using var f = new Fixture();
        Expect(f.Bind(), Status.Success);
        Expect(Core.RunEager(f.Runner, Fixture.Capacity), Status.Success);
        foreach (ulong address in new[] { 0UL, f.Context + 1, f.Address(0x1000), f.Address(0x2000),
            f.Address(0x8000), (ulong)f.Owner, (ulong)f.Runner })
            Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, address), Status.ContextRejected);
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, f.Context), Status.ContextRejected);
        *(ulong*)f.Context = f.Address(0x1800);
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, f.Context), Status.TargetRejected);
        Check(*(ulong*)f.Context == f.Address(0x1800), "rejection cleared context");
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out var p), Status.Success);
        Check(p.LazyCount == 0 && p.LazyCompleted == 0, "rejection added completion");
        f.Guards();
    }

    [HostTest("m04.024 lazy capacity rejects without clearing context", HostLane.Fast, 7)]
    private static void LazyCapacity()
    {
        using var f = new Fixture(inline: true);
        Expect(f.Bind(1, 0), Status.Success);
        Expect(Core.RunEager(f.Runner, Fixture.Capacity), Status.Success);
        *(ulong*)f.Context = f.Address(0x8000);
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, f.Context), Status.CapacityExceeded);
        Check(*(ulong*)f.Context == f.Address(0x8000), "capacity failure cleared context");
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, (ulong)f.Runner + Core.StateBytes), Status.ContextRejected);
        f.Guards();
    }

    // Ordinary retained-owner fixture: actual registration against caller-owned
    // mapped bytes. RX flags are metadata only; never call this synthetic code.
    private sealed class Fixture : IDisposable
    {
        internal const uint ImageSize = 0xC000;
        internal const ulong Capacity = 4096, OwnerCapacity = 4096;
        internal readonly byte* Image = (byte*)NativeMemory.AlignedAlloc(ImageSize, 4096);
        internal readonly byte* Owner = (byte*)NativeMemory.AllocZeroed((nuint)OwnerCapacity);
        private readonly byte* allocation = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        internal readonly byte* Runner;
        internal readonly byte* Table;
        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal ulong Context => Address(0x2100);

        internal Fixture(int eagerCount = 0, int table = 0x1800, int bytes = -1,
            bool absent = false, bool inline = false, uint textFlags = 0x60000020)
        {
            Check(Image != null && Owner != null && allocation != null, "fixture allocation");
            NativeMemory.Clear(Image, ImageSize);
            Runner = inline ? Image + 0x3000 : allocation + 8; Table = Image + table;
            *(ulong*)(Runner - 8) = 0x0123456789ABCDEF; *(ulong*)(Runner + Capacity) = 0xFEDCBA9876543210;
            *(ushort*)Image = 0x5A4D; *(uint*)(Image + 60) = 0x80; *(uint*)(Image + 0x80) = 0x4550;
            *(ushort*)(Image + 0x84) = 0x8664; *(ushort*)(Image + 0x86) = 5; *(ushort*)(Image + 0x94) = 0xF0;
            *(ushort*)(Image + 0x98) = 0x20B; *(uint*)(Image + 0xD0) = ImageSize; *(uint*)(Image + 0xD4) = 0x400;
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x4000, 0xC0000040);
            Section(2, ".modules", 0x7000, 16, 0x40000040);
            Section(3, ".text", 0x8000, 0x1000, textFlags);
            Section(4, ".rdata", 0xA000, 0x1000, 0x40000040);
            *(ulong*)(Image + 0x7000) = Address(0x1000);
            *(uint*)(Image + 0x1000) = 0x00525452; *(ushort*)(Image + 0x1004) = 16;
            *(ushort*)(Image + 0x100C) = (ushort)(absent ? 1 : 2); Image[0x100E] = 24; Image[0x100F] = 1;
            Row(0, 204, 0, Address(0x2000), 0);
            if (!absent) Row(1, 205, 1, (ulong)Table, (ulong)Table + (uint)(bytes < 0 ? eagerCount * 4 : bytes));
            for (int i = 0; i < eagerCount; i++) *(int*)(Table + i * 4) = 0x8000 + i * 16 - table - i * 4;
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageSize);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var registration = Module.RegisterOnce(Capture, Mapping, Owner, OwnerCapacity);
            Check(registration.Succeeded, "fixture registration " + registration.Status); Handle = registration.Handle;
        }

        internal Status Bind(uint eager = 1, uint lazy = 1) =>
            Core.Bind(Capture, Mapping, Owner, OwnerCapacity, Handle, Runner, Capacity, eager, lazy);
        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal byte[] RunnerBytes() => new ReadOnlySpan<byte>(Runner, (int)Capacity).ToArray();
        internal byte[] ImageBytes() => new ReadOnlySpan<byte>(Image, (int)ImageSize).ToArray();
        internal void Guards() => Check(*(ulong*)(Runner - 8) == 0x0123456789ABCDEF &&
            *(ulong*)(Runner + Capacity) == 0xFEDCBA9876543210, "runner canaries");
        private void Row(int i, uint id, uint flags, ulong start, ulong end)
        {
            byte* p = Image + 0x1010 + i * 24;
            *(uint*)p = id; *(uint*)(p + 4) = flags; *(ulong*)(p + 8) = start; *(ulong*)(p + 16) = end;
        }
        private void Section(int i, string name, uint start, uint bytes, uint flags)
        {
            byte* p = Image + 0x188 + i * 40;
            for (int n = 0; n < name.Length; n++) p[n] = (byte)name[n];
            *(uint*)(p + 8) = bytes; *(uint*)(p + 12) = start; *(uint*)(p + 36) = flags;
        }
        public void Dispose() { NativeMemory.AlignedFree(Image); NativeMemory.Free(Owner); NativeMemory.Free(allocation); }
    }
}
