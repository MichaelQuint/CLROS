using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.NativeAotEhClauseMetadata;
using Status = OS.Kernel.Runtime.NativeAotEhClauseStatus;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

internal static unsafe partial class NativeAotEhClauseMetadataTests
{
    private sealed class Fixture : IDisposable
    {
        internal const int ImageBytes = 0x8000;
        internal const ulong Capacity = 0x4000;
        internal readonly byte* Image;
        private readonly byte* ownerAllocation;
        private readonly byte* snapshotAllocation;
        internal byte* Owner => ownerAllocation + 8;
        internal byte* Snapshot => snapshotAllocation + 8;
        internal EfiLoadedImageRangeCapturePlan Capture { get; }
        internal M01LoadedImageVirtualMappingEvidence Mapping { get; }

        internal ulong Handle;
        internal NativeAotEhMethodBounds Bounds;
        internal Fixture(bool associated = false)
        {
            Image = (byte*)NativeMemory.AlignedAlloc(ImageBytes, 4096);
            ownerAllocation = (byte*)NativeMemory.AllocZeroed((nuint)(Capacity + 16));
            snapshotAllocation = (byte*)NativeMemory.AllocZeroed((nuint)(Capacity + 16));
            if (Image == null || ownerAllocation == null || snapshotAllocation == null)
                throw new OutOfMemoryException();
            NativeMemory.Clear(Image, ImageBytes);
            *(ulong*)ownerAllocation = *(ulong*)snapshotAllocation = 0x0123456789ABCDEF;
            *(ulong*)(Owner + Capacity) = *(ulong*)(Snapshot + Capacity) = 0xFEDCBA9876543210;
            W16(Image, 0x5A4D);
            W32(Image + 60, 0x80);
            W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664);
            W16(Image + 0x86, 4);
            W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B);
            W32(Image + 0xD0, ImageBytes);
            W32(Image + 0xD4, 0x400);
            W32(Image + 0x104, 16);
            W32(Image + 0x120, 0x1100);
            W32(Image + 0x124, 36);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000));
            W32(Image + 0x1000, 0x00525452);
            W16(Image + 0x1004, 26);
            W16(Image + 0x1006, 1);
            W16(Image + 0x100C, 1);
            Image[0x100E] = 16;
            Image[0x100F] = 1;
            W32(Image + 0x1010, 204);
            W32(Image + 0x1014, 16);
            W64(Image + 0x1018, Address(0x2000));
            Function(0, 0x6000, 0x1200);
            Function(1, 0x6100, 0x1240);
            Function(2, 0x6200, 0x1280);
            Image[0x1200] = 9;
            W32(Image + 0x1204, 0x6500);
            Image[0x1208] = (byte)(associated ? 20 : 4);
            if (associated)
                W32(Image + 0x1209, 0x1800);
            W32(Image + (associated ? 0x120D : 0x1209), 0x1600);
            Image[0x1240] = Image[0x1280] = 1;
            Image[0x1244] = 1;
            Image[0x1284] = 2;
            Blob(Table(new Row(0, 4, 1, 0x100)));
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageBytes);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }

        internal ulong Address(uint rva) => (ulong)Image + rva;
        internal void Blob(byte[] bytes)
        {
            new Span<byte>(Image + 0x1600, 0x100).Clear();
            bytes.CopyTo(new Span<byte>(Image + 0x1600, bytes.Length));
            Bounds = new(0x6000, 0x400, 0x1600, (uint)bytes.Length);
        }

        internal void Bind()
        {
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, Capacity);
            Check(module.Succeeded, "module registration " + module.Status);
            Handle = module.Handle;
            var snapshot = Unwind.Bind(Capture, Mapping, Owner, Capacity, Handle, Snapshot, Capacity);
            Check(snapshot.Succeeded, "unwind snapshot " + snapshot.Status);
        }

        internal NativeAotEhClauseResult Read(uint index = 0) => Core.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, Capture, Mapping, Owner, Capacity, Handle, Snapshot, Capacity, Bounds, index);
        private void Function(uint index, uint begin, uint unwind)
        {
            byte* p = Image + 0x1100 + index * 12;
            W32(p, begin);
            W32(p + 4, begin + 0x80);
            W32(p + 8, unwind);
        }

        private void Section(int i, string name, uint start, uint size, uint flags)
        {
            byte* p = Image + 0x188 + i * 40;
            for (int n = 0; n < name.Length; n++)
                p[n] = (byte)name[n];
            W32(p + 8, size);
            W32(p + 12, start);
            W32(p + 36, flags);
        }

        internal void Guards() => Check(*(ulong*)ownerAllocation == 0x0123456789ABCDEF && *(ulong*)snapshotAllocation == 0x0123456789ABCDEF && *(ulong*)(Owner + Capacity) == 0xFEDCBA9876543210 && *(ulong*)(Snapshot + Capacity) == 0xFEDCBA9876543210, "caller canaries");
        public void Dispose()
        {
            NativeMemory.AlignedFree(Image);
            NativeMemory.Free(ownerAllocation);
            NativeMemory.Free(snapshotAllocation);
        }
    }
}
