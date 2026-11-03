using System.Diagnostics;
using System.Reflection;
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
#if ACPI_BOOTSTRAP_HOST
using OS.Boot;

// The whole actual EFI source is linked below. Only private numeric methods are
// invoked on admitted host storage; EfiMain, firmware output and devices never run.
internal static unsafe class AcpiBootstrapHost
{
    private const int Capacity = 266240, SourceCapacity = 1048576;
    private static byte* s_source;
    private static AcpiBootstrapWorkspace* s_work;
    private static byte* s_owned;
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void* VirtualAlloc(void* address, nuint size, uint allocation, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(void* address, nuint size, uint kind);
    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
    private static object P(void* pointer, Type type) => Pointer.Box(pointer, type);
    private static object W => P(s_work, typeof(AcpiBootstrapWorkspace*));
    private static object O => P(s_owned, typeof(byte*));
    private static object? Call(string name, params object?[] args) => typeof(EfiEntry).GetMethod(name, PrivateStatic)!.Invoke(null, args);
    private static bool Capture(int capacity = Capacity)
    {
        object?[] args = [W, O, capacity, (ulong)s_source, 0UL];
        bool result = (bool)Call("TryCaptureAcpiBootstrap", args)!;
        if (!result)
            Console.WriteLine("ACPI_CAPTURE_REJECT status=" + args[4]);
        return result;
    }
    private static bool ReadBack(int capacity = Capacity) => (bool)Call("ReadBackAcpiBootstrap", W, O, capacity)!;
    private static void Put32(byte* p, uint value)
    {
        for (int i = 0; i < 4; i++)
            p[i] = (byte)(value >> (i * 8));
    }
    private static void Put64(byte* p, ulong value)
    {
        for (int i = 0; i < 8; i++)
            p[i] = (byte)(value >> (i * 8));
    }
    private static void Checksum(byte* p, int length, int position)
    {
        p[position] = 0;
        int sum = 0;
        for (int i = 0; i < length; i++)
            sum += p[i];
        p[position] = unchecked((byte)-sum);
    }
    private static void Sdt(byte* p, string signature, int length)
    {
        new Span<byte>(p, length).Clear();
        for (int i = 0; i < 4; i++)
            p[i] = (byte)signature[i];
        Put32(p + 4, (uint)length);
        p[8] = 1;
        for (int i = 10; i < length; i++)
            p[i] = (byte)(i * 7 + 3);
        Checksum(p, length, 9);
    }
    private static void Map(uint type = 9, ulong count = 1, ulong length = SourceCapacity)
    {
        object buffer = Activator.CreateInstance(typeof(OwnedMemoryRegionBuffer))!;
        GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var records = (BootMemoryRegionRecord*)pin.AddrOfPinnedObject();
            records[0] = new(type, (ulong)s_source, 0, length / 4096, 0, length, (ulong)s_source + length);
            if (count == 2)
                records[1] = new(type, (ulong)s_source + length, 0, 1, 0, 4096, (ulong)s_source + length + 4096);
            typeof(EfiEntry).GetField("s_ownedMemoryRegions", PrivateStatic)!.SetValue(null, buffer);
        }
        finally
        {
            pin.Free();
        }
        typeof(EfiEntry).GetField("s_memoryRegionMaterialization", PrivateStatic)!.SetValue(null,
            BootMemoryRegionMaterializationResult.Success(count, 4096, 256, SourceCapacity, 0, 0));
    }
    private static void Reset(int revision = 2, bool xsdt = true)
    {
        new Span<byte>(s_source, SourceCapacity).Clear();
        new Span<byte>(s_work, sizeof(AcpiBootstrapWorkspace)).Clear();
        new Span<byte>(s_owned, Capacity + 32).Fill(0xBC);
        Map();
        byte* root = s_source + 4096;
        byte* mcfg = s_source + 8192;
        byte* madt = s_source + 16384;
        byte* hpet = s_source + 24576;
        Sdt(mcfg, "MCFG", 60);
        new Span<byte>(mcfg + 36, 8).Clear();
        Put64(mcfg + 44, 0xE0000000);
        mcfg[54] = 0;
        mcfg[55] = 255;
        Checksum(mcfg, 60, 9);
        Sdt(madt, "APIC", 52);
        Sdt(hpet, "HPET", 56);
        int width = revision >= 2 && xsdt ? 8 : 4;
        Sdt(root, width == 8 ? "XSDT" : "RSDT", 36 + 3 * width);
        for (int i = 0; i < 3; i++)
        {
            ulong value = (ulong)(i == 0 ? madt : i == 1 ? mcfg : hpet);
            if (width == 8)
                Put64(root + 36 + i * width, value);
            else
                Put32(root + 36 + i * width, (uint)value);
        }
        Checksum(root, 36 + 3 * width, 9);
        byte[] signature = "RSD PTR "u8.ToArray();
        signature.AsSpan().CopyTo(new Span<byte>(s_source, 8));
        "CLROS "u8.CopyTo(new Span<byte>(s_source + 9, 6));
        s_source[15] = (byte)revision;
        Put32(s_source + 16, (uint)(ulong)root);
        if (revision >= 2)
        {
            Put32(s_source + 20, 48);
            Put64(s_source + 24, xsdt ? (ulong)root : 0);
        }
        Checksum(s_source, 20, 8);
        if (revision >= 2)
            Checksum(s_source, 48, 32);
    }
    private static void UnchangedDestination() => Need(new ReadOnlySpan<byte>(s_owned, Capacity + 32).IndexOfAnyExcept((byte)0xBC) == -1, "entire destination unchanged");
    private static void Reject(Action mutate)
    {
        Reset();
        mutate();
        Need(!Capture() && s_work->Phase != 3, "rejected before publication");
        UnchangedDestination();
    }
    private static byte[] Bytes(byte* p, int length) => new ReadOnlySpan<byte>(p, length).ToArray();
    private static void Parity(int revision, bool xsdt)
    {
        Reset(revision, xsdt);
        int rootLength = revision >= 2 && xsdt ? 60 : 48;
        byte[] rsdp = Bytes(s_source, revision >= 2 ? 48 : 20), root = Bytes(s_source + 4096, rootLength);
        AcpiTableSource[] sources = [new((ulong)(s_source + 8192), Bytes(s_source + 8192, 60)),
            new((ulong)(s_source + 24576), Bytes(s_source + 24576, 56)), new((ulong)(s_source + 16384), Bytes(s_source + 16384, 52))];
        byte[] expected = new byte[Capacity];
        var oracle = RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap.Copy(rsdp, (ulong)s_source, root,
            (ulong)(s_source + 4096), new ulong[256], sources, expected);
        Need(oracle.Succeeded && Capture() && s_work->Phase == 3 && ReadBack(), "actual capture and independent host oracle");
        Need(s_work->TotalBytes == oracle.RequiredBytes && new ReadOnlySpan<byte>(s_owned + 16, oracle.CopiedBytes).SequenceEqual(expected.AsSpan(0, oracle.CopiedBytes)), "all five full bytes");
        AcpiOwnedTableDescriptor[] descriptors = [oracle.Rsdp, oracle.Root, oracle.Mcfg, oracle.Madt, oracle.Hpet];
        for (int i = 0; i < 5; i++)
        {
            var d = descriptors[i];
            ulong* row = s_work->Descriptors + i * 6;
            Need(row[0] == d.PhysicalAddress && row[1] == d.Signature && row[2] == (ulong)d.Length &&
                row[3] == unchecked((ulong)d.RootPointerIndex) && row[4] == (ulong)d.Offset && row[5] == (ulong)d.Length, "descriptor parity " + i);
        }
        byte[] retained = Bytes(s_owned, Capacity + 32);
        new Span<byte>(s_source, SourceCapacity).Fill(0xEE);
        Need(ReadBack() && new ReadOnlySpan<byte>(s_owned, Capacity + 32).SequenceEqual(retained), "all sources mutated; owned bytes retained");
        Need(!Capture() && s_work->Phase == 3 && new ReadOnlySpan<byte>(s_owned, Capacity + 32).SequenceEqual(retained), "one shot preserves original publication");
    }
    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m03.acpi.host " + name);
        action();
        Console.WriteLine("TEST_PASS m03.acpi.host " + name);
    }
    private static void Stage()
    {
        Need((bool)Call("TryBeginAcpiBootstrap", W, O, Capacity)!, "actual begin");
        s_work->SourceRsdp = (ulong)s_source;
        object?[] rsdp = [W, O, Capacity, (ulong)s_source, 0UL];
        Need((bool)Call("TryReadAndValidateAcpiRsdp", rsdp)!, "actual RSDP");
        object?[] args = [W, O, Capacity, 0UL];
        Need((bool)Call("TryReadAndValidateRootSdt", args)! && (bool)Call("TryDiscoverSelectedAcpiTables", args)!, "actual root/children");
        s_work->Phase = 2; // The real orchestration's one scalar handoff, to exercise prewrite negatives.
    }
    private static int Main()
    {
        for (uint hint = 0x30000000; hint < 0x60000000 && s_source == null; hint += 0x01000000)
            s_source = (byte*)VirtualAlloc((void*)(nuint)hint, SourceCapacity, 0x3000, 4);
        Need(s_source != null && (ulong)s_source + SourceCapacity < uint.MaxValue, "readable low host allocation for real RSDT pointers");
        s_work = (AcpiBootstrapWorkspace*)NativeMemory.AlignedAlloc((nuint)sizeof(AcpiBootstrapWorkspace), 64);
        s_owned = (byte*)NativeMemory.AlignedAlloc(Capacity + 32, 64);
        try
        {
            Run("five copies ACPI1 XSDT fallback and retained parity", () =>
            {
                Parity(0, false);
                Parity(2, true);
                Parity(2, false);
                Map(10);
            });
            Run("envelope selection and missing corrupt earlier table rejection", () =>
            {
                Reject(() => s_source[0] ^= 1);
                Reject(() =>
                {
                    new Span<byte>(s_source + 9, 6).Clear();
                    Checksum(s_source, 20, 8);
                    Checksum(s_source, 48, 32);
                });
                Reject(() => s_source[8] ^= 1);
                Reject(() =>
                {
                    s_source[15] = 1;
                    Checksum(s_source, 20, 8);
                });
                Reject(() => Put32(s_source + 20, 4097));
                Reject(() => s_source[32] ^= 1);
                Reject(() =>
                {
                    s_source[4096] ^= 1;
                    Checksum(s_source + 4096, 60, 9);
                });
                Reject(() => Put32(s_source + 4100, 65537));
                Reject(() => s_source[4105] ^= 1);
                Reject(() =>
                {
                    Put64(s_source + 4132, 0);
                    Checksum(s_source + 4096, 60, 9);
                });
                Reject(() =>
                {
                    Put64(s_source + 4148, (ulong)(s_source + 16384));
                    Checksum(s_source + 4096, 60, 9);
                });
                Reject(() =>
                {
                    Put64(s_source + 4148, (ulong)(s_source + 16390));
                    Checksum(s_source + 4096, 60, 9);
                });
                Reject(() => Sdt(s_source + 24576, "SSDT", 56));
                Reject(() =>
                {
                    Sdt(s_source + 16384, "SSDT", 52);
                    s_source[16393] ^= 1;
                });
                Reject(() =>
                {
                    s_source[8192 + 36] = 1;
                    Checksum(s_source + 8192, 60, 9);
                });
                Reject(() =>
                {
                    s_source[8192 + 54] = 2;
                    s_source[8192 + 55] = 1;
                    Checksum(s_source + 8192, 60, 9);
                });
                Reject(() => Map(7));
                Reject(() => Map(count: 0));
                Reject(() => Map(count: 4097));
                Reject(() => Map(length: 24576 + 55));
                Reset();
                Map(10);
                Need(Capture(), "NVS type10 permitted");
                Reset();
                Sdt(s_source + 32768, "SSDT", 40);
                Put64(s_source + 4148, (ulong)(s_source + 32768));
                Put64(s_source + 4156, (ulong)(s_source + 24576));
                Put32(s_source + 4100, 68);
                Checksum(s_source + 4096, 68, 9);
                Need(Capture(), "earlier valid full nonmatch followed by required HPET");
            });
            Run("actual final envelope helpers reject changed header fields", () =>
            {
                Reset();
                byte[] original = Bytes(s_source, 48);
                foreach (int field in new[] { 0, 9, 15, 20, 32 })
                {
                    original.AsSpan().CopyTo(new Span<byte>(s_source, 48));
                    if (field == 9)
                        new Span<byte>(s_source + 9, 6).Clear();
                    else
                        s_source[field] ^= 1;
                    if (field != 32)
                    {
                        Checksum(s_source, 20, 8);
                        Checksum(s_source, 48, 32);
                    }
                    Need(!(bool)Call("AcpiRsdpEnvelope", P(s_source, typeof(byte*)), 48, (byte)2)!, "RSDP final field " + field);
                }
                Reset();
                byte* root = s_source + 4096;
                byte[] before = Bytes(root, 60);
                foreach (int field in new[] { 0, 4, 9 })
                {
                    before.AsSpan().CopyTo(new Span<byte>(root, 60));
                    root[field] ^= 1;
                    if (field != 9)
                        Checksum(root, 60, 9);
                    Need(!(bool)Call("AcpiSdtEnvelope", P(root, typeof(byte*)), 60, 0x54445358U)!, "SDT final field " + field);
                }
                Need(!(bool)Call("AcpiSdtEnvelope", P(root, typeof(byte*)), 35, 0x54445358U)!, "short envelope before field reads");
            });
            Run("full capacity aliases range guards and cross-region denial", () =>
            {
                Reset();
                ulong w = (ulong)s_work, o = (ulong)s_owned;
                foreach (ulong[] pair in new ulong[][] { [0, o], [ulong.MaxValue - 100, o], [w + 1, o], [w, w], [w, w + (ulong)sizeof(AcpiBootstrapWorkspace) - 1], [w, ulong.MaxValue - 31] })
                    Need(!(bool)Call("AcpiBootstrapStorage", pair[0], pair[1], Capacity)!, "numeric storage admission");
                foreach (int capacity in new[] { -1, 0, Capacity + 1 })
                    Need(!(bool)Call("AcpiBootstrapStorage", w, o, capacity)!, "capacity");
                foreach (ulong address in new[] { w, w + (ulong)sizeof(AcpiBootstrapWorkspace) - 1, o, o + 16, o + Capacity + 31, ulong.MaxValue - 10 })
                    Need(!(bool)Call("AcpiSourceAdmission", W, O, Capacity, address, 36)!, "source overlaps full capacities");
                Need(!(bool)Call("AcpiOverlap", w, 16UL, w + 16, 16UL)!, "touching boundaries");
                object header = P(s_work->Header, typeof(byte*));
                Need((bool)Call("TryReadAcpiPhysicalBytes", W, O, Capacity, (ulong)s_source + SourceCapacity - 36, 36, header, 36)!, "exact range end");
                Need(!(bool)Call("TryReadAcpiPhysicalBytes", W, O, Capacity, (ulong)s_source + SourceCapacity - 35, 36, header, 36)!, "one byte outside");
                Need(!(bool)Call("TryReadAcpiPhysicalBytes", W, O, Capacity, (ulong)s_source, 36, P(s_owned, typeof(byte*)), 36)!, "not a finite read target");
                Map(count: 2, length: 4096);
                Need(!(bool)Call("TryReadAcpiPhysicalBytes", W, O, Capacity, (ulong)s_source + 4080, 36, header, 36)!, "contiguous but cross-region denied");
            });
            Run("prewrite metadata rejection exact capacity and full readback", () =>
            {
                Reset();
                Need(Capture(), "measure bounded payload");
                int total = (int)s_work->TotalBytes;
                Reset();
                Need(!Capture(total - 1), "short destination");
                UnchangedDestination();
                Reset();
                Need(Capture(total) && ReadBack(total), "exact capacity");
                Need(new ReadOnlySpan<byte>(s_owned + total + 32, Capacity - total).IndexOfAnyExcept((byte)0xBC) == -1, "beyond declared capacity unchanged");
                foreach (int slot in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 18, 19, 20, 21, 24, 25, 26, 27 })
                {
                    Reset();
                    Stage();
                    s_work->Descriptors[slot] ^= 1;
                    object?[] args = [W, O, Capacity, 0UL];
                    Need(!(bool)Call("TryPublishAcpiBootstrap", args)!, "forged descriptor " + slot);
                    UnchangedDestination();
                }
                foreach (int index in Enumerable.Range(0, 16).Concat(Enumerable.Range(Capacity + 16, 16)).Concat(new[] { 16, 16 + total / 2, 15 + total, 16 + total, Capacity + 15 }))
                {
                    Reset();
                    Need(Capture(), "capture before corruption");
                    s_owned[index] ^= 1;
                    Need(!ReadBack(), "guard/payload/tail mismatch " + index);
                }
                Reset();
                Need(Capture(), "snapshot corruption baseline");
                s_work->Madt[40] ^= 1;
                Need(!ReadBack(), "snapshot versus retained comparison");
                Need(sizeof(AcpiBootstrapWorkspace) == 335696 && sizeof(AcpiBootstrapOwnedBuffer) == 266272, "actual whole struct layout");
                Console.WriteLine("M03_ACPI_POD workspace=" + sizeof(AcpiBootstrapWorkspace) + " owned=" + sizeof(AcpiBootstrapOwnedBuffer) + " payloadOffset=16 capacity=266240 descriptorSlots=30");
            });
            Console.WriteLine("TEST_PASS m03.acpi.host.tests");
            return 0;
        }
        finally
        {
            NativeMemory.AlignedFree(s_work);
            NativeMemory.AlignedFree(s_owned);
            Need(VirtualFree(s_source, 0, 0x8000), "release host source mapping");
        }
    }
}
#else
#endif
