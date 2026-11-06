using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Alloc = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Core = OS.Kernel.Runtime.ExceptionTypeMatch;
using Status = OS.Kernel.Runtime.ExceptionTypeMatchStatus;

internal static unsafe partial class ExceptionTypeMatchTests
{
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
    private const uint ClassFlags = 0x50000000;
    [HostTest("m04.exception_type_match.identity", HostLane.Fast, 0)]
    private static void Identity()
    {
        Require(!default(ExceptionTypeMatchResult).Succeeded && !default(ExceptionTypeMatchResult).Matches, "default denied");
        // Intentionally unusable pointers prove null does not read any authority.
        var nil = Core.Match(Profile, default, default, (byte*)1, ulong.MaxValue, 1, default, (RhpNewFastBumpState*)1, ulong.MaxValue, default, 0, ulong.MaxValue);
        Require(nil.Succeeded && !nil.Matches, "null nonmatch without dereference");
        Require(Core.Match(default, default, default, null, 0, 0, default, null, 0, default, 0, 0).Status == Status.UnsupportedProfile, "profile precedes null");
        Require(Core.Match(Profile, default, default, null, 0, 0, default, null, 0, default, 1, 0).Status == Status.ObjectRejected, "invalid nonnull is not null");
        using Fixture f = new();
        Expect(f, f.Derived, Status.Success, true, "exact identity");
        Expect(f, f.Base, Status.Success, true, "immediate base");
        Expect(f, f.Root, Status.Success, true, "root ancestor");
        Expect(f, f.Sibling, Status.Success, false, "sibling shares base, size and hash");
        Expect(f, f.Other, Status.Success, false, "unrelated root shares size and hash");
        var baseObject = f.Allocate(f.Base);
        Check(f, () => f.Match(f.Derived, receipt: baseObject), Status.Success, false, "base is not derived");
        // Ordinary classes may contain references and implement interfaces. This
        // predicate does not inspect their reference maps, interface or vtable tails.
        W32((byte*)f.Derived, ClassFlags | 0x01040000);
        W16((byte*)f.Derived + 18, 1);
        Expect(f, f.Base, Status.Success, true, "reference-containing ordinary class");
    }

    [HostTest("m04.exception_type_match.object_authority", HostLane.Fast, 1)]
    private static void ObjectAuthority()
    {
        using Fixture f = new();
        using Fixture other = new();
        Check(f, () => f.Match(f.Root, reference: 1), Status.ObjectRejected, false, "invalid object address");
        Check(f, () => f.Match(f.Root, reference: f.Receipt.ObjectReference + 8), Status.ObjectRejected, false, "interior reference");
        Check(f, () => f.Match(f.Root, receipt: default(RhpNewFastResult), reference: f.Receipt.ObjectReference), Status.ObjectRejected, false, "missing receipt");
        Check(f, () => f.Match(f.Root, receipt: other.Receipt), Status.ObjectRejected, false, "wrong retained heap");
        Check(f, () => f.Match(f.Root, stateCapacity: 79), Status.ObjectRejected, false, "short owner storage");
        Check(f, () => Core.Match(Profile, f.Capture, f.Mapping, f.Owner, 4096, f.Handle, other.Access, other.State, Fixture.StateCapacity, f.Receipt, f.Receipt.ObjectReference, f.Root), Status.ObjectRejected, false, "different original controller");
        byte* copiedState = f.HeapStorage + 0x9000;
        new ReadOnlySpan<byte>(f.State, (int)Fixture.StateCapacity).CopyTo(new Span<byte>(copiedState, (int)Fixture.StateCapacity));
        Check(f, () => Core.Match(Profile, f.Capture, f.Mapping, f.Owner, 4096, f.Handle, f.Access, (RhpNewFastBumpState*)copiedState, Fixture.StateCapacity, f.Receipt, f.Receipt.ObjectReference, f.Root), Status.ObjectRejected, false, "copied controller");
        new Span<byte>(copiedState, (int)Fixture.StateCapacity).Fill(0xA5);
        byte* obj = (byte*)f.Receipt.ObjectReference;
        W64(obj, f.Base);
        Expect(f, f.Root, Status.ObjectRejected, false, "changed object MT slot");
        W64(obj, f.Derived);
        obj[-8] = 1;
        Expect(f, f.Root, Status.ObjectRejected, false, "changed syncblock");
        obj[-8] = 0;
        W32((byte*)f.Derived + 4, 32);
        Expect(f, f.Root, Status.ObjectRejected, false, "allocation and admitted MT size differ");
        W32((byte*)f.Derived + 4, 24);
        obj[8] = 0x7B;
        Expect(f, f.Root, Status.Success, true, "payload mutation is legal");
        // The collecting owner retains a live allocation ledger. Removing this
        // record models a swept receipt; it is not a test of collector execution.
        byte[] record = new ReadOnlySpan<byte>(f.State + 1, 32).ToArray();
        NativeMemory.Clear(f.State + 1, 32);
        Expect(f, f.Root, Status.ObjectRejected, false, "receipt absent from current live ledger");
        record.AsSpan().CopyTo(new Span<byte>(f.State + 1, 32));
        Expect(f, f.Root, Status.Success, true, "restored original authority");
    }

    [HostTest("m04.exception_type_match.module_authority", HostLane.Fast, 2)]
    private static void ModuleAuthority()
    {
        using Fixture f = new();
        using Fixture other = new();
        Check(f, () => f.Match(f.Root, handle: f.Handle + 8), Status.ModuleRejected, false, "wrong module handle");
        Check(f, () => f.Match(f.Root, ownerCapacity: 4095), Status.ModuleRejected, false, "changed original owner capacity");
        Check(f, () => Core.Match(Profile, f.Capture, f.Mapping, other.Owner, 4096, other.Handle, f.Access, f.State, Fixture.StateCapacity, f.Receipt, f.Receipt.ObjectReference, f.Root), Status.ModuleRejected, false, "owner registered for another image");
        Check(f, () => Core.Match(Profile, other.Capture, other.Mapping, other.Owner, 4096, other.Handle, f.Access, f.State, Fixture.StateCapacity, f.Receipt, f.Receipt.ObjectReference, other.Root), Status.MetadataRejected, false, "object MT is outside the supplied image");
        byte* copy = f.ImageStorage + 0xA000;
        new ReadOnlySpan<byte>(f.Owner, 4096).CopyTo(new Span<byte>(copy, 4096));
        Check(f, () => Core.Match(Profile, f.Capture, f.Mapping, copy, 4096, (ulong)copy + f.Handle - (ulong)f.Owner, f.Access, f.State, Fixture.StateCapacity, f.Receipt, f.Receipt.ObjectReference, f.Root), Status.ModuleRejected, false, "copied module owner");
        new Span<byte>(copy, 4096).Fill(0xA5);
        f.Image[0x1010] ^= 1;
        Expect(f, f.Root, Status.ModuleRejected, false, "stale registered RTR row");
        f.Image[0x1010] ^= 1;
        f.Image[0x2000] ^= 8;
        Expect(f, f.Root, Status.ModuleRejected, false, "changed module slot");
        f.Image[0x2000] ^= 8;
        Expect(f, f.Root, Status.Success, true, "restored registration");
    }

    [HostTest("m04.exception_type_match.metadata", HostLane.Fast, 3)]
    private static void Metadata()
    {
        using Fixture f = new();
        foreach (ulong target in new[]
        {
            0UL,
            1UL,
            ulong.MaxValue - 7,
            f.Root + 1,
            f.Address(0x1FF0),
            f.Address(0x2000),
            f.Address(0x6000),
            f.Address(0x8000)
        }

        )
            Expect(f, target, Status.MetadataRejected, false, "invalid or incomplete immutable target");
        using Fixture other = new();
        Expect(f, other.Root, Status.MetadataRejected, false, "foreign target identity");
        f.Type(0x1FE8, 0);
        Expect(f, f.Address(0x1FE8), Status.Success, false, "exact 24-byte section-end prefix");
        foreach (ulong ancestor in new[]
        {
            1UL,
            ulong.MaxValue - 7,
            f.Address(0x1FF0),
            f.Address(0x2000),
            other.Root
        }

        )
        {
            W64((byte*)f.Base + 8, ancestor);
            Expect(f, f.Other, Status.MetadataRejected, false, "invalid ancestor is not a nonmatch");
            Expect(f, f.Derived, Status.MetadataRejected, false, "exact identity cannot hide invalid ancestor");
        }

        W64((byte*)f.Base + 8, f.Root);
        W64((byte*)f.Other + 8, f.Address(0x2000));
        Expect(f, f.Other, Status.MetadataRejected, false, "target hierarchy also requires authority");
        W64((byte*)f.Other + 8, 0);
        foreach (uint size in new uint[]
        {
            0,
            16,
            25
        }

        )
        {
            W32((byte*)f.Other + 4, size);
            Expect(f, f.Other, Status.MetadataRejected, false, "invalid class size");
        }

        W32((byte*)f.Other + 4, 24);
        W32((byte*)f.Other, ClassFlags | 0x00200000);
        Expect(f, f.Other, Status.MetadataRejected, false, "reserved type flag");
        // An import cell contains a pointer, not a MethodTable prefix; no second
        // dereference may turn the following cell into a supported type identity.
        f.Type(0x1600, 0, flags: 0);
        W64(f.Image + 0x1600 + 8, f.Root);
        W64((byte*)f.Base + 8, f.Address(0x1600));
        Expect(f, f.Root, Status.UnsupportedType, false, "undeclared indirection is not followed");
    }

    [HostTest("m04.exception_type_match.unsupported", HostLane.Fast, 4)]
    private static void Unsupported()
    {
        using Fixture f = new();
        foreach (uint flags in new uint[]
        {
            0x54000000, // interface
            0x40000000, // value type
            0x48000000, // nullable
            0xE0020008, // SZ array
            0xDC020008, // multidimensional array
            0xD0000002, // string, with its required base size below
            ClassFlags | 0x02000000, // constructed generic
            ClassFlags | 0x00030000, // generic definition
            ClassFlags | 0x00080000, // dynamic
            ClassFlags | 0x00100000, // finalizer
            ClassFlags | 0x00800000, // generic variance
            ClassFlags | 0x00000008, // dynamic interface casting
            0x6C010000 // function pointer
        }

        )
        {
            uint size = flags == 0xD0000002 ? 22U : 24U;
            f.Type(0x1500, 0, flags, size);
            Expect(f, f.Other, Status.UnsupportedType, false, "unsupported target shape " + flags.ToString("X8"));
            f.Type(0x1400, 0, flags, size);
            Expect(f, f.Derived, Status.UnsupportedType, false, "unsupported ancestor shape " + flags.ToString("X8"));
            f.Type(0x1400, 0);
        }

        Check(f, () => f.Match(f.Root, profile: default(NativeAotLayoutProfile)), Status.UnsupportedProfile, false, "unknown profile");
    }

    [HostTest("m04.exception_type_match.hierarchy_bounds", HostLane.Fast, 5)]
    private static void HierarchyBounds()
    {
        using Fixture f = new();
        W64((byte*)f.Base + 8, f.Base);
        Expect(f, f.Other, Status.HierarchyCycle, false, "self cycle");
        W64((byte*)f.Base + 8, f.Root);
        W64((byte*)f.Root + 8, f.Derived);
        Expect(f, f.Other, Status.HierarchyCycle, false, "long cycle");
        Expect(f, f.Derived, Status.HierarchyCycle, false, "target identity before cycle");
        W64((byte*)f.Root + 8, 0);
        for (int i = 0; i < Core.MaximumHierarchyDepth + 1; i++)
            f.Type(0x1600 + i * 24, i == Core.MaximumHierarchyDepth ? 0 : f.Address(0x1600 + (i + 1) * 24));
        ulong head = f.Address(0x1600);
        W64(f.Image + 0x1600 + (Core.MaximumHierarchyDepth - 1) * 24 + 8, 0);
        var receipt = f.Allocate(head);
        Check(f, () => f.Match(f.Other, receipt: receipt), Status.Success, false, "exact depth boundary admitted");
        Check(f, () => f.Match(head, receipt: receipt), Status.Success, true, "exact depth target admitted");
        W64(f.Image + 0x1600 + (Core.MaximumHierarchyDepth - 1) * 24 + 8, f.Address(0x1600 + Core.MaximumHierarchyDepth * 24));
        Check(f, () => f.Match(f.Other, receipt: receipt), Status.DepthExceeded, false, "overlong object chain");
        Expect(f, head, Status.DepthExceeded, false, "overlong target chain");
    }

    [HostTest("m04.exception_type_match.zero_allocation", HostLane.Fast, 6)]
    private static void ZeroAllocation()
    {
        using Fixture f = new();
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<ExceptionTypeMatchResult>(), "POD result");
        for (int i = 0; i < 128; i++)
            _ = f.Match(f.Root);
        byte[] image = f.ImageSnapshot(), heap = f.HeapSnapshot();
        long before = GC.GetAllocatedBytesForCurrentThread();
        int matches = 0;
        for (int i = 0; i < 128; i++)
        {
            if (f.Match(f.Root).Matches)
                matches++;
            if (f.Match(f.Other).Matches)
                matches++;
            if (f.Match(1).Succeeded)
                matches++;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(allocated == 0 && matches == 128, "allocation-free match, nonmatch and rejection");
        f.Unchanged(image, heap);
    }

    private static void Expect(Fixture f, ulong target, Status status, bool matches, string why) => Check(f, () => f.Match(target), status, matches, why);
    private static void Check(Fixture f, Func<ExceptionTypeMatchResult> invoke, Status status, bool matches, string why)
    {
        byte[] image = f.ImageSnapshot(), heap = f.HeapSnapshot();
        ExceptionTypeMatchResult result = invoke();
        Require(result.Status == status && result.Succeeded == (status == Status.Success) && result.Matches == matches, why + ": expected " + status + "/" + matches + ", got " + result.Status + "/" + result.Matches);
        f.Unchanged(image, heap);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void W16(byte* p, ushort value)
    {
        for (int i = 0; i < 2; i++)
            p[i] = (byte)(value >> (i * 8));
    }

    private static void W32(byte* p, uint value)
    {
        for (int i = 0; i < 4; i++)
            p[i] = (byte)(value >> (i * 8));
    }

    private static void W64(byte* p, ulong value)
    {
        for (int i = 0; i < 8; i++)
            p[i] = (byte)(value >> (i * 8));
    }
}
