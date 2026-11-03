using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;
using Status = OS.Kernel.Runtime.X64UnwindMetadataStatus;

internal static unsafe partial class X64PERuntimeFunctionUnwindMetadataHostileInputTests
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
        internal Fixture()
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
            W32(Image + 0x124, 24);
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
            Row(0, 0x6000, 0x6080, 0x1200);
            Row(1, 0x6100, 0x6180, 0x1240);
            Unwind(0x1200, 1, 5, 0, [5, 0x32, 1, 0x50]);
            Unwind(0x1240, 1, 0, 0, []);
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageBytes);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }

        internal ulong Address(uint rva) => (ulong)Image + rva;
        internal void Register()
        {
            if (Handle != 0)
                return;
            var result = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, Capacity);
            Check(result.Succeeded, "original module registration " + result.Status);
            Handle = result.Handle;
        }

        internal X64UnwindMetadataResult Bind()
        {
            Register();
            return Core.Bind(Capture, Mapping, Owner, Capacity, Handle, Snapshot, Capacity);
        }

        internal X64UnwindMetadataResult Lookup(uint pc) => Core.Lookup(Snapshot, Capacity, Address(pc));
        internal X64UnwindMetadataResult LookupFunction(uint index) => Core.LookupFunction(Snapshot, Capacity, index);
        internal void Row(uint i, uint begin, uint end, uint unwind)
        {
            byte* p = Image + 0x1100 + i * 12;
            W32(p, begin);
            W32(p + 4, end);
            W32(p + 8, unwind);
        }

        internal void Unwind(int rva, byte header, byte prolog, byte frame, byte[] codes)
        {
            new Span<byte>(Image + rva, 16).Clear();
            Image[rva] = header;
            Image[rva + 1] = prolog;
            Image[rva + 2] = (byte)(codes.Length / 2);
            Image[rva + 3] = frame;
            codes.AsSpan().CopyTo(new Span<byte>(Image + rva + 4, codes.Length));
        }

        internal void Chain(int rva, uint begin, uint end, uint unwind)
        {
            Unwind(rva, 33, 0, 0, []);
            W32(Image + rva + 4, begin);
            W32(Image + rva + 8, end);
            W32(Image + rva + 12, unwind);
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
