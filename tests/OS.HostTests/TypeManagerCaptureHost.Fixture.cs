using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Tooling;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;
using Probe = OS.Runtime.NativeAotModuleRegistrationProbe;
using Status = OS.Kernel.Runtime.TypeManagerModuleStatus;

internal static unsafe partial class TypeManagerCaptureHost
{
    private sealed class Fixture : IDisposable
    {
        internal const int ImageSize = 0x8000;
        internal const ulong Capacity = 4096;
        internal readonly byte* Image;
        internal byte* Storage => Image + 0x3000;
        internal EfiLoadedImageRangeCapturePlan Capture { get; }
        internal M01LoadedImageVirtualMappingEvidence Mapping { get; }

        internal Fixture()
        {
            Image = (byte*)NativeMemory.AlignedAlloc(ImageSize, 4096);
            if (Image == null)
                throw new OutOfMemoryException();
            NativeMemory.Clear(Image, ImageSize);
            W16(Image, 0x5A4D);
            W32(Image + 60, 0x80);
            W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664);
            W16(Image + 0x86, 4);
            W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B);
            W32(Image + 0xD0, ImageSize);
            W32(Image + 0xD4, 0x400);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000));
            W32(Image + 0x1000, 0x00525452);
            W16(Image + 0x1004, 26);
            W16(Image + 0x1006, 1);
            W16(Image + 0x100C, 5);
            Image[0x100E] = 16;
            Image[0x100F] = 1;
            Row(0, 202, 8, 0x2100);
            Row(1, 204, 16, 0x2000);
            Row(2, 206, 8, 0x1100);
            Row(3, 213, 0, 0x1110);
            Row(4, 301, 24, 0x1200);
            Capture = TypeManagerCaptureHost.Capture((ulong)Image, ImageSize);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }

        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal byte* Row(int i) => Image + 0x1010 + 16 * i;
        internal byte* Section(int i) => Image + 0x188 + 40 * i;
        private void Row(int i, uint id, uint length, int start)
        {
            byte* r = Row(i);
            W32(r, id);
            W32(r + 4, length);
            W64(r + 8, Address(start));
        }

        private void Section(int i, string name, uint start, uint bytes, uint flags)
        {
            byte* s = Section(i);
            for (int n = 0; n < name.Length; n++)
                s[n] = (byte)name[n];
            W32(s + 8, bytes);
            W32(s + 12, start);
            W32(s + 36, flags);
        }

        internal TypeManagerModuleResult Register() => Core.RegisterOnce(Capture, Mapping, Storage, Capacity);
        internal TypeManagerModuleResult Lookup(ulong handle, int id) => Core.TryGetSection(Capture, Mapping, Storage, Capacity, handle, id);
        public void Dispose()
        {
            NativeMemory.AlignedFree(Image);
        }
    }
}
