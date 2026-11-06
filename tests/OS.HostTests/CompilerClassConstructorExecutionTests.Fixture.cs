using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.CompilerClassConstructorExecution;
using Status = OS.Kernel.Runtime.CompilerClassConstructorStatus;
using Module = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;

internal static unsafe partial class CompilerClassConstructorExecutionTests
{
    // Ordinary retained-owner fixture: actual registration against caller-owned
    // mapped bytes. RX flags are metadata only; never call this synthetic code.
    internal sealed class Fixture : IDisposable
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

        internal Fixture(int eagerCount = 0, int table = 0x1800, int bytes = -1, bool absent = false, bool inline = false, uint textFlags = 0x60000020)
        {
            Check(Image != null && Owner != null && allocation != null, "fixture allocation");
            NativeMemory.Clear(Image, ImageSize);
            Runner = inline ? Image + 0x3000 : allocation + 8;
            Table = Image + table;
            *(ulong*)(Runner - 8) = 0x0123456789ABCDEF;
            *(ulong*)(Runner + Capacity) = 0xFEDCBA9876543210;
            *(ushort*)Image = 0x5A4D;
            *(uint*)(Image + 60) = 0x80;
            *(uint*)(Image + 0x80) = 0x4550;
            *(ushort*)(Image + 0x84) = 0x8664;
            *(ushort*)(Image + 0x86) = 5;
            *(ushort*)(Image + 0x94) = 0xF0;
            *(ushort*)(Image + 0x98) = 0x20B;
            *(uint*)(Image + 0xD0) = ImageSize;
            *(uint*)(Image + 0xD4) = 0x400;
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x4000, 0xC0000040);
            Section(2, ".modules", 0x7000, 16, 0x40000040);
            Section(3, ".text", 0x8000, 0x1000, textFlags);
            Section(4, ".rdata", 0xA000, 0x1000, 0x40000040);
            *(ulong*)(Image + 0x7000) = Address(0x1000);
            *(uint*)(Image + 0x1000) = 0x00525452;
            *(ushort*)(Image + 0x1004) = 26;
            *(ushort*)(Image + 0x1006) = 1;
            *(ushort*)(Image + 0x100C) = (ushort)(absent ? 1 : 2);
            Image[0x100E] = 16;
            Image[0x100F] = 1;
            Row(0, 204, 16, Address(0x2000));
            if (!absent)
                Row(1, 205, (uint)(bytes < 0 ? eagerCount * 4 : bytes), (ulong)Table);
            for (int i = 0; i < eagerCount; i++)
                *(int*)(Table + i * 4) = 0x8000 + i * 16 - table - i * 4;
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageSize);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var registration = Module.RegisterOnce(Capture, Mapping, Owner, OwnerCapacity);
            Check(registration.Succeeded, "fixture registration " + registration.Status);
            Handle = registration.Handle;
        }

        internal Status Bind(uint eager = 1, uint lazy = 1) => Core.Bind(Capture, Mapping, Owner, OwnerCapacity, Handle, Runner, Capacity, eager, lazy);
        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal byte[] RunnerBytes() => new ReadOnlySpan<byte>(Runner, (int)Capacity).ToArray();
        internal byte[] ImageBytes() => new ReadOnlySpan<byte>(Image, (int)ImageSize).ToArray();
        internal void Guards() => Check(*(ulong*)(Runner - 8) == 0x0123456789ABCDEF && *(ulong*)(Runner + Capacity) == 0xFEDCBA9876543210, "runner canaries");
        private void Row(int i, uint id, uint length, ulong start)
        {
            byte* p = Image + 0x1010 + i * 16;
            *(uint*)p = id;
            *(uint*)(p + 4) = length;
            *(ulong*)(p + 8) = start;
        }

        private void Section(int i, string name, uint start, uint bytes, uint flags)
        {
            byte* p = Image + 0x188 + i * 40;
            for (int n = 0; n < name.Length; n++)
                p[n] = (byte)name[n];
            *(uint*)(p + 8) = bytes;
            *(uint*)(p + 12) = start;
            *(uint*)(p + 36) = flags;
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(Image);
            NativeMemory.Free(Owner);
            NativeMemory.Free(allocation);
        }
    }
}
