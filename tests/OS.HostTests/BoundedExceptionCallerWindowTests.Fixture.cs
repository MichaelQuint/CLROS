using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.BoundedExceptionCallerWindow;
using Status = OS.Kernel.Runtime.BoundedExceptionCallerWindowStatus;

internal static unsafe partial class BoundedExceptionCallerWindowTests
{
    internal sealed class Fixture : IDisposable
    {
        internal const int ImageBytes = 0x8000;
        internal const ulong Capacity = 0x4000, StackBytes = 0x800;
        internal readonly byte* Image = (byte*)NativeMemory.AlignedAlloc(ImageBytes, 4096);
        private readonly byte* ownerBacking = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        private readonly byte* snapshotBacking = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        private readonly byte* stackBacking = (byte*)NativeMemory.AllocZeroed((nuint)StackBytes + 16);
        internal byte* Owner => ownerBacking + 8;
        internal byte* Snapshot => snapshotBacking + 8;
        internal byte* Stack => stackBacking + 8;

        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal readonly X64UnwindContext Initial;
        internal Fixture(bool unlistedEh = false)
        {
            Need(Image != null && ownerBacking != null && snapshotBacking != null && stackBacking != null, "fixture storage");
            NativeMemory.Clear(Image, ImageBytes);
            W64(ownerBacking, 0x123456789abcdef0);
            W64(Owner + Capacity, 0xfedcba9876543210);
            W64(snapshotBacking, 0x123456789abcdef0);
            W64(Snapshot + Capacity, 0xfedcba9876543210);
            W64(stackBacking, 0x123456789abcdef0);
            W64(Stack + StackBytes, 0xfedcba9876543210);
            // Independent mapped PE, ReadyToRun module and five original runtime-function rows.
            W16(Image, 0x5a4d);
            W32(Image + 60, 0x80);
            W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664);
            W16(Image + 0x86, 4);
            W16(Image + 0x94, 0xf0);
            W16(Image + 0x98, 0x20b);
            W32(Image + 0xd0, ImageBytes);
            W32(Image + 0xd4, 0x400);
            W32(Image + 0x104, 16);
            W32(Image + 0x120, 0x1100);
            W32(Image + 0x124, 60);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xc0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            new Span<byte>(Image + 0x6000, 0x1000).Fill(0x90);
            W64(Image + 0x5000, Address(0x1000));
            W32(Image + 0x1000, 0x00525452);
            W16(Image + 0x1004, 26);
            W16(Image + 0x1006, 1);
            W16(Image + 0x100c, 1);
            Image[0x100e] = 16;
            Image[0x100f] = 1;
            W32(Image + 0x1010, 204);
            W32(Image + 0x1014, 16);
            W64(Image + 0x1018, Address(0x2000));
            Function(0, 0x6000, 0x6080, 0x1200);
            Function(1, 0x6100, 0x6140, 0x1240);
            Function(2, 0x6140, 0x6180, 0x1280);
            Function(3, 0x6200, 0x6240, 0x12c0);
            Function(4, 0x6240, 0x6280, 0x1300);
            Info(0x1200, false, 0);
            Info(0x1240, true, 0x1600);
            Info(0x12c0, true, 0x1700);
            Image[0x1280] = Image[0x1300] = 1;
            Image[0x1284] = Image[0x1304] = 1;
            // NativeAOT compressed count/start/(length<<2|kind)/handler; typed RVA is fixed-width.
            new byte[]
            {
                2,
                0,
                5,
                2,
                128
            }.CopyTo(new Span<byte>(Image + 0x1600, 5));
            new byte[]
            {
                2,
                0,
                1,
                2,
                128,
                0,
                0x18,
                0,
                0
            }.CopyTo(new Span<byte>(Image + 0x1700, 9));
            W32(Image + 0x1900, 0x43454843);
            W32(Image + 0x1904, 1);
            W32(Image + 0x1908, 2);
            W32(Image + 0x190c, 48);
            CatalogRow(0, 0x6100, 0x1600, 5);
            CatalogRow(1, 0x6200, 0x1700, 9);
            if (unlistedEh)
            {
                Image[0x1206] = 4;
                W32(Image + 0x1207, 0x1a00);
                Image[0x1a00] = 0;
            }

            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageBytes);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, Capacity);
            Need(module.Succeeded, "real module registration " + module.Status);
            Handle = module.Handle;
            var snapshot = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Capture, Mapping, Owner, Capacity, Handle, Snapshot, Capacity);
            Need(snapshot.Succeeded, "real original unwind snapshot " + snapshot.Status);
            X64UnwindContext initial = new()
            {
                Rip = Address(0x6020),
                Rsp = (ulong)Stack + 0x100
            };
            foreach (byte register in new byte[]
            {
                3,
                5,
                6,
                7,
                12,
                13,
                14,
                15
            }

            )
                initial.SetInteger(register, 0x1234567800000000UL + register);
            for (byte register = 6; register <= 15; register++)
                initial.SetVector(register, new()
                {
                    Low = 0x1122334400000000UL + register,
                    High = 0xaabbccdd00000000UL + register
                });
            Initial = initial;
            W64(Stack + 0x120, Address(0x6120));
            W64(Stack + 0x148, Address(0x6220));
            W64(Stack + 0x170, Initial.Rip);
        }

        internal ulong Address(uint rva) => (ulong)Image + rva;
        internal BoundedExceptionCallerWindowResult Build(uint count, Span<BoundedExceptionSearchFrame> frames, Span<NativeAotEhClauseResult> rows, ulong? handle = null, byte* owner = null, byte* snapshot = null, ulong stackBytes = StackBytes, NativeAotLayoutProfile profile = Profile) => Core.Build(profile, Capture, Mapping, owner == null ? Owner : owner, Capacity, handle ?? Handle, snapshot == null ? Snapshot : snapshot, Capacity, 0x1900, 48, Stack, stackBytes, Initial, count, frames, rows);
        internal byte[][] InputBytes() => [new ReadOnlySpan<byte>(Image, ImageBytes).ToArray(), new ReadOnlySpan<byte>(ownerBacking, (int)Capacity + 16).ToArray(), new ReadOnlySpan<byte>(snapshotBacking, (int)Capacity + 16).ToArray(), new ReadOnlySpan<byte>(stackBacking, (int)StackBytes + 16).ToArray()];
        internal void Unchanged(byte[][] before) => Need(new ReadOnlySpan<byte>(Image, ImageBytes).SequenceEqual(before[0]) && new ReadOnlySpan<byte>(ownerBacking, (int)Capacity + 16).SequenceEqual(before[1]) && new ReadOnlySpan<byte>(snapshotBacking, (int)Capacity + 16).SequenceEqual(before[2]) && new ReadOnlySpan<byte>(stackBacking, (int)StackBytes + 16).SequenceEqual(before[3]), "original inputs and canaries unchanged");
        private void Function(int index, uint begin, uint end, uint unwind)
        {
            byte* row = Image + 0x1100 + index * 12;
            W32(row, begin);
            W32(row + 4, end);
            W32(row + 8, unwind);
        }

        private void Info(uint rva, bool eh, uint table)
        {
            Image[rva] = eh ? (byte)9 : (byte)1;
            Image[rva + 1] = 4;
            Image[rva + 2] = 1;
            Image[rva + 4] = 4;
            Image[rva + 5] = 0x32; // UWOP_ALLOC_SMALL: 32 bytes.
            if (eh)
            {
                W32(Image + rva + 8, 0x6f00);
                Image[rva + 12] = 4;
                W32(Image + rva + 13, table);
            }
        }

        private void CatalogRow(int index, uint root, uint table, uint bytes)
        {
            byte* row = Image + 0x1910 + index * 16;
            W32(row, root);
            W32(row + 4, 0x80);
            W32(row + 8, table);
            W32(row + 12, bytes);
        }

        private void Section(int index, string name, uint start, uint bytes, uint flags)
        {
            byte* row = Image + 0x188 + index * 40;
            for (int i = 0; i < name.Length; i++)
                row[i] = (byte)name[i];
            W32(row + 8, bytes);
            W32(row + 12, start);
            W32(row + 36, flags);
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(Image);
            NativeMemory.Free(ownerBacking);
            NativeMemory.Free(snapshotBacking);
            NativeMemory.Free(stackBacking);
        }
    }
}
