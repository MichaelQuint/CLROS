using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
#if ARRAY_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

// Actual adapter POD core only: these are owned buffers, never CLR object addresses.
internal static unsafe partial class ArrayCoreTestProgram
{
    private static int Main()
    {
        Run("mixed exact headers and scalar storage", Mixed);
        Run("authentic token and bounded prefix rejection", Rejected);
        Run("actual before-bind and mixed exhaustion rejection", Exhaustion);
        Run("scratch extent and zero managed allocation", Geometry);
        Console.WriteLine("TEST_PASS array.core.tests");
        return 0;
    }
    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START array.core " + name);
        try
        {
            test();
            Console.WriteLine("TEST_PASS array.core " + name);
        }
        catch (Exception e) { Console.WriteLine("TEST_FAIL array.core " + name + " " + e); throw; }
    }
    private static void Need(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
    private static void W32(byte* p, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(p, 4), value);
    private static void W64(byte* p, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(p, 8), value);
    private static int Length(ulong id) => id == 3 ? 3 : id == 5 ? 1 : 0;
    private static ulong Size(ulong id) => id == 3 ? 48UL : id == 4 || id == 5 ? 32UL : 24UL;
    private static ulong Scalar(ulong id) => id == 1 ? 0x1111111111111111UL : id == 3 ? 0x3333333333333333UL : id == 4 ? 0x2222222222222222UL : id == 5 ? 0x5AUL : 0;
    private static ulong Offset(ulong id) => id == 1 ? 8UL : id == 3 ? 24UL : id == 4 || id == 5 ? 16UL : 0;
    private static void Sequence(F f, bool oracle)
    {
        ulong used = 0;
        for (ulong id = 1; id <= 5; id++)
        {
            ulong size = Size(id);
            int length = Length(id);
            bool array = id == 2 || id == 3 || id == 5;
            new Span<byte>(f.Page + used, (int)size).Fill(0xBC);
            byte[] before = oracle ? f.Snapshot() : [];
            var result = f.Mixed(id, length);
            Need(result.Succeeded && result.Leaf.UsedBefore == used && result.Leaf.UsedAfter == used + size &&
                result.Leaf.AllocationCount == id && result.Observation.Reference == (ulong)f.Page + used + 8 &&
                result.Observation.ZeroBytes == size - (array ? 12UL : 8UL) && result.Observation.ZeroOr == 0, "mixed result id=" + id);
            if (oracle)
            {
                byte[] expected = new byte[size];
                BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8), f.MixedToken(id));
                if (array)
                    BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16), length);
                Need(new ReadOnlySpan<byte>(f.Page + used, (int)size).SequenceEqual(expected), "all independent header/padding/payload bytes");
                f.Only(before, (0x3100, 80), (0x3200, 96), (0x4000, 6672), (0x8000 + (int)used, (int)size));
                if (array)
                    Need(result.Observation.ElementTable == f.Token(id == 3 ? 7UL : 6UL) &&
                    result.Observation.ElementPrefix0 == ((24UL << 32) | (id == 3 ? 0x28000000UL : 0x14000000UL)), "related element prefix");
            }
            ulong reference = result.Leaf.ObjectReference, scalar = Scalar(id), offset = Offset(id);
            if (id == 5)
                ((byte*)reference)[offset] = (byte)scalar;
            else if (id != 2)
                W64((byte*)reference + offset, scalar);
            Need(f.Done(id, reference, length, scalar) == RhpAdmissionStatus.Success, "actual scalar readback id=" + id);
            used += size;
        }
        Need(used == 160 && f.Bump->UsedBytes == 160 && f.Bump->AllocationCount == 5 && f.Control->Completed == 5 &&
            f.Check() == RhpAdmissionStatus.Success && f.Retained().IsReady && !f.Retained().ContentsAreZeroNow, "single mixed state160/5 retained");
    }
    private static void Mixed()
    {
        using F f = new();
        Need(f.Prepare() == RhpAdmissionStatus.Success && f.Bind().Succeeded, "one original bind");
        Sequence(f, true);
        foreach (int offset in new[] { 16, 40, 68, 88, 136, 144, 159 })
        {
            f.Page[offset] ^= 1;
            byte[] before = f.Snapshot();
            Need(f.Mixed(1, 0).Status == RhpAdmissionStatus.PriorObjectChanged, "prior byte corruption");
            f.Unchanged(before);
            f.Page[offset] ^= 1;
        }
        Need(f.Bind().Status == RhpNewFastStatus.AlreadyInitialized, "no second binder");
    }
    private static void Rejected()
    {
        for (int variant = 0; variant < 14; variant++)
        {
            using F f = new();
            Need(f.Prepare() == RhpAdmissionStatus.Success, "prepare");
            ulong token = f.MixedToken(2), expected = token, element = f.Token(6);
            long length = 0;
            switch (variant)
            {
                case 0:
                    token = ulong.MaxValue - 7;
                    break;
                case 1:
                    element = ulong.MaxValue - 7;
                    break;
                case 2:
                    W64((byte*)token + 8, f.Token(7));
                    break;
                case 3:
                    W32((byte*)token, 0xE0020008);
                    break;
                case 4:
                    W32((byte*)token + 4, 32);
                    break;
                case 5:
                    W32((byte*)element, 0x28000000);
                    break;
                case 6:
                    W32((byte*)element + 4, 32);
                    break;
                case 7:
                    W32(f.Image + 0x1AC, 0xC0000040);
                    break;
                case 8:
                    length = 1;
                    break;
                case 9:
                    f.Control->InProgress = 1;
                    break;
                case 10:
                    length = 0x100000000L;
                    break;
                case 11:
                    length = 0x100000001L;
                    break;
                case 12:
                    length = long.MinValue;
                    break;
                case 13:
                    length = long.MaxValue;
                    break;
            }
            byte[] before = f.Snapshot();
            var r = RhpNewFastAllocationCore.AllocateMixed(f.Access, f.Bump, f.Control, f.Records, token, expected, 2, 3, length, element);
            Need(!r.Succeeded && r.Status != RhpAdmissionStatus.AllocationRejected, "pre-read/fixture rejection " + variant);
            f.Unchanged(before);
        }
    }
    private static void Exhaustion()
    {
        using F unbound = new();
        Need(unbound.Prepare() == RhpAdmissionStatus.Success, "beforebind prepare");
        byte[] page = unbound.PageSnapshot(), state = unbound.BumpSnapshot();
        var rejection = unbound.Mixed(2, 0);
        Need(rejection.Status == RhpAdmissionStatus.AllocationRejected && rejection.Leaf.Status == RhpNewFastStatus.NotInitialized, "genuine leaf status5");
        unbound.PageUnchanged(page);
        unbound.BumpUnchanged(state);
        using F f = new();
        Need(f.Prepare() == RhpAdmissionStatus.Success && f.Bind().Succeeded, "bind");
        Sequence(f, true);
        for (int pair = 0; pair < 82; pair++)
        {
            var fixedResult = f.Mixed(1, 0);
            Need(fixedResult.Succeeded, "legitimate fixed fill");
            W64((byte*)fixedResult.Leaf.ObjectReference + 8, 0x5555555555555555);
            var arrayResult = f.Mixed(2, 0);
            Need(arrayResult.Succeeded, "legitimate byte zero fill");
            Need(f.Check() == RhpAdmissionStatus.Success, "five prior objects persist");
        }
        Need(f.Bump->UsedBytes == 4096 && f.Bump->AllocationCount == 169 && f.Control->Completed == 5, "no extra cursor/observation slot");
        for (int offset = 160; offset < 4096; offset += 48)
        {
            byte[] expected = new byte[48];
            BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8), f.Token(1));
            BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(16), 0x5555555555555555);
            BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(32), f.Token(2));
            Need(new ReadOnlySpan<byte>(f.Page + offset, 48).SequenceEqual(expected), "all legitimate fill bytes");
        }
        page = f.PageSnapshot();
        state = f.BumpSnapshot();
        rejection = f.Mixed(3, 1);
        Need(rejection.Status == RhpAdmissionStatus.AllocationRejected && rejection.Leaf.Status == RhpNewFastStatus.Exhausted, "actual pointer-size array leaf15");
        f.PageUnchanged(page);
        f.BumpUnchanged(state);
    }
    private static void Geometry()
    {
        Need(sizeof(RhpNewFastHeapAccess) == 104 && sizeof(RhpNewFastBumpState) == 80 && sizeof(RhpAllocationControl) == 96 &&
            sizeof(RhpObjectObservation) == 256 && sizeof(RhpObservationStorage) == 6672, "exact shared POD geometry");
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<RhpObservationStorage>(), "no object references in observations");
        using F f = new();
        byte[] before = f.Snapshot();
        var alias = (RhpObservationStorage*)(f.Page - 6672 + 8);
        Need(RhpNewFastAllocationCore.Prepare(f.Original, f.Access, f.Bump, f.Control, alias, f.Image, 16384) == RhpAdmissionStatus.StorageOverlap, "last eight bytes overlap full backing");
        f.Unchanged(before);
        Need(f.Prepare() == RhpAdmissionStatus.Success && f.Bind().Succeeded, "prepare");
        // Warm actual core, reset only fixture-owned storage, then measure allocator calls alone.
        for (int i = 0; i < 2; i++)
        {
            f.Reset();
            f.PrefixBytes(2, 0xE0020001, 24, f.Token(6));
            f.PrefixBytes(6, 0x14000000, 24, 0);
            f.Prepare();
            f.Bind();
            var r = f.Mixed(1, 0);
            W64((byte*)r.Leaf.ObjectReference + 8, 1);
            f.Done(1, r.Leaf.ObjectReference, 0, 1);
            long start = GC.GetAllocatedBytesForCurrentThread();
            r = f.Mixed(2, 0);
            var done = f.Done(2, r.Leaf.ObjectReference, 0, 0);
            long end = GC.GetAllocatedBytesForCurrentThread();
            Need(r.Succeeded && done == RhpAdmissionStatus.Success && end == start, "zero CLR allocation in actual shared array core");
        }
        Console.WriteLine("ARRAY_CORE_POD access=104 bump=80 control=96 observation=256 records=6672 used=160 count=5");
    }

}
#else
#endif
