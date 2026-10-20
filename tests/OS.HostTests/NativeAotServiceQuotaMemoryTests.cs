using System.Text;
using OS.Tooling;

internal static class NativeAotServiceQuotaMemoryTests
{
    private const ulong Reservation = 0x100185000, Arena = Reservation + 4096, ArenaBytes = 5 * 16777216UL, Native = 0x200000000;
    private const ulong Mmap = 0x47059a, Munmap = 0x470691, Madvise = 0x470541, Mprotect = 0x47064a, NoMemory = unchecked((ulong)-12L);
    private const string Gc = "limit=16777216 baselineHeap=30920 baselineCommitted=151552 liveBytes=2097152 liveHeap=2129504 liveCommitted=4194304 pressureBytes=12582912 pressureHeap=12615680 pressureCommitted=14680064 pressureCollections=3 oom=1\n";

    // Independent framed values only: no original ELF, call/write scheduler,
    // physical allocator, source pins, peer or actual guest is constructed.
    private enum HeldMutation { None, Advise, PartialUnmap, Shrink, GrowInto, FixedReplace, Protect }

    private static string Raw(bool segmented = false, HeldMutation mutation = HeldMutation.None, bool understateRetention = false)
    {
        var text = new StringBuilder("M16_016_BUDGET gc=16777216 nativeReserve=20971520 static=16777216 pool=37748736 entitlement=67108864\r\nTEST_START m16.014.startup\r\n");
        ulong ordinal = 0, reserved = 2 * 1048576UL;
        void Output(string value)
        {
            foreach (string part in segmented ? value.Chunk(17).Select(c => new string(c)) : new[] { value })
                text.Append($"M16_005_WRITE fd=2 bytes={Encoding.UTF8.GetByteCount(part)} data={part}\r\n");
        }
        void Observe(ulong nr, ulong result, ulong address, ulong bytes, ulong protection, ulong flags, ulong rip, ulong backed, ulong regions)
        {
            ordinal++; ulong free = 9216 - backed;
            text.Append($"M16_014_MEMORY nr={nr} regions={regions} regionLimit=256 backed={backed} pool=9216 reserved={reserved}\r\n");
            text.Append($"M16_016_TOPOLOGY identity=4294967297 ordinal={ordinal} nr={nr} pool=9216 backed={backed} free={free} freeRuns={(free == 0 ? 0 : 1)} largestFreeRun={free} regions={regions} regionLimit=256 tables=8 tableLimit=64\r\n");
            text.Append($"M16_008_CALL identity=4294967297 nr={nr} a0={address} a1={bytes} a2={protection} a3={flags} a4={ulong.MaxValue} a5=0 rip={rip} rsp=1879048184 flags=514 fs=0 gs=0 clock={ordinal} valueBits={result} action=1 status=1 ordinal={ordinal}" +
                $" waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip={rip} returnRsp=1879048184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={result}\r\n");
        }
        reserved += ArenaBytes + 4096; Observe(9, Reservation, 0, ArenaBytes + 4096, 0, 0x22, Mmap, 0, 2);
        reserved -= 4096; Observe(11, 0, Reservation, 4096, 0, 0, Munmap, 0, 2);
        Observe(28, 0, Arena, ArenaBytes, 16, 0, Madvise, 0, 2);
        Output("CLROS_APP_ENTRY minimal.api.service\nCLROS_APP_CCTOR minimal.api.service\nCLROS_APP_CTOR minimal.api.service\nCLROS_APP_BUILT minimal.api.service\n");
        Observe(10, 0, Arena, 14 * 1048576, 3, 0, Mprotect, 3584, 3);
        // PROT_NONE retains backing; only subsequent DONTNEED releases it.
        Observe(10, 0, Arena + 4 * 1048576, 10 * 1048576, 0, 0, Mprotect, 3584, 4);
        Observe(28, 0, Arena + 4 * 1048576, 10 * 1048576, 4, 0, Madvise, 1024, 3);
        Output("CLROS_SERVICE_GC_BEGIN " + Gc + "CLROS_SERVICE_LISTEN contract=v3-quota endpoint=0.0.0.0:8080\nCLROS_M10_011_READY\nCLROS_SERVICE_QUOTA_BEGIN chunkBytes=1048576 maximumChunks=64\n");
        for (int index = 0; index < 32; index++)
        {
            Output($"CLROS_SERVICE_QUOTA_ATTEMPT index={index} retainedBytes={(ulong)index * 1048576} gcCommitted=4194304\n");
            reserved += 1048576; Observe(9, Native + (ulong)index * 1048576, 0, 1048576, 3, 0x22, Mmap,
                understateRetention && index == 0 ? 1024UL : 1024 + (ulong)(index + 1) * 256, 4);
            int before = 6 + Math.Min(index, 4), after = 6 + Math.Min(index + 1, 4);
            Output($"CLROS_SERVICE_QUOTA_RETAINED index={index} retainedBytes={(ulong)(index + 1) * 1048576} collectionsBefore={before} collectionsAfter={after} gcCommitted=4194304\n");
            if (index == 0) switch (mutation)
            {
                case HeldMutation.Advise: Observe(28, 0, Native, 1048576, 4, 0, Madvise, 1280, 4); break;
                case HeldMutation.PartialUnmap: Observe(11, 0, Native + 4096, 4096, 0, 0, Munmap, 1280, 4); break;
                case HeldMutation.Shrink: Observe(25, Native, Native, 1048576, 524288, 0, 0x400202, 1280, 4); break;
                case HeldMutation.GrowInto: Observe(25, Native - 4096, Native - 4096, 4096, 8192, 0, 0x400202, 1280, 4); break;
                case HeldMutation.FixedReplace: Observe(9, Native, Native, 4096, 0, 0x32, Mmap, 1280, 4); break;
                case HeldMutation.Protect: Observe(10, 0, Native + 4096, 4096, 0, 0, Mprotect, 1280, 4); break;
            }
        }
        Output("CLROS_SERVICE_QUOTA_ATTEMPT index=32 retainedBytes=33554432 gcCommitted=4194304\n");
        Observe(9, NoMemory, 0, 1048576, 3, 0x22, Mmap, 9216, 4);
        text.Append($"M16_016_QUOTA_ABORT identity=4294967297 ordinal={ordinal} nr=9 backed=9216 pool=9216 exit=99 policy=terminate cause=physical-backing\r\n");
        text.Append($"M16_016_MEMORY poolPages=9216 peakPages=9216 endPages=0\r\nM16_008_BUDGET_END nanos={ordinal}\r\n");
        text.Append("M16_008_OWNER_END reaped=3 transfers=16 irqs=1 exit=99 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained\r\n");
        return text.ToString();
    }

    private static QemuTranscripts.ServiceQuotaMemoryInspection Read(string raw) =>
        QemuTranscripts.InspectNativeAotServiceQuotaMemoryForTests(Encoding.UTF8.GetBytes(raw), 67108864);
    private static void Reject(string raw)
    { bool rejected = false; try { _ = Read(raw); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }

    [HostTest("quota memory reports real operation-local topology strict live GC ownership and retained mmap bytes without recovery placeholders")]
    private static void ValuesAndCopies()
    {
        foreach (bool segmented in new[] { false, true })
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Raw(segmented)), before = bytes.ToArray();
            var value = QemuTranscripts.InspectNativeAotServiceQuotaMemoryForTests(bytes, 67108864);
            HostCheck.Equal(39, value.Snapshots.Count); HostCheck.Equal(33, value.PressureSnapshots);
            HostCheck.Equal(32UL, value.RetainedMappings); HostCheck.Equal(33554432UL, value.RetainedBytes);
            HostCheck.Equal(37748736UL, value.PeakBackedBytes); HostCheck.Equal(value.PeakBackedBytes, value.PressurePeakBackedBytes);
            HostCheck.Equal(16777216UL, value.GcLimitBytes); HostCheck.Equal(20971520UL, value.NativeReserveBytes);
            HostCheck.Equal(33554432UL, value.MaximumNonGcUpperBoundBytes); HostCheck.Equal(50331648UL, value.GcLimitPlusNonGcUpperBoundBytes);
            HostCheck.Equal(67108864UL, value.StaticPlusGcLimitAndNonGcUpperBoundBytes);
            HostCheck.True(value.RetainedBytes > value.NativeReserveBytes && value.GcLimitPlusNonGcUpperBoundBytes > value.PoolBytes);
            HostCheck.True(value.GcArena.ArenaIdentified); HostCheck.Equal(4194304UL, value.GcArena.LastBackedBytes);
            HostCheck.Equal(14680064UL, value.GcArena.PeakBackedBytes); HostCheck.Equal(0, value.GcArena.RevokedPages);
            HostCheck.Equal(3584UL, value.Snapshots[4].BackedPages); HostCheck.Equal(14680064UL, value.Snapshots[4].GcBackedBytes);
            HostCheck.Equal(0UL, value.Denial.FreePages); HostCheck.Equal(39UL, value.Denial.Ordinal);
            HostCheck.True(bytes.AsSpan().SequenceEqual(before)); Array.Clear(bytes);
            HostCheck.Equal(9216UL, value.Denial.BackedPages); HostCheck.Equal(39, value.Snapshots.Count);
        }
    }

    [HostTest("quota memory rejects incomplete snapshots topology budget and denial rollback or cleanup substitutions")]
    private static void RequiredObservations()
    {
        string raw = Raw(); Reject(Raw(understateRetention: true));
        foreach (var (from, to) in new[] { ("nativeReserve=20971520", "nativeReserve=16777216"), ("endPages=0", "endPages=1"),
            ("peakPages=9216", "peakPages=9215"), ("exit=99 userReserved=0", "exit=0 userReserved=0"),
            ("free=0 freeRuns=0", "free=1 freeRuns=0"), ("regionLimit=256", "regionLimit=257"),
            ("tables=8 tableLimit=64", "tables=65 tableLimit=64"), ("GC_BEGIN limit=16777216", "GC_BEGIN limit=16777217") })
        {
            string changed = raw.Replace(from, to, StringComparison.Ordinal); HostCheck.True(changed != raw); Reject(changed);
        }
        string[] rows = raw.Split("\r\n", StringSplitOptions.None);
        foreach (string prefix in new[] { "M16_014_MEMORY nr=28 ", "M16_016_TOPOLOGY identity=4294967297 ordinal=39 ", "M16_016_QUOTA_ABORT ", "M16_008_BUDGET_END " })
            Reject(string.Join("\r\n", rows.Where(row => !row.StartsWith(prefix, StringComparison.Ordinal))));
        int finalMemory = Array.FindLastIndex(rows, row => row.StartsWith("M16_014_MEMORY ", StringComparison.Ordinal));
        var changedRows = rows.ToArray(); changedRows[finalMemory] = changedRows[finalMemory].Replace("reserved=119537664", "reserved=119533568", StringComparison.Ordinal);
        HostCheck.True(!changedRows.SequenceEqual(rows)); Reject(string.Join("\r\n", changedRows));
    }

    [HostTest("quota memory preserves existing GC arena identity pins and rejects native retention revocation or recovery output")]
    private static void OwnershipAndContract()
    {
        string raw = Raw();
        foreach (var mutation in Enum.GetValues<HeldMutation>().Where(value => value != HeldMutation.None)) Reject(Raw(mutation: mutation));
        foreach (var (from, to) in new[] { ($"rip={Mmap}", "rip=4198402"), ($"nr=11 a0={Reservation} a1=4096", $"nr=11 a0={Reservation} a1=8192"),
            ("contract=v3-quota", "contract=v2"),
            ("nr=10 a0=" + (Arena + 4 * 1048576), "nr=10 a0=" + Native) })
        {
            string changed = raw.Replace(from, to, StringComparison.Ordinal); HostCheck.True(changed != raw); Reject(changed);
        }
        Reject(raw.Replace($"valueBits={Native + 1048576}", $"valueBits={Native}", StringComparison.Ordinal)
            .Replace($"returnValueBits={Native + 1048576}", $"returnValueBits={Native}", StringComparison.Ordinal));
        foreach (string extra in new[] { "CLROS_SERVICE_GC_END " + Gc, "CLROS_SERVICE_NATIVE_BEGIN retainedBytes=0\n", "CLROS_SERVICE_QUOTA_SURVIVED retainedBytes=33554432 released=True\n" })
            Reject(raw + $"M16_005_WRITE fd=2 bytes={Encoding.UTF8.GetByteCount(extra)} data={extra}\r\n");
        bool rejected = false; try { _ = QemuTranscripts.InspectNativeAotServiceQuotaMemoryForTests(Encoding.UTF8.GetBytes(raw), 67112960); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
