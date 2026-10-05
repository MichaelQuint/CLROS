using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;
using Status = OS.Kernel.Runtime.TypeManagerModuleStatus;
using RangeKind = OS.Kernel.Runtime.TypeManagerRegisteredRangeKind;

internal static unsafe class TypeManagerModuleMetadataLiveNativeAOTImageTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;
        Run("m04.011 ABI and result layouts are unmanaged", Layout, ref failures);
        Run("m04.011 default and mismatched image authority reject", Authority, ref failures);
        Run("m04.011 PE headers and bounded section table reject malformed bytes", Headers, ref failures);
        Run("m04.011 PE section overlap and permissions are enforced", Sections, ref failures);
        Run("m04.011 one relocated module and virtual padding are exact", Modules, ref failures);
        Run("m04.011 RTR format and unsupported dehydration reject", Format, ref failures);
        Run("m04.011 row lengths bounds and identifiers are checked", Rows, ref failures);
        Run("m04.011 registration publishes exact ABI and index once", Registration, ref failures);
        Run("m04.011 writable slot admits sixteen bytes not lookup eight", Slot, ref failures);
        Run("m04.011 full storage capacity aliases reject before writes", Storage, ref failures);
        Run("m04.011 repeated foreign and altered owner calls reject", Owner, ref failures);
        Run("m04.011 well formed metadata replacement loses retained identity", MetadataTamper, ref failures);
        Run("m04.011 retained ABI phase snapshot and slot tampering reject", StateTamper, ref failures);
        Run("m04.011 registered ranges enforce finite permissions and unsigned bounds", RegisteredRanges, ref failures);
        Run("m04.011 mutable ranges exclude complete owner slot and metadata extents", ProtectedRanges, ref failures);
        Run("m04.011 range queries retain original registration and never write", RangeIdentity, ref failures);
        Run("m04.011 validation and lookups allocate no managed storage", Allocation, ref failures);
        Run("m04.011 actual source closure compiles without assembly references", NoStdLib, ref failures);
        if (failures != 0) throw new InvalidOperationException("M04-011 host failures=" + failures);
        Console.WriteLine("CLROS_M04_011_READY hostOnly=true guestIntegrated=false abiBytes=48 slotBytes=16 stateBytes=" + Core.StateBytes);
    }

    private static void Run(string name, Action test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);
        try { test(); Console.WriteLine("TEST_PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("TEST_FAIL " + name + " " + ex); }
    }

    private static void Check(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
    }

    private static void Layout()
    {
        Check(sizeof(NativeAotTypeManagerAbi) == 48 && Marshal.SizeOf<NativeAotTypeManagerAbi>() == 48, "ABI48");
        string[] fields = ["OsModule", "ReadyToRunHeader", "GcStaticRegion", "ThreadStaticRegion", "ClassLibraryFunctions", "ClassLibraryFunctionCount", "Padding"];
        int[] offsets = [0, 8, 16, 24, 32, 40, 44];
        for (int i = 0; i < fields.Length; i++) Check(Marshal.OffsetOf<NativeAotTypeManagerAbi>(fields[i]).ToInt32() == offsets[i], fields[i]);
        Check(!RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotTypeManagerAbi>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<TypeManagerModuleResult>(), "unmanaged");
        Check(!default(TypeManagerModuleResult).Succeeded && Core.StateBytes % 8 == 0, "default/alignment");
        Console.WriteLine("M04_011_LAYOUT abi=" + sizeof(NativeAotTypeManagerAbi) + " state=" + Core.StateBytes +
            " result=" + sizeof(TypeManagerModuleResult) + " slot=16");
    }

    private static void Authority()
    {
        using Fixture f = new();
        Check(Core.Validate(default, default).Status == Status.ImageAuthorityRejected, "default");
        Check(Core.Validate(f.Capture, default).Status == Status.ImageAuthorityRejected, "missing map");
        using Fixture other = new();
        Check(Core.Validate(f.Capture, other.Mapping).Status == Status.ImageAuthorityRejected, "foreign map");
        var huge = Capture(ulong.MaxValue - 15, 64);
        Check(Core.Validate(huge, M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(huge)).Status == Status.ImageAuthorityRejected, "wrap");
        foreach (ulong n in new ulong[] { 1, 63, 64, 0x80, 0x98, 0x187, 0x227 })
        {
            var small = Capture((ulong)f.Image, n);
            Check(!Core.Validate(small, M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(small)).Succeeded, "truncated " + n);
        }
    }

    private static void Headers()
    {
        Action<Fixture>[] bad = [
            f => W16(f.Image, 0), f => W32(f.Image + 60, 0xFFFFFFF0),
            f => W32(f.Image + 60, 16), f => W32(f.Image + 0x80, 0),
            f => W16(f.Image + 0x84, 0x14C), f => W16(f.Image + 0x86, 0),
            f => W16(f.Image + 0x86, 65535), f => W16(f.Image + 0x94, 111),
            f => W16(f.Image + 0x94, 65535), f => W16(f.Image + 0x98, 0x10B),
            f => W32(f.Image + 0xD0, Fixture.ImageSize - 1),
            f => W32(f.Image + 0xD4, 0x100), f => W32(f.Image + 0xD4, Fixture.ImageSize + 1)
        ];
        foreach (var change in bad) Rejected(change, Status.ImageFormatRejected);
    }

    private static void Sections()
    {
        Action<Fixture>[] bad = [
            f => W32(f.Section(0) + 12, 0x100),
            f => W32(f.Section(0) + 8, uint.MaxValue),
            f => W32(f.Section(1) + 12, 0x1800),
            f => W32(f.Section(1) + 36, 0xE0000040),
            f => W32(f.Section(3) + 12, 0x7F00)
        ];
        foreach (var change in bad) Rejected(change, Status.ImageFormatRejected);
        Rejected(f => W32(f.Section(0) + 36, 0x40), Status.MetadataRejected);
        Rejected(f => W32(f.Section(0) + 36, 0xC0000040), Status.MetadataRejected);
    }

    private static void Modules()
    {
        Action<Fixture>[] bad = [
            f => f.Section(2)[0] = (byte)'x',
            f => W32(f.Section(2) + 8, 7), f => W32(f.Section(2) + 8, 15),
            f => W32(f.Section(2) + 12, 0x5001),
            f => W32(f.Section(2) + 36, 0x20000040),
            f => W64(f.Image + 0x5008, f.Address(0x1000))
        ];
        foreach (var change in bad) Rejected(change, Status.ModuleListRejected);
        Rejected(f => W64(f.Image + 0x5000, 0), Status.MetadataRejected);
        Rejected(f => W64(f.Image + 0x5000, ulong.MaxValue - 7), Status.MetadataRejected);
        Rejected(f => W64(f.Image + 0x5000, f.Address(0x1001)), Status.MetadataRejected);
        using Fixture f = new();
        // Raw-file padding outside .modules virtual size must never be scanned.
        W64(f.Image + 0x5010, ulong.MaxValue);
        Check(f.Register().Succeeded, "ignore raw alignment padding");
    }

    private static void Format()
    {
        Action<Fixture>[] bad = [
            f => W32(f.Image + 0x1000, 0), f => W16(f.Image + 0x1004, 15),
            f => W16(f.Image + 0x1006, 1), f => W32(f.Image + 0x1008, 1),
            f => f.Image[0x100E] = 16, f => f.Image[0x100F] = 0,
            f => W32(f.Row(3), 207)
        ];
        foreach (var change in bad) Rejected(change, Status.UnsupportedFormat);
        Rejected(f => W16(f.Image + 0x100C, 0), Status.MetadataRejected);
        Rejected(f => W16(f.Image + 0x100C, 65535), Status.MetadataRejected);
    }

    private static void Rows()
    {
        Action<Fixture>[] bad = [
            f => W32(f.Row(0), 0), f => W32(f.Row(1), 202),
            f => W32(f.Row(1), uint.MaxValue), f => W32(f.Row(2) + 4, 2),
            f => W64(f.Row(1) + 16, 1),
            f => W64(f.Row(2) + 16, f.Address(0x10FF)),
            f => W64(f.Row(2) + 16, f.Address(0x1100) + 0x80000000),
            f => W64(f.Row(2) + 8, f.Address(0x7000)),
            f => W64(f.Row(2) + 16, f.Address(0x2001)),
            f => W64(f.Row(2) + 8, ulong.MaxValue - 7),
            f => { W64(f.Row(2) + 8, f.Address(0x2008)); W64(f.Row(2) + 16, f.Address(0x2010)); }
        ];
        foreach (var change in bad) Rejected(change, Status.MetadataRejected);
        using Fixture aliases = new();
        W64(aliases.Row(4) + 8, aliases.Address(0x1100));
        W64(aliases.Row(4) + 16, aliases.Address(0x1118));
        Check(aliases.Register().Succeeded, "legitimate readonly payload overlap");
    }

    private static void Registration()
    {
        using Fixture f = new();
        byte[] imageBefore = Bytes(f.Image, Fixture.ImageSize);
        var valid = Core.Validate(f.Capture, f.Mapping);
        Check(valid.Succeeded && valid.Handle == 0, "validation is not registration");
        new Span<byte>(f.Storage + valid.RequiredStorage, (int)(Fixture.Capacity - valid.RequiredStorage)).Fill(0xA5);
        var result = f.Register();
        Check(result.Succeeded && result.RequiredStorage == valid.RequiredStorage, "register");
        Check(result.Handle == (ulong)f.Storage + Core.StateBytes, "separate ABI pointer");
        var abi = *(NativeAotTypeManagerAbi*)result.Handle;
        Check(abi.OsModule == (ulong)f.Image && abi.ReadyToRunHeader == f.Address(0x1000) &&
            abi.GcStaticRegion == 0 && abi.ThreadStaticRegion == f.Address(0x2100) &&
            abi.ClassLibraryFunctions == 0 && abi.ClassLibraryFunctionCount == 0 && abi.Padding == 0, "ABI fields");
        Check(*(ulong*)(f.Image + 0x2000) == result.Handle && *(int*)(f.Image + 0x2008) == 0 &&
            *(uint*)(f.Image + 0x200C) == 0, "handle index padding");
        var slot = f.Lookup(result.Handle, 204);
        var empty = f.Lookup(result.Handle, 213);
        var absent = f.Lookup(result.Handle, 201);
        var payload = f.Lookup(result.Handle, 301);
        Check(slot.Succeeded && slot.Length == 8 && slot.Section == f.Address(0x2000), "no end row");
        Check(empty.Succeeded && empty.Length == 0 && empty.Section == f.Address(0x1110), "present empty");
        Check(absent.Status == Status.SectionAbsent && absent.Section == 0 && absent.Length == 0, "absent");
        Check(payload.Succeeded && payload.Length == 24 && payload.Section == f.Address(0x1200), "range row");
        for (int i = 0; i < Fixture.ImageSize; i++)
            if (i < 0x2000 || i >= 0x2010) Check(f.Image[i] == imageBefore[i], "unrelated image byte " + i);
        for (ulong i = valid.RequiredStorage; i < Fixture.Capacity; i++) Check(f.Storage[i] == 0xA5, "storage tail");
        f.Guards();
        // Dedicated retained state can be in the real image's writable NX area.
        using Fixture inside = new();
        Check(Core.RegisterOnce(inside.Capture, inside.Mapping, inside.Image + 0x2800, 0x1000).Succeeded, "static in-image storage");
    }

    private static void Slot()
    {
        Rejected(f => W64(f.Row(1) + 8, f.Address(0x3FF8)), Status.MetadataRejected);
        Rejected(f => W64(f.Row(1) + 8, f.Address(0x2001)), Status.MetadataRejected);
        Rejected(f => W64(f.Row(1) + 8, f.Address(0x1300)), Status.MetadataRejected);
        Rejected(f => { W32(f.Row(1) + 4, 1); W64(f.Row(1) + 16, f.Address(0x2010)); }, Status.MetadataRejected);
        Rejected(f => W64(f.Image + 0x2008, 1UL << 32), Status.AlreadyRegistered);
        Rejected(f => W64(f.Image + 0x2000, 1), Status.AlreadyRegistered);
    }

    private static void Storage()
    {
        using Fixture f = new();
        ulong need = Core.Validate(f.Capture, f.Mapping).RequiredStorage;
        (ulong Address, ulong Size, Status Expected)[] bad = [
            (0, Fixture.Capacity, Status.StorageRejected),
            ((ulong)f.Storage + 1, Fixture.Capacity - 1, Status.StorageRejected),
            ((ulong)f.Storage, need - 1, Status.StorageRejected),
            (ulong.MaxValue - 7, 16, Status.StorageRejected),
            (f.Address(0), need, Status.StorageOverlap),
            (f.Address(0x1000), need, Status.StorageOverlap),
            (f.Address(0x2000), need, Status.StorageOverlap),
            (f.Address(0x2008), need, Status.StorageOverlap),
            (f.Address(0x2100), need, Status.StorageOverlap),
            (f.Address(0x2800), 0x3000, Status.StorageOverlap),
            (f.Address(0x5000), need, Status.StorageOverlap)
        ];
        foreach (var x in bad)
        {
            byte[] image = Bytes(f.Image, Fixture.ImageSize), storage = f.AllStorage();
            Check(Core.RegisterOnce(f.Capture, f.Mapping, (byte*)x.Address, x.Size).Status == x.Expected, "storage " + x);
            Check(Bytes(f.Image, Fixture.ImageSize).AsSpan().SequenceEqual(image) && f.AllStorage().AsSpan().SequenceEqual(storage), "no prewrite mutation");
        }
        Rejected(g => g.Storage[Core.StateBytes + 3] = 1, Status.AlreadyRegistered);
    }

    private static void Owner()
    {
        using Fixture f = new();
        var result = f.Register(); Check(result.Succeeded, "register");
        byte[] before = f.AllStorage(), image = Bytes(f.Image, Fixture.ImageSize);
        Check(f.Register().Status == Status.AlreadyRegistered, "repeat");
        Check(f.Lookup(result.Handle + 8, 204).Status == Status.RegistrationChanged, "foreign handle");
        Check(f.Lookup(0, 204).Status == Status.RegistrationChanged, "null handle");
        Check(Core.TryGetSection(f.Capture, f.Mapping, f.Storage, Fixture.Capacity - 8, result.Handle, 204).Status == Status.RegistrationChanged, "capacity identity");
        var otherIdentity = Capture((ulong)f.Image, Fixture.ImageSize, 0x9000);
        Check(Core.TryGetSection(otherIdentity, M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(otherIdentity),
            f.Storage, Fixture.Capacity, result.Handle, 204).Status == Status.RegistrationChanged, "original capture identity");
        Check(f.AllStorage().AsSpan().SequenceEqual(before) && Bytes(f.Image, Fixture.ImageSize).AsSpan().SequenceEqual(image), "rejections unchanged");
    }

    private static void MetadataTamper()
    {
        Action<Fixture>[] changes = [
            f => { W64(f.Row(4) + 8, f.Address(0x1240)); W64(f.Row(4) + 16, f.Address(0x1258)); },
            f => W32(f.Row(4), 302), f => f.Image[2] = 1,
            f => W32(f.Section(3) + 36, 0x60000060),
            f => { W64(f.Image + 0x5000, 0); W64(f.Image + 0x5008, f.Address(0x1000)); }
        ];
        foreach (var change in changes)
        {
            using Fixture f = new(); var r = f.Register(); Check(r.Succeeded, "initial");
            change(f); Check(Core.Validate(f.Capture, f.Mapping).Succeeded, "still structurally valid");
            byte[] before = f.AllStorage(), image = Bytes(f.Image, Fixture.ImageSize);
            Check(f.Lookup(r.Handle, 204).Status == Status.RegistrationChanged, "retained exact bytes");
            Check(f.AllStorage().AsSpan().SequenceEqual(before) && Bytes(f.Image, Fixture.ImageSize).AsSpan().SequenceEqual(image), "tamper check read only");
        }
        using Fixture opaque = new(); var ok = opaque.Register();
        opaque.Image[0x1200] = 12;
        Check(opaque.Lookup(ok.Handle, 301).Succeeded, "opaque payload is not a whole-image freeze");
    }

    private static void StateTamper()
    {
        using Fixture f = new(); var r = f.Register(); Check(r.Succeeded, "register");
        // Every byte of the ABI and mutable slot must be checked, including padding.
        for (int i = 0; i < 48; i++)
        {
            byte* p = (byte*)r.Handle + i; *p ^= 1;
            Check(f.Lookup(r.Handle, 204).Status == Status.RegistrationChanged, "ABI tamper " + i); *p ^= 1;
        }
        for (int i = 0; i < 16; i++)
        {
            f.Image[0x2000 + i] ^= 1;
            Check(f.Lookup(r.Handle, 204).Status == Status.RegistrationChanged, "slot tamper " + i); f.Image[0x2000 + i] ^= 1;
        }
        foreach (ulong offset in new ulong[] { 0, 8, 16, Core.StateBytes + 48, r.RequiredStorage - 1 })
        {
            f.Storage[offset] ^= 1;
            Check(f.Lookup(r.Handle, 204).Status == Status.RegistrationChanged, "phase/owner/snapshot tamper " + offset);
            f.Storage[offset] ^= 1;
        }
        Check(f.Lookup(r.Handle, 204).Succeeded, "restored fixture"); f.Guards();
    }

    private static void Allocation()
    {
        using Fixture f = new(); var r = f.Register();
        for (int i = 0; i < 50; i++)
        {
            _ = Core.Validate(f.Capture, f.Mapping); _ = f.Lookup(r.Handle, 204);
            _ = f.Range(r.Handle, f.Address(0x2200), 64, RangeKind.MutableData);
            _ = f.Range(r.Handle, f.Address(0x1200), 24, RangeKind.ImmutableMetadata);
            _ = f.Range(r.Handle, f.Address(0x2008), 8, RangeKind.MutableData);
        }
        long start = GC.GetAllocatedBytesForCurrentThread();
        bool passed = true;
        for (int i = 0; i < 1000; i++) passed &= Core.Validate(f.Capture, f.Mapping).Succeeded && f.Lookup(r.Handle, 204).Succeeded &&
            f.Range(r.Handle, f.Address(0x2200), 64, RangeKind.MutableData) == Status.Success &&
            f.Range(r.Handle, f.Address(0x1200), 24, RangeKind.ImmutableMetadata) == Status.Success &&
            f.Range(r.Handle, f.Address(0x2008), 8, RangeKind.MutableData) == Status.RangeProtectedOverlap;
        long finish = GC.GetAllocatedBytesForCurrentThread();
        Check(passed && start == finish, "allocation-free path");
    }

    private static void RegisteredRanges()
    {
        using Fixture f = new(); var r = f.Register(); Check(r.Succeeded, "registered range fixture");
        (int Offset, ulong Size, RangeKind Kind, Status Expected)[] cases = [
            (0x1200, 24, RangeKind.ImmutableMetadata, Status.Success),
            (0x1000, 136, RangeKind.ImmutableMetadata, Status.Success),
            (0x5000, 16, RangeKind.ImmutableMetadata, Status.Success),
            (0x1FF8, 8, RangeKind.ImmutableMetadata, Status.Success),
            (0x1FF8, 9, RangeKind.ImmutableMetadata, Status.RangePermissionRejected),
            (0x2200, 64, RangeKind.MutableData, Status.Success),
            (0x3FFF, 1, RangeKind.MutableData, Status.Success),
            (0x3FFF, 2, RangeKind.MutableData, Status.RangePermissionRejected),
            (0x2200, 8, RangeKind.ImmutableMetadata, Status.RangePermissionRejected),
            (0x1300, 8, RangeKind.MutableData, Status.RangePermissionRejected),
            (0x6000, 8, RangeKind.ImmutableMetadata, Status.RangePermissionRejected),
            (0x6000, 8, RangeKind.MutableData, Status.RangePermissionRejected),
            (0x4000, 8, RangeKind.MutableData, Status.RangePermissionRejected),
            (0x7000, 8, RangeKind.ImmutableMetadata, Status.RangePermissionRejected),
            (0x7FFF, 2, RangeKind.ImmutableMetadata, Status.RangeRejected),
            (0x8000, 1, RangeKind.MutableData, Status.RangeRejected)
        ];
        foreach (var c in cases) ExpectRange(f, r.Handle, f.Address(c.Offset), c.Size, c.Kind, c.Expected);
        foreach ((ulong address, ulong size) in new (ulong, ulong)[] {
            (0, 8), (f.Address(0x2200), 0), (f.Address(0x2200), ulong.MaxValue),
            (ulong.MaxValue - 7, 8), (1, 8), (f.Address(0) - 1, 1) })
            ExpectRange(f, r.Handle, address, size, RangeKind.MutableData, Status.RangeRejected);
        foreach (RangeKind invalid in new[] { RangeKind.Uninitialized, (RangeKind)4, (RangeKind)255 })
            ExpectRange(f, r.Handle, f.Address(0x2200), 8, invalid, Status.UnsupportedRangeKind);
        // Registered immutable aliases are legal; changing opaque payload is not
        // a mutation of the retained PE/RTR snapshot and grants no write authority.
        f.Image[0x1200] = 0xA9;
        ExpectRange(f, r.Handle, f.Address(0x1200), 24, RangeKind.ImmutableMetadata, Status.Success);
    }

    private static void ProtectedRanges()
    {
        using Fixture f = new();
        // GCStaticRegion contains rel32 entries in RO metadata. The future cell
        // is separate writable data, not the registered row extent itself.
        W32(f.Row(0), 201); W64(f.Row(0) + 8, f.Address(0x1100)); W64(f.Row(0) + 16, f.Address(0x1104));
        W32(f.Image + 0x1100, 0x1100); // signed rel32 from 0x1100 to cell 0x2200.
        W64(f.Row(3) + 8, f.Address(0x2300)); W64(f.Row(3) + 16, f.Address(0x2300));
        W64(f.Row(4) + 8, f.Address(0x2400)); W64(f.Row(4) + 16, f.Address(0x2418));
        var r = f.Register(); Check(r.Succeeded, "separate entry/cell registration");
        ExpectRange(f, r.Handle, f.Address(0x1100), 4, RangeKind.ImmutableMetadata, Status.Success);
        ExpectRange(f, r.Handle, f.Address(0x1100), 4, RangeKind.ReadableSectionData, Status.Success);
        ExpectRange(f, r.Handle, f.Address(0x2400), 24, RangeKind.ReadableSectionData, Status.Success);
        ExpectRange(f, r.Handle, f.Address(0x2200), 8, RangeKind.ReadableSectionData, Status.RangeRejected);
        ExpectRange(f, r.Handle, f.Address(0x23FF), 2, RangeKind.ReadableSectionData, Status.RangeRejected);
        ExpectRange(f, r.Handle, f.Address(0x6000), 8, RangeKind.ReadableSectionData, Status.RangePermissionRejected);
        ExpectRange(f, r.Handle, f.Address(0x2200), 8, RangeKind.MutableData, Status.Success);
        ExpectRange(f, r.Handle, f.Address(0x22F8), 16, RangeKind.MutableData, Status.Success); // empty row excludes no byte.
        foreach ((int offset, ulong size) in new (int, ulong)[] {
            (0, 1), (0x1000, 1), (0x1087, 1), (0x1100, 4),
            (0x2000, 1), (0x2008, 1), (0x200F, 1), (0x1FFF, 2),
            (0x2400, 1), (0x2417, 1), (0x23FF, 2), (0x5000, 16) })
            ExpectRange(f, r.Handle, f.Address(offset), size, RangeKind.MutableData, Status.RangeProtectedOverlap);
        ExpectRange(f, r.Handle, f.Address(0x2010), 8, RangeKind.MutableData, Status.Success);
        ExpectRange(f, r.Handle, f.Address(0x2418), 8, RangeKind.MutableData, Status.Success);
        using Fixture inside = new(); byte* owner = inside.Image + 0x2800;
        var registered = Core.RegisterOnce(inside.Capture, inside.Mapping, owner, 0x1000);
        Check(registered.Succeeded && registered.RequiredStorage < 0x1000, "owner unused tail exists");
        byte[] before = Bytes(inside.Image, Fixture.ImageSize);
        foreach ((int offset, ulong size) in new (int, ulong)[] { (0x2800, 1), (0x37FF, 1), (0x27FF, 2), (0x37F8, 16) })
            Check(Core.TryValidateRegisteredRange(inside.Capture, inside.Mapping, owner, 0x1000,
                registered.Handle, inside.Address(offset), size, RangeKind.MutableData) == Status.RangeProtectedOverlap, "full owner capacity");
        Check(Core.TryValidateRegisteredRange(inside.Capture, inside.Mapping, owner, 0x1000,
            registered.Handle, inside.Address(0x3800), 8, RangeKind.MutableData) == Status.Success, "owner endpoint");
        Check(Bytes(inside.Image, Fixture.ImageSize).AsSpan().SequenceEqual(before), "image owner query unchanged");
    }

    private static void RangeIdentity()
    {
        using Fixture f = new(); var r = f.Register(); Check(r.Succeeded, "range identity fixture");
        ExpectRange(f, 0, f.Address(0x2200), 8, RangeKind.MutableData, Status.RegistrationChanged);
        ExpectRange(f, r.Handle + 8, f.Address(0x2200), 8, RangeKind.MutableData, Status.RegistrationChanged);
        byte[] before = f.AllStorage(), image = Bytes(f.Image, Fixture.ImageSize);
        Check(Core.TryValidateRegisteredRange(f.Capture, f.Mapping, f.Storage, Fixture.Capacity - 8,
            r.Handle, f.Address(0x2200), 8, RangeKind.MutableData) == Status.RegistrationChanged, "range capacity identity");
        Check(Core.TryValidateRegisteredRange(default, f.Mapping, f.Storage, Fixture.Capacity,
            r.Handle, f.Address(0x2200), 8, RangeKind.MutableData) == Status.ImageAuthorityRejected, "original capture required");
        Check(f.AllStorage().AsSpan().SequenceEqual(before) && Bytes(f.Image, Fixture.ImageSize).AsSpan().SequenceEqual(image), "foreign admission unchanged");
        foreach (int offset in new[] { 0x2000, 0x2008, 0x200F, 2 })
        {
            f.Image[offset] ^= 1;
            ExpectRange(f, r.Handle, f.Address(0x2200), 8, RangeKind.MutableData, Status.RegistrationChanged);
            f.Image[offset] ^= 1;
        }
        f.Storage[0] ^= 1;
        ExpectRange(f, r.Handle, f.Address(0x2200), 8, RangeKind.MutableData, Status.RegistrationChanged);
        f.Storage[0] ^= 1;
        W32(f.Section(1) + 36, 0x40000040); // Well formed PE, but original registration changed.
        ExpectRange(f, r.Handle, f.Address(0x2200), 8, RangeKind.MutableData, Status.MetadataRejected);
    }

    private static void ExpectRange(Fixture f, ulong handle, ulong address, ulong size, RangeKind kind, Status expected)
    {
        byte[] before = f.AllStorage(), image = Bytes(f.Image, Fixture.ImageSize);
        Status actual = f.Range(handle, address, size, kind);
        Check(actual == expected, "range expected=" + expected + " actual=" + actual);
        Check(f.AllStorage().AsSpan().SequenceEqual(before) && Bytes(f.Image, Fixture.ImageSize).AsSpan().SequenceEqual(image), "range check wrote bytes");
        f.Guards();
    }

    private static void Rejected(Action<Fixture> mutate, Status expected)
    {
        using Fixture f = new(); mutate(f);
        byte[] image = Bytes(f.Image, Fixture.ImageSize), storage = f.AllStorage();
        Status actual = f.Register().Status;
        Check(actual == expected, "expected=" + expected + " actual=" + actual);
        Check(Bytes(f.Image, Fixture.ImageSize).AsSpan().SequenceEqual(image) && f.AllStorage().AsSpan().SequenceEqual(storage), "rejection wrote bytes");
        f.Guards();
    }

    private static EfiLoadedImageRangeCapturePlan Capture(ulong address, ulong bytes, ulong handle = 0x1000)
    {
        var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, handle, 0x2000, address, bytes);
        return EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
    }

    private sealed class Fixture : IDisposable
    {
        internal const int ImageSize = 0x8000;
        internal const ulong Capacity = 0x4000;
        internal readonly byte* Image;
        private readonly byte* allocation;
        internal byte* Storage => allocation + 8;
        internal EfiLoadedImageRangeCapturePlan Capture { get; }
        internal M01LoadedImageVirtualMappingEvidence Mapping { get; }

        internal Fixture()
        {
            Image = (byte*)NativeMemory.AlignedAlloc(ImageSize, 4096);
            allocation = (byte*)NativeMemory.AllocZeroed((nuint)(Capacity + 16));
            if (Image == null || allocation == null) throw new OutOfMemoryException();
            NativeMemory.Clear(Image, ImageSize);
            *(ulong*)allocation = 0xBAD0BAD0BAD0BAD0; *(ulong*)(Storage + Capacity) = 0xABCD1234ABCD1234;
            W16(Image, 0x5A4D); W32(Image + 60, 0x80); W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664); W16(Image + 0x86, 4); W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B); W32(Image + 0xD0, ImageSize); W32(Image + 0xD4, 0x400);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000));
            W32(Image + 0x1000, 0x00525452); W16(Image + 0x1004, 16); W16(Image + 0x100C, 5);
            Image[0x100E] = 24; Image[0x100F] = 1;
            Row(0, 202, 1, 0x2100, 0x2108); Row(1, 204, 0, 0x2000, 0);
            Row(2, 206, 1, 0x1100, 0x1108); Row(3, 213, 1, 0x1110, 0x1110); Row(4, 301, 1, 0x1200, 0x1218);
            Capture = TypeManagerModuleMetadataLiveNativeAOTImageTests.Capture((ulong)Image, ImageSize);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }

        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal byte* Row(int i) => Image + 0x1010 + 24 * i;
        internal byte* Section(int i) => Image + 0x188 + 40 * i;
        private void Row(int i, uint id, uint flags, int start, int end)
        {
            byte* r = Row(i); W32(r, id); W32(r + 4, flags); W64(r + 8, Address(start)); W64(r + 16, end == 0 ? 0 : Address(end));
        }
        private void Section(int i, string name, uint start, uint bytes, uint flags)
        {
            byte* s = Section(i); for (int n = 0; n < name.Length; n++) s[n] = (byte)name[n];
            W32(s + 8, bytes); W32(s + 12, start); W32(s + 36, flags);
        }
        internal TypeManagerModuleResult Register() => Core.RegisterOnce(Capture, Mapping, Storage, Capacity);
        internal TypeManagerModuleResult Lookup(ulong handle, int id) => Core.TryGetSection(Capture, Mapping, Storage, Capacity, handle, id);
        internal Status Range(ulong handle, ulong address, ulong size, RangeKind kind) =>
            Core.TryValidateRegisteredRange(Capture, Mapping, Storage, Capacity, handle, address, size, kind);
        internal byte[] AllStorage() => Bytes(allocation, (int)Capacity + 16);
        internal void Guards() => Check(*(ulong*)allocation == 0xBAD0BAD0BAD0BAD0 && *(ulong*)(Storage + Capacity) == 0xABCD1234ABCD1234, "canaries");
        public void Dispose() { NativeMemory.AlignedFree(Image); NativeMemory.Free(allocation); }
    }

    private static byte[] Bytes(byte* p, int length) => new ReadOnlySpan<byte>(p, length).ToArray();
    private static void W16(byte* p, ushort v) { p[0] = (byte)v; p[1] = (byte)(v >> 8); }
    private static void W32(byte* p, uint v) { W16(p, (ushort)v); W16(p + 2, (ushort)(v >> 16)); }
    private static void W64(byte* p, ulong v) { W32(p, (uint)v); W32(p + 4, (uint)(v >> 32)); }

    private static string[] ClosureSources(string[] projectSources, string core)
    {
        List<string> sources = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string source in projectSources)
        {
            string canonical = Path.GetFullPath(source);
            if (!seen.Add(canonical)) throw new InvalidOperationException("duplicate canonical Compile path: " + canonical);
            sources.Add(canonical);
        }
        string canonicalCore = Path.GetFullPath(core);
        if (!seen.Contains(canonicalCore)) sources.Add(canonicalCore);
        return sources.ToArray();
    }

    private static void NoStdLib()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "host-tests", "m04-011-nostdlib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string osProject = Path.Combine(root, "src", "OS", "OS.csproj");
        string core = Path.Combine(root, "src", "OS.Kernel", "Runtime", "TypeManagerModuleMetadataLiveNativeAOTImage.cs");
        string other = Path.Combine(root, "src", "OS", "Runtime", "MinimalRuntime.cs");
        Check(ClosureSources(new[] { other }, core).SequenceEqual(new[] { other, core }), "closure appends missing core in order");
        Check(ClosureSources(new[] { core, other }, core).SequenceEqual(new[] { core, other }), "closure preserves present core order");
        bool duplicateRejected = false;
        try { ClosureSources(new[] { other, Path.Combine(root, "src", "OS", "Runtime", ".", "MinimalRuntime.cs") }, core); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        Check(duplicateRejected, "closure rejects canonical project duplicates");
        duplicateRejected = false;
        try { ClosureSources(new[] { core, core.ToUpperInvariant() }, core); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        Check(duplicateRejected, "closure rejects case-only core duplicates");
        string[] sources = ClosureSources(XDocument.Load(osProject).Descendants("Compile").Select(x =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(osProject)!, x.Attribute("Include")!.Value))).ToArray(), core);
        var identities = sources.Append(osProject).Select(p => new { Path = p, Bytes = new FileInfo(p).Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray();
        File.WriteAllText(Path.Combine(directory, "inputs.json"), JsonSerializer.Serialize(identities, new JsonSerializerOptions { WriteIndented = true }));
        var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net10.0"), new XElement("LangVersion", "14.0"),
                new XElement("AssemblyName", "M04_011_Closure"), new XElement("OutputType", "Library"),
                new XElement("AllowUnsafeBlocks", "true"), new XElement("EnableDefaultCompileItems", "false"),
                new XElement("ImplicitUsings", "disable"), new XElement("Nullable", "disable"),
                new XElement("NoStdLib", "true"), new XElement("NoConfig", "true"),
                new XElement("DisableImplicitFrameworkReferences", "true"), new XElement("GenerateAssemblyInfo", "false"),
                new XElement("GenerateTargetFrameworkAttribute", "false"), new XElement("ProduceReferenceAssembly", "false"),
                new XElement("EnableNETAnalyzers", "false"), new XElement("RunAnalyzersDuringBuild", "false"),
                new XElement("RuntimeMetadataVersion", "v4.0.30319"), new XElement("DefineConstants", "NOSTDLIB_EFI")),
            new XElement("ItemGroup", sources.Select(p => new XElement("Compile", new XAttribute("Include", p)))),
            new XElement("Target", new XAttribute("Name", "RemoveFrameworkReferences"),
                new XAttribute("BeforeTargets", "BeforeCompile"), new XAttribute("AfterTargets", "FindReferenceAssembliesForReferences"),
                new XElement("ItemGroup", new XElement("ReferencePathWithRefAssemblies", new XAttribute("Remove", "@(ReferencePathWithRefAssemblies)")),
                    new XElement("ReferencePath", new XAttribute("Remove", "@(ReferencePath)")))));
        string projectPath = Path.Combine(directory, "Closure.csproj");
        File.WriteAllText(projectPath, project.ToString(), new UTF8Encoding(false));
        ProcessStartInfo start = new("dotnet") { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in new[] { "build", projectPath, "--nologo", "-c", "Release", "-m:1", "--disable-build-servers",
            "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false", "-p:UseSharedCompilation=false" }) start.ArgumentList.Add(arg);
        DateTime begin = DateTime.UtcNow;
        File.WriteAllText(Path.Combine(directory, "command.json"), JsonSerializer.Serialize(new { start.FileName, Arguments = start.ArgumentList.ToArray(), start.WorkingDirectory, StartedUtc = begin }));
        using Process process = Process.Start(start)!;
        var outputTask = process.StandardOutput.ReadToEndAsync(); var errorTask = process.StandardError.ReadToEndAsync();
        bool timeout = !process.WaitForExit(120_000);
        if (timeout) { process.Kill(true); process.WaitForExit(); }
        string output = outputTask.GetAwaiter().GetResult(), error = errorTask.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, "stdout.log"), output); File.WriteAllText(Path.Combine(directory, "stderr.log"), error);
        bool unchanged = identities.All(r => new FileInfo(r.Path).Length == r.Bytes && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(r.Path))) == r.Sha256);
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { NativeExit = process.ExitCode, Timeout = timeout,
            StartedUtc = begin, EndedUtc = DateTime.UtcNow, InputsUnchanged = unchanged }));
        Check(!timeout && process.ExitCode == 0 && error.Length == 0 && unchanged, "no-stdlib command " + directory);
        string dll = Path.Combine(directory, "bin", "Release", "net10.0", "M04_011_Closure.dll");
        using var stream = File.OpenRead(dll); using PEReader pe = new(stream);
        Check(pe.GetMetadataReader().AssemblyReferences.Count == 0, "AssemblyRefs=0");
        MetadataReader metadata = pe.GetMetadataReader();
        var query = metadata.MethodDefinitions.Select(metadata.GetMethodDefinition).Single(m =>
            metadata.GetString(m.Name) == "TryValidateRegisteredRange");
        Check(query.RelativeVirtualAddress != 0 && pe.GetMethodBody(query.RelativeVirtualAddress).GetILBytes()!.Length > 0,
            "actual registered range body");
        Console.WriteLine("M04_011_NOSTDLIB sources=" + sources.Length + " assemblyRefs=0 artifacts=" + directory);
    }
}
