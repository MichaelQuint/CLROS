using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using CaptureSeed = OS.Boot.AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown;

internal static class AnyFirmwareDerivedEntropySeedNeededFirmwareShutdownTests
{

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private sealed class Fixture
    {
        internal readonly byte[] Scratch = Pattern(112, 0xA1);
        internal readonly byte[] Destination = Pattern(112, 0xD3);
        internal readonly FirmwareServicesGuard Firmware = new();
        internal readonly Source Provider = new();
        internal readonly GuardedPlatformRandom Random;
        internal FirmwareEntropySeedPublication Publication;
        internal Fixture() { Random = new(Firmware, Provider); Provider.Owner = this; }
        internal FirmwareEntropySeedResult Capture(int count = 32) => CaptureSeed.Capture(Random,
            count, Scratch.AsSpan(8, 96), Destination.AsSpan(8, 96), ref Publication);
    }

    private sealed class Source : IPlatformRandomSource
    {
        internal Fixture? Owner;
        internal int Calls;
        internal int LastLength;
        internal int WriteLimit = int.MaxValue;
        internal int Reported = -1;
        internal int Failure;
        internal int Constant = -1;
        internal bool SawUnpublishedAndUntouched = true;
        public PlatformRandomSourceResult FillBytes(Span<byte> destination)
        {
            Calls++;
            LastLength = destination.Length;
            if (Owner is not null)
            {
                SawUnpublishedAndUntouched &= Owner.Publication.RetainedBytes == 0;
                for (int i = 0; i < Owner.Destination.Length; i++)
                    SawUnpublishedAndUntouched &= Owner.Destination[i] == PatternByte(i, 0xD3);
            }
            int count = Math.Min(WriteLimit, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = Constant < 0 ? unchecked((byte)(0x31 + 7 * i)) : (byte)Constant;
            return Failure switch
            {
                1 => PlatformRandomSourceResult.Unavailable(),
                2 => PlatformRandomSourceResult.FirmwareError(0x8000000000000007UL),
                3 => default,
                _ => PlatformRandomSourceResult.Filled(Reported < 0 ? destination.Length : Reported)
            };
        }
    }

    private sealed class ResultSource(PlatformRandomResult result) : IPlatformRandom
    {
        internal int Calls;
        public PlatformRandomResult FillBytes(Span<byte> destination)
        {
            Calls++;
            destination.Fill(0x61);
            return result;
        }
    }

    [HostTest("m03.003", HostLane.Fast, 0)]
    private static void TestSuccess()
    {
        foreach (int count in new[] { 32, 64 })
        {
            Fixture f = new();
            FirmwareEntropySeedResult r = f.Capture(count);
            Check(r.Succeeded && r.Status == FirmwareEntropySeedStatus.Success
                && r.RequestedBytes == count && r.SourceResult.BytesWritten == count
                && r.SourceResult.Mode == FirmwareServicesMode.BootServicesAvailable
                && f.Publication.IsPublished && f.Publication.RetainedBytes == count, "success metadata");
            Check(f.Provider.Calls == 1 && f.Provider.LastLength == count
                && f.Provider.SawUnpublishedAndUntouched, "one bounded source call before publication");
            for (int i = 0; i < 112; i++)
            {
                byte expected = i >= 8 && i < 8 + count ? unchecked((byte)(0x31 + 7 * (i - 8)))
                    : PatternByte(i, 0xD3);
                Check(f.Destination[i] == expected, "retained literal bytes and full guards");
                Check(f.Scratch[i] == (i >= 8 && i < 8 + count ? (byte)0 : PatternByte(i, 0xA1)),
                    "requested scratch cleared and full tail preserved");
            }
        }
        Fixture exact = new();
        FirmwareEntropySeedResult exactResult = CaptureSeed.Capture(exact.Random, 64,
            exact.Scratch.AsSpan(8, 64), exact.Destination.AsSpan(8, 64), ref exact.Publication);
        Check(exactResult.Succeeded && exact.Provider.LastLength == 64, "exact payload capacity");
    }

    [HostTest("m03.003 retains bytes after scratch mutation and firmware denial", HostLane.Fast, 1)]
    private static void TestRetention()
    {
        Fixture f = new();
        Check(f.Capture(64).Succeeded, "initial retention");
        byte[] retained = (byte[])f.Destination.Clone();
        f.Scratch.AsSpan().Fill(0xBC);
        _ = f.Firmware.MarkBootServicesExited();
        Check(f.Destination.AsSpan().SequenceEqual(retained) && f.Publication.RetainedBytes == 64,
            "retention independent of scratch and firmware mode");
        byte[] scratch = (byte[])f.Scratch.Clone();
        FirmwareEntropySeedResult r = f.Capture();
        Check(r.Status == FirmwareEntropySeedStatus.AlreadyPublished && f.Provider.Calls == 1
            && f.Destination.AsSpan().SequenceEqual(retained) && f.Scratch.AsSpan().SequenceEqual(scratch),
            "repeat does not reacquire firmware");
    }

    [HostTest("m03.003 admits zero and repeated source bytes", HostLane.Fast, 2)]
    private static void TestNoQualityHeuristic()
    {
        foreach (int constant in new[] { 0, 0xA5 })
        {
            Fixture f = new(); f.Provider.Constant = constant;
            Check(f.Capture().Succeeded, "valid constant source accepted");
            for (int i = 0; i < 32; i++) Check(f.Destination[8 + i] == constant, "constant retained privately");
        }
    }

    [HostTest("m03.003 rejects invalid lengths and capacities before source", HostLane.Fast, 3)]
    private static void TestGeometry()
    {
        foreach (int count in new[] { int.MinValue, -1, 0, 1, 31, 33, 63, 65, int.MaxValue })
        {
            Fixture f = new();
            AssertRejected(f, f.Random, count, 96, 96, FirmwareEntropySeedStatus.InvalidRequestLength, true);
        }
        foreach (int capacity in new[] { 0, 1, 31, 32, 63 })
        {
            Fixture f = new();
            AssertRejected(f, f.Random, 32, capacity, 96, FirmwareEntropySeedStatus.ScratchTooSmall, true);
            AssertRejected(f, f.Random, 32, 96, capacity, FirmwareEntropySeedStatus.DestinationTooSmall, true);
        }
        Fixture missing = new();
        AssertRejected(missing, null, 32, 96, 96, FirmwareEntropySeedStatus.InvalidSource, true);
    }

    [HostTest("m03.003 rejects complete buffer aliases", HostLane.Fast, 4)]
    private static void TestBufferAliases()
    {
        foreach ((int a, int al, int b, int bl) in new[] {
            (0, 96, 0, 96), (0, 96, 1, 96), (1, 96, 0, 96),
            (0, 96, 64, 64), (64, 64, 0, 96), (0, 96, 95, 64) })
        {
            byte[] shared = Pattern(176, 0xC3), before = (byte[])shared.Clone();
            Fixture f = new();
            FirmwareEntropySeedResult r = CaptureSeed.Capture(f.Random, 32, shared.AsSpan(a, al),
                shared.AsSpan(b, bl), ref f.Publication);
            Check(r.Status == FirmwareEntropySeedStatus.AliasedBuffers && f.Provider.Calls == 0
                && f.Publication.RetainedBytes == 0 && shared.AsSpan().SequenceEqual(before), "full alias rejection");
        }
        byte[] adjacent = Pattern(144, 0xC3); Fixture ok = new();
        Check(CaptureSeed.Capture(ok.Random, 32, adjacent.AsSpan(8, 64), adjacent.AsSpan(72, 64),
            ref ok.Publication).Succeeded, "exactly adjacent disjoint spans");
        Check(adjacent[7] == PatternByte(7, 0xC3) && adjacent[136] == PatternByte(136, 0xC3), "adjacent guards");
    }

    [HostTest("m03.003 rejects publication aliases before source", HostLane.Fast, 5)]
    private static void TestPublicationAliases()
    {
        foreach (bool inScratch in new[] { false, true })
        foreach (int offset in new[] { 8, 72, 100 })
        {
            Fixture f = new(); byte[] owner = inScratch ? f.Scratch : f.Destination;
            owner.AsSpan(offset, 4).Clear();
            ref FirmwareEntropySeedPublication publication = ref MemoryMarshal.AsRef<FirmwareEntropySeedPublication>(owner.AsSpan(offset, 4));
            byte[] scratch = (byte[])f.Scratch.Clone(), destination = (byte[])f.Destination.Clone();
            FirmwareEntropySeedResult r = CaptureSeed.Capture(f.Random, 32, f.Scratch.AsSpan(8, 96),
                f.Destination.AsSpan(8, 96), ref publication);
            Check(r.Status == FirmwareEntropySeedStatus.AliasedBuffers && f.Provider.Calls == 0
                && f.Scratch.AsSpan().SequenceEqual(scratch) && f.Destination.AsSpan().SequenceEqual(destination),
                "publication payload or unused-tail alias");
        }
    }

    [HostTest("m03.003 reports firmware denial without source calls", HostLane.Fast, 6)]
    private static void TestFirmwareDenial()
    {
        foreach (bool exited in new[] { false, true })
        {
            Fixture f = new();
            if (exited) _ = f.Firmware.MarkBootServicesExited(); else _ = f.Firmware.EnterUefiGoneSimulation();
            FirmwareEntropySeedResult r = AssertRejected(f, f.Random, 32, 96, 96,
                FirmwareEntropySeedStatus.FirmwareServicesUnavailable, true);
            Check(r.SourceResult.Status == PlatformRandomStatus.FirmwareServicesUnavailable
                && r.SourceResult.Mode == (exited ? FirmwareServicesMode.BootServicesExited : FirmwareServicesMode.UefiGoneSimulated),
                "firmware failure provenance");
        }
    }

    [HostTest("m03.003 preserves retained storage on source failures", HostLane.Fast, 7)]
    private static void TestSourceFailures()
    {
        foreach (int failure in new[] { 1, 2, 3 })
        {
            Fixture f = new(); f.Provider.Failure = failure; f.Provider.WriteLimit = 7;
            FirmwareEntropySeedResult r = AssertRejected(f, f.Random, 32, 96, 96,
                failure == 1 ? FirmwareEntropySeedStatus.SourceUnavailable : FirmwareEntropySeedStatus.SourceFailed, false);
            Check(f.Provider.Calls == 1 && f.Provider.LastLength == 32, "one source failure call");
            Check(r.SourceResult.FirmwareStatus == (failure == 2 ? 0x8000000000000007UL : 0), "firmware status retained");
            for (int i = 0; i < 112; i++)
                Check(f.Scratch[i] == (i >= 8 && i < 15 ? (byte)(0x31 + 7 * (i - 8)) : PatternByte(i, 0xA1)),
                    "failed source scratch mutation bounded");
        }
    }

    [HostTest("m03.003 rejects inexact and default source results", HostLane.Fast, 8)]
    private static void TestSourceResults()
    {
        foreach (int reported in new[] { 1, 31, 33, 64, int.MaxValue })
        {
            Fixture f = new(); f.Provider.Reported = reported; f.Provider.WriteLimit = 3;
            AssertRejected(f, f.Random, 32, 96, 96, FirmwareEntropySeedStatus.WrongLength, false);
            Check(f.Provider.Calls == 1, "inexact count no retry");
        }
        FirmwareCallResult allowed = FirmwareCallResult.Allowed(PlatformRandomOperations.FillBytes,
            FirmwareServicesMode.BootServicesAvailable);
        foreach (PlatformRandomResult result in new[] {
            default, PlatformRandomResult.Fill(allowed, PlatformRandomSourceResult.Filled(64), 64),
            PlatformRandomResult.Fill(FirmwareCallResult.Allowed(PlatformRandomOperations.FillBytes,
                FirmwareServicesMode.UefiGoneSimulated), PlatformRandomSourceResult.Filled(32), 32) })
        {
            Fixture f = new(); ResultSource source = new(result);
            AssertRejected(f, source, 32, 96, 96, result.Succeeded && result.Mode == FirmwareServicesMode.BootServicesAvailable
                ? FirmwareEntropySeedStatus.WrongLength : FirmwareEntropySeedStatus.InvalidSourceResult, false);
            Check(source.Calls == 1, "invalid result no retry");
        }
    }

    [HostTest("m03.003 rejects repeated and malformed publication", HostLane.Fast, 9)]
    private static void TestPublication()
    {
        foreach (int value in new[] { int.MinValue, -1, 1, 31, 32, 33, 63, 64, 65, int.MaxValue })
        {
            Fixture f = new();
            Span<byte> bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref f.Publication, 1));
            MemoryMarshal.Write(bytes, in value);
            AssertRejected(f, f.Random, 32, 96, 96, value is 32 or 64
                ? FirmwareEntropySeedStatus.AlreadyPublished : FirmwareEntropySeedStatus.InvalidPublication, true);
        }
    }

    [HostTest("m03.003 status precedence preserves all admitted storage", HostLane.Fast, 10)]
    private static void TestPrecedence()
    {
        Fixture f = new();
        AssertRejected(f, null, -1, 0, 0, FirmwareEntropySeedStatus.InvalidSource, true);
        AssertRejected(f, f.Random, -1, 0, 0, FirmwareEntropySeedStatus.InvalidRequestLength, true);
        AssertRejected(f, f.Random, 32, 0, 0, FirmwareEntropySeedStatus.ScratchTooSmall, true);
        int forged = 1;
        MemoryMarshal.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref f.Publication, 1)), in forged);
        AssertRejected(f, null, -1, 0, 0, FirmwareEntropySeedStatus.InvalidPublication, true);
    }

    private static FirmwareEntropySeedResult AssertRejected(Fixture f, IPlatformRandom? source, int count,
        int scratchLength, int destinationLength, FirmwareEntropySeedStatus status, bool unchangedScratch)
    {
        byte[] scratch = (byte[])f.Scratch.Clone(), destination = (byte[])f.Destination.Clone();
        byte[] publication = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref f.Publication, 1)).ToArray();
        int calls = f.Provider.Calls;
        FirmwareEntropySeedResult r = CaptureSeed.Capture(source, count, f.Scratch.AsSpan(8, scratchLength),
            f.Destination.AsSpan(8, destinationLength), ref f.Publication);
        Check(!r.Succeeded && r.Status == status && r.RequestedBytes == count, "exact rejection status");
        Check(f.Destination.AsSpan().SequenceEqual(destination)
            && MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref f.Publication, 1)).SequenceEqual(publication),
            "whole destination and publication unchanged after each rejection");
        if (unchangedScratch)
            Check(f.Provider.Calls == calls && f.Scratch.AsSpan().SequenceEqual(scratch), "pre-source rejection unchanged");
        return r;
    }

    [HostTest("m03.003 POD and warmed zero allocation", HostLane.Fast, 11)]
    private static void TestNoAllocation()
    {
        Check(!RuntimeHelpers.IsReferenceOrContainsReferences<FirmwareEntropySeedPublication>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FirmwareEntropySeedResult>()
            && Unsafe.SizeOf<FirmwareEntropySeedPublication>() == 4, "publication and result POD");
        Fixture f = new(); f.Provider.Owner = null;
        for (int i = 0; i < 256; i++) { f.Publication = default; _ = f.Capture(64); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool success = true;
        for (int i = 0; i < 1024; i++)
        {
            f.Publication = default;
            success &= f.Capture(64).Succeeded;
            success &= f.Capture(64).Status == FirmwareEntropySeedStatus.AlreadyPublished;
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        Check(success && after == before, "warmed capture and repeat rejection allocate zero bytes");
    }

    private static byte PatternByte(int index, int salt) => unchecked((byte)(salt + index * 13));
    private static byte[] Pattern(int length, int salt)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++) bytes[i] = PatternByte(i, salt);
        return bytes;
    }
}
