using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.X64FrameUnwind;
using Metadata = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;
using Status = OS.Kernel.Runtime.X64FrameUnwindStatus;

internal static unsafe partial class X64FrameUnwindTests
{
    private sealed class Fixture : IDisposable
    {
        internal const ulong Capacity = 4096, StackBytes = 0x90000;
        private readonly byte* all = (byte*)NativeMemory.AlignedAlloc(0xA000, 4096);
        private readonly byte* owner = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        private readonly byte* index = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc((nuint)StackBytes + 4096, 4096);
        internal byte* Image => all + 4096;
        internal byte* Owner => owner + 8;
        internal byte* Index => index + 8;
        internal byte* Stack => backing + 16;
        internal ulong StackAddress => (ulong)Stack;
        internal ulong ReturnPc => Address(0x6520);

        internal EfiLoadedImageRangeCapturePlan Capture;
        internal M01LoadedImageVirtualMappingEvidence Mapping;
        internal ulong Handle;
        internal Fixture()
        {
            Require(all != null && owner != null && index != null && backing != null, "fixture backing");
            new Span<byte>(all, 0xA000).Fill(0xA5);
            NativeMemory.Clear(Image, 0x8000);
            new Span<byte>(backing, (int)StackBytes + 4096).Fill(0xA5);
            W64(owner, 0x12345678);
            W64(index, 0x12345678);
            W64(Owner + Capacity, 0x87654321);
            W64(Index + Capacity, 0x87654321);
            W16(Image, 0x5A4D);
            W32(Image + 60, 0x80);
            W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664);
            W16(Image + 0x86, 4);
            W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B);
            W32(Image + 0xD0, 0x8000);
            W32(Image + 0xD4, 0x400);
            W32(Image + 0x104, 16);
            W32(Image + 0x120, 0x1100);
            W32(Image + 0x124, 24);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            new Span<byte>(Image + 0x6000, 0x1000).Fill(0x90);
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
            W32(Image + 0x1100, 0x6000);
            W32(Image + 0x1104, 0x6400);
            W32(Image + 0x1108, 0x1200);
            W32(Image + 0x110C, 0x6500);
            W32(Image + 0x1110, 0x6600);
            W32(Image + 0x1114, 0x1400);
            Image[0x1400] = 1;
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, 0x8000);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }

        internal ulong Address(uint rva) => (ulong)Image + rva;
        internal void Info(byte prolog, byte frame, byte[] codes, byte header = 1)
        {
            Image[0x1200] = header;
            Image[0x1201] = prolog;
            Image[0x1202] = (byte)(codes.Length / 2);
            Image[0x1203] = frame;
            codes.AsSpan().CopyTo(new Span<byte>(Image + 0x1204, codes.Length));
        }

        internal void Bind()
        {
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, Capacity);
            Require(module.Succeeded, "module bind " + module.Status);
            Handle = module.Handle;
            var bound = Metadata.Bind(Capture, Mapping, Owner, Capacity, Handle, Index, Capacity);
            Require(bound.Succeeded, "metadata bind " + bound.Status);
        }

        internal void Code(uint rva, byte[] bytes) => bytes.AsSpan().CopyTo(new Span<byte>(Image + rva, bytes.Length));
        internal void Word(ulong address, ulong value) => W64((byte*)address, value);
        internal void Vector(ulong address, X64UnwindVector value)
        {
            Word(address, value.Low);
            Word(address + 8, value.High);
        }

        internal X64UnwindContext Context()
        {
            X64UnwindContext context = new()
            {
                Rip = Address(0x6020),
                Rsp = StackAddress + 0x100
            };
            foreach (byte register in Registers)
                context.SetInteger(register, 0x10000000UL + register);
            for (byte register = 6; register <= 15; register++)
                context.SetVector(register, new() { Low = 0x20000000UL + register, High = 0x30000000UL + register });
            return context;
        }

        internal X64FrameUnwindResult Invoke(X64UnwindContext input, ulong? handle = null, ulong ownerCapacity = Capacity, ulong indexCapacity = Capacity, ulong stackBytes = StackBytes, NativeAotLayoutProfile profile = Profile) => Core.UnwindOne(profile, Capture, Mapping, Owner, ownerCapacity, handle ?? Handle, Index, indexCapacity, Stack, stackBytes, input);
        internal byte[][] Snapshot() => [Bytes(all, 0xA000), Bytes(owner, (int)Capacity + 16), Bytes(index, (int)Capacity + 16), Bytes(backing, (int)StackBytes + 4096)];
        internal void Unchanged(byte[][] before)
        {
            Require(new ReadOnlySpan<byte>(all, 0xA000).SequenceEqual(before[0]) && new ReadOnlySpan<byte>(owner, (int)Capacity + 16).SequenceEqual(before[1]) && new ReadOnlySpan<byte>(index, (int)Capacity + 16).SequenceEqual(before[2]) && new ReadOnlySpan<byte>(backing, (int)StackBytes + 4096).SequenceEqual(before[3]), "original image, owners, snapshot, backing and canaries unchanged");
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

        public void Dispose()
        {
            NativeMemory.AlignedFree(all);
            NativeMemory.Free(owner);
            NativeMemory.Free(index);
            NativeMemory.AlignedFree(backing);
        }
    }
}
