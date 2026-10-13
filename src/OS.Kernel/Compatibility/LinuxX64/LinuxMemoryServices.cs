using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Single-CPU anonymous Linux mappings backed by an exclusive physical pool.
/// Sparse reservations own virtual intervals without backing or page tables.
/// Mutations build and audit a complete candidate root before changing live
/// tables, backing or accounting. The caller runs on a separate kernel root,
/// keeps the target root inactive throughout each call, then publishes/reloads
/// CR3 before resuming its user. This class does not execute CR3/TLB instructions.
/// All borrowed storage/static ranges must remain mapped, stable, exclusive and
/// nonaliasing for the state lifetime. No firmware, allocations, demand faults,
/// shared/file mappings, overcommit, SMP or process lifetime is implemented.
/// Linux negative errno values describe rejection; failure leaves the target
/// root, backing and ownership unchanged (candidate tables are scratch).
/// </summary>
public static unsafe class LinuxMemoryServices
{
    public const uint MaximumRegions = 64, MaximumPoolPages = 4096;
    // A partial interval can create two temporary fragments. Enforce the live
    // quota after coalescing, rather than rejecting an operation that reduces it.
    private const uint WorkingRegions = MaximumRegions + 2;
    public const ulong DynamicStart = 0x100000000;
    private const ulong Page = 4096;
    private const long Invalid = -22, NoMemory = -12, Fault = -14, Exists = -17;

    /// <summary>
    /// Borrowed low-4GiB physical pool and target table arena, their kernel
    /// aliases, distinct scratch tables and immutable non-pool static mappings.
    /// ReservationBytes is an independent virtual quota, not physical capacity.
    /// The owner proves physical allocation, inactive-root authority and lifetime.
    /// </summary>
    public struct Admission
    {
        public byte* Pool;
        public ulong PoolPhysical, RootPhysical, ReservationBytes;
        public uint PoolPages, TablePages, StaticCount;
        public ulong* Tables;
        public ulong* CandidateTables;
        public X64UserMappingRange* StaticRanges;
        public X64PageTableEncodingPreconditions Conditions;
    }

    /// <summary>
    /// One-use caller-owned state. Retain it and all admitted storage until
    /// user termination; do not copy a live state or mutate it concurrently.
    /// Counters separate virtual ownership, physical pool use and table cost.
    /// </summary>
    public struct State
    {
        internal Admission Memory;
        internal uint Count, Initialized;
        internal fixed ulong Regions[(int)MaximumRegions * 4];
        public ulong ReservedBytes { get; internal set; }
        public uint BackedPages { get; internal set; }
        public X64UserAddressSpaceResult Root { get; internal set; }
    }

    private struct Region
    {
        public ulong Start, Bytes, Backing;
        public ulong FreshFrom;
        public uint Protection;
        public bool Fresh, NoDump;
    }

    /// <summary>
    /// Admit empty target tables and build the initial static root. Clears the
    /// exclusive pool only after complete admission and candidate-root audit.
    /// A failed begin leaves state, pool and target tables unchanged.
    /// </summary>
    public static bool Begin(State* state, Admission memory)
        => Initialize(state, memory, default, false);

    /// <summary>
    /// Adopt an already audited static target root while it is inactive. The
    /// original root must exactly match the borrowed static inventory. Rejection
    /// preserves it and the pool; success starts empty dynamic ownership.
    /// </summary>
    public static bool Adopt(State* state, Admission memory, X64UserAddressSpaceResult original)
        => Initialize(state, memory, original, true);

    private static bool Initialize(State* state, Admission memory, X64UserAddressSpaceResult original, bool adopt)
    {
        if (!Storage(state, memory) || state->Initialized != 0 || state->Count != 0 ||
            state->ReservedBytes != 0 || state->BackedPages != 0 || state->Root.Status != X64UserAddressSpaceStatus.Uninitialized)
            return false;
        for (uint i = 0; i < MaximumRegions * 4; i++) if (state->Regions[i] != 0) return false;
        if (adopt)
        {
            if (!X64UserAddressSpace.Audit(memory.StaticRanges, memory.StaticCount, memory.Tables,
                memory.RootPhysical, memory.TablePages, memory.Conditions, original)) return false;
        }
        else for (uint i = 0; i < memory.TablePages * 512; i++) if (memory.Tables[i] != 0) return false;
        for (uint i = 0; i < memory.StaticCount; i++)
            if (memory.StaticRanges[i].Pages > 32768 ||
                Overlap(memory.StaticRanges[i].PhysicalStart, memory.StaticRanges[i].Pages * Page, memory.PoolPhysical, memory.PoolPages * Page) ||
                Overlap(memory.StaticRanges[i].VirtualStart, memory.StaticRanges[i].Pages * Page, DynamicStart, X64UserAddressSpace.UserEnd - DynamicStart))
                return false;
        X64UserAddressSpace.Clear(memory.CandidateTables, memory.TablePages);
        var root = X64UserAddressSpace.Build(memory.StaticRanges, memory.StaticCount,
            memory.CandidateTables, memory.RootPhysical, memory.TablePages, memory.Conditions);
        if (!root.Succeeded) return false;
        Clear(memory.Pool, memory.PoolPages * Page);
        CopyTables(memory);
        state->Memory = memory; state->Root = root; state->Initialized = 1;
        return true;
    }

    /// <summary>
    /// mmap for MAP_PRIVATE|MAP_ANONYMOUS, optional MAP_NORESERVE and the two
    /// fixed-address forms. MAP_FIXED replaces only this owner's dynamic ranges;
    /// NOREPLACE returns EEXIST. Hints may fall back to first-fit. Length rounds
    /// up to 4KiB. PROT_NONE consumes virtual quota only; accessible pages get
    /// real zero-filled backing. Unknown flags/protections fail before mutation.
    /// </summary>
    public static long Map(State* state, ulong address, ulong bytes, uint protection, ulong flags, ulong descriptor, ulong offset)
    {
        if (!Valid(state)) return Fault;
        const ulong fixedFlag = 0x10, noReplace = 0x100000, noReserve = 0x4000;
        if ((flags & 0x22) != 0x22 || (flags & ~(0x22UL | fixedFlag | noReplace | noReserve)) != 0 ||
            (flags & (fixedFlag | noReplace)) == (fixedFlag | noReplace) ||
            unchecked((uint)descriptor) != 0xffffffffU || offset != 0 || !Protection(protection) || !Round(bytes, out bytes)) return Invalid;
        Region* candidate = stackalloc Region[(int)WorkingRegions];
        uint count = CopyRegions(state, candidate);
        bool replace = (flags & fixedFlag) != 0, exact = replace || (flags & noReplace) != 0;
        if (exact)
        {
            if (!Range(address, bytes)) return Invalid;
            if (!replace && Intersects(candidate, count, address, bytes)) return Exists;
            if (replace && !Split(candidate, ref count, address, bytes, 0, true)) return NoMemory;
        }
        else
        {
            address &= ~4095UL;
            if (!Range(address, bytes) || Intersects(candidate, count, address, bytes))
            {
                address = DynamicStart;
                for (uint i = 0; i < count; i++)
                {
                    if (address <= candidate[i].Start && bytes <= candidate[i].Start - address) break;
                    if (address < candidate[i].Start + candidate[i].Bytes) address = candidate[i].Start + candidate[i].Bytes;
                }
                if (!Range(address, bytes)) return NoMemory;
            }
        }
        if (count == WorkingRegions) return NoMemory;
        Region added = new() { Start = address, Bytes = bytes, Protection = protection };
        if (protection != 0 && !Allocate(state->Memory, candidate, count, ref added)) return NoMemory;
        candidate[count++] = added;
        Sort(candidate, count);
        return Apply(state, candidate, count) == 0 ? (long)address : NoMemory;
    }

    /// <summary>
    /// mprotect requires complete reservation coverage. Partial ranges split
    /// ownership. PROT_NONE removes leaves but retains existing backing/data;
    /// first accessibility commits actual pool pages. W+X is refused.
    /// </summary>
    public static long Protect(State* state, ulong address, ulong bytes, uint protection)
    {
        if (!Valid(state)) return Fault;
        if (!Protection(protection) || !Round(bytes, out bytes) || !Range(address, bytes)) return Invalid;
        Region* candidate = stackalloc Region[(int)WorkingRegions];
        uint count = CopyRegions(state, candidate);
        if (!Covered(candidate, count, address, bytes)) return NoMemory;
        if (!Split(candidate, ref count, address, bytes, protection, false)) return NoMemory;
        for (uint i = 0; i < count; i++)
            if (candidate[i].Protection != 0 && candidate[i].Backing == 0 && !Allocate(state->Memory, candidate, count, ref candidate[i])) return NoMemory;
        return Apply(state, candidate, count);
    }

    /// <summary>
    /// munmap releases complete/partial dynamic ownership and scrubs freed pool
    /// pages. Absent dynamic intervals succeed as on Linux; static kernel/ELF
    /// mappings and addresses outside the admitted window cannot be released.
    /// </summary>
    public static long Unmap(State* state, ulong address, ulong bytes)
    {
        if (!Valid(state)) return Fault;
        if (!Round(bytes, out bytes) || !Range(address, bytes)) return Invalid;
        Region* candidate = stackalloc Region[(int)WorkingRegions];
        uint count = CopyRegions(state, candidate);
        if (!Split(candidate, ref count, address, bytes, 0, true)) return NoMemory;
        return Apply(state, candidate, count);
    }

    /// <summary>
    /// MADV_DONTNEED discards anonymous content. Inaccessible ranges return their
    /// backing to the pool; accessible ranges are eagerly zeroed because demand
    /// faults are not yet supported. No promise of physical reclamation is made
    /// for accessible ranges. DONTDUMP/DODUMP retain actual per-interval dump
    /// exclusion policy for later exporters; they do not claim a dump service.
    /// Other advice values fail with EINVAL.
    /// </summary>
    public static long Advise(State* state, ulong address, ulong bytes, uint advice)
    {
        if (!Valid(state)) return Fault;
        if ((advice != 4 && advice != 16 && advice != 17) || !Round(bytes, out bytes) || !Range(address, bytes)) return Invalid;
        Region* candidate = stackalloc Region[(int)WorkingRegions];
        uint count = CopyRegions(state, candidate);
        if (!Covered(candidate, count, address, bytes)) return NoMemory;
        // Preserve each region's protection while isolating the advised extent.
        if (!Split(candidate, ref count, address, bytes, 0, false, true)) return NoMemory;
        for (uint i = 0; i < count; i++)
            if (candidate[i].Start >= address && candidate[i].Start < address + bytes)
            {
                if (advice == 16 || advice == 17) candidate[i].NoDump = advice == 16;
                else if (candidate[i].Protection == 0) candidate[i].Backing = 0;
                else if (candidate[i].Backing != 0) candidate[i].Fresh = true;
            }
        return Apply(state, candidate, count);
    }

    /// <summary>
    /// Query this owner's retained dump-exclusion policy over complete dynamic
    /// reservation coverage. Unknown, stale or mixed/included ranges return false.
    /// </summary>
    public static bool IsDumpExcluded(State* state, ulong address, ulong bytes)
    {
        if (!Valid(state) || !Range(address, bytes)) return false;
        Region* regions = stackalloc Region[(int)MaximumRegions]; uint count = CopyRegions(state, regions);
        if (!Covered(regions, count, address, bytes)) return false;
        for (uint i = 0; i < count; i++) if (Overlap(address, bytes, regions[i].Start, regions[i].Bytes) && !regions[i].NoDump) return false;
        return true;
    }

    /// <summary>
    /// In-place mremap without move/fixed flags. One complete dynamic interval
    /// may shrink or grow into a free adjacent VA/backing extent, retaining its
    /// bytes and zero-filling new pages. Immutable startup mappings cannot grow:
    /// mapped stack probes return ENOMEM and absent guards EFAULT. This supports
    /// musl's genuine stack-bound discovery without an endless synthetic failure.
    /// </summary>
    public static long Remap(State* state, ulong address, ulong oldBytes, ulong newBytes, ulong flags)
    {
        if (!Valid(state)) return Fault;
        if (flags != 0 || !Round(oldBytes, out oldBytes) || !Round(newBytes, out newBytes) ||
            address < Page || (address & 4095) != 0 || address >= X64UserAddressSpace.UserEnd ||
            oldBytes > X64UserAddressSpace.UserEnd - address || newBytes > X64UserAddressSpace.UserEnd - address) return Invalid;
        Region* candidate = stackalloc Region[(int)WorkingRegions]; uint count = CopyRegions(state, candidate);
        for (uint i = 0; i < count; i++)
        {
            Region r = candidate[i];
            if (address < r.Start || address - r.Start >= r.Bytes) continue;
            if (oldBytes > r.Bytes - (address - r.Start)) return Fault;
            if (oldBytes == newBytes) return (long)address;
            if (address != r.Start || oldBytes != r.Bytes) return NoMemory;
            if (newBytes > oldBytes)
            {
                if (Intersects(candidate, count, address + oldBytes, newBytes - oldBytes)) return NoMemory;
                if (r.Backing != 0)
                {
                    if (newBytes > state->Memory.PoolPages * Page ||
                        r.Backing - state->Memory.PoolPhysical > state->Memory.PoolPages * Page - newBytes) return NoMemory;
                    for (uint j = 0; j < count; j++) if (j != i && candidate[j].Backing != 0 &&
                        Overlap(r.Backing + oldBytes, newBytes - oldBytes, candidate[j].Backing, candidate[j].Bytes)) return NoMemory;
                }
            }
            if (newBytes > oldBytes && r.Backing != 0)
            { candidate[i].Fresh = true; candidate[i].FreshFrom = oldBytes; }
            candidate[i].Bytes = newBytes;
            return Apply(state, candidate, count) == 0 ? (long)address : NoMemory;
        }
        for (uint i = 0; i < state->Memory.StaticCount; i++)
        {
            var r = state->Memory.StaticRanges[i];
            if (!r.User || address < r.VirtualStart || address - r.VirtualStart >= r.Pages * Page) continue;
            if (oldBytes > r.Pages * Page - (address - r.VirtualStart)) return Fault;
            return oldBytes == newBytes ? (long)address : NoMemory;
        }
        return Fault;
    }

    /// <summary>
    /// Re-audit ownership and the entire target root, accepting only hardware
    /// accessed/dirty changes. This is memory evidence, not a CR3 observation.
    /// The owner must still establish pointer mapping and inactive-root lifetime.
    /// </summary>
    public static bool Audit(State* state) => Valid(state);

    private static bool Valid(State* state)
    {
        if (state == null || ((ulong)state & 7) != 0 || state->Initialized != 1 ||
            !Storage(state, state->Memory) || state->Count > MaximumRegions) return false;
        for (uint i = 0; i < state->Count; i++) if ((state->Regions[i * 4 + 3] & ~0x107UL) != 0) return false;
        Region* regions = stackalloc Region[(int)MaximumRegions];
        uint count = CopyRegions(state, regions);
        if (!Accounting(state->Memory, regions, count, out ulong reserved, out uint backed) ||
            reserved != state->ReservedBytes || backed != state->BackedPages) return false;
        X64UserMappingRange* mappings = stackalloc X64UserMappingRange[(int)X64UserAddressSpace.MaximumRanges];
        return Mappings(state->Memory, regions, count, mappings, out uint mapped) &&
            X64UserAddressSpace.Audit(mappings, mapped, state->Memory.Tables, state->Memory.RootPhysical,
                state->Memory.TablePages, state->Memory.Conditions, state->Root);
    }

    private static long Apply(State* state, Region* regions, uint count)
    {
        var memory = state->Memory;
        count = Coalesce(regions, count);
        if (count > MaximumRegions) return NoMemory;
        if (!Accounting(memory, regions, count, out ulong reserved, out uint backed)) return NoMemory;
        X64UserMappingRange* mappings = stackalloc X64UserMappingRange[(int)X64UserAddressSpace.MaximumRanges];
        if (!Mappings(memory, regions, count, mappings, out uint mapped)) return NoMemory;
        X64UserAddressSpace.Clear(memory.CandidateTables, memory.TablePages);
        var root = X64UserAddressSpace.Build(mappings, mapped, memory.CandidateTables,
            memory.RootPhysical, memory.TablePages, memory.Conditions);
        if (!root.Succeeded) return NoMemory;
        // All fallible work is complete. The inactive-root, exclusive-pool
        // precondition allows zero-fill before publishing the candidate leaves.
        for (uint page = 0; page < memory.PoolPages; page++)
        {
            ulong physical = memory.PoolPhysical + page * Page;
            bool held = false, fresh = false;
            for (uint i = 0; i < count; i++)
                if (regions[i].Backing != 0 && physical >= regions[i].Backing && physical - regions[i].Backing < regions[i].Bytes)
                { held = true; fresh = regions[i].Fresh && physical - regions[i].Backing >= regions[i].FreshFrom; break; }
            if (!held || fresh) Clear(memory.Pool + page * Page, Page);
        }
        CopyTables(memory);
        for (uint i = 0; i < MaximumRegions; i++)
        {
            Region r = i < count ? regions[i] : default;
            state->Regions[i * 4] = r.Start; state->Regions[i * 4 + 1] = r.Bytes;
            state->Regions[i * 4 + 2] = r.Backing; state->Regions[i * 4 + 3] = r.Protection | (r.NoDump ? 0x100UL : 0UL);
        }
        state->Count = count; state->ReservedBytes = reserved; state->BackedPages = backed; state->Root = root;
        return 0;
    }

    private static bool Storage(State* state, Admission m)
    {
        if (state == null || ((ulong)state & 7) != 0 || (ulong)state > ~0UL - (ulong)sizeof(State) ||
            m.PoolPages == 0 || m.PoolPages > MaximumPoolPages || m.TablePages < 4 || m.TablePages > X64UserAddressSpace.MaximumTables ||
            m.StaticCount == 0 || m.StaticCount >= X64UserAddressSpace.MaximumRanges ||
            m.ReservationBytes == 0 || (m.ReservationBytes & 4095) != 0 || m.ReservationBytes > X64UserAddressSpace.UserEnd - DynamicStart ||
            m.PoolPhysical < Page || (m.PoolPhysical & 4095) != 0 || m.PoolPhysical > X64UserAddressSpace.PhysicalEnd - m.PoolPages * Page ||
            m.RootPhysical < Page || (m.RootPhysical & 4095) != 0 || m.RootPhysical > X64UserAddressSpace.PhysicalEnd - m.TablePages * Page ||
            Overlap(m.RootPhysical, m.TablePages * Page, m.PoolPhysical, m.PoolPages * Page) ||
            !Buffer(m.Pool, m.PoolPages * Page, 4096) || !Buffer(m.Tables, m.TablePages * Page, 4096) ||
            !Buffer(m.CandidateTables, m.TablePages * Page, 4096) || !Buffer(m.StaticRanges, m.StaticCount * (ulong)sizeof(X64UserMappingRange), 8)) return false;
        ulong* starts = stackalloc ulong[5] { (ulong)state, (ulong)m.Pool, (ulong)m.Tables, (ulong)m.CandidateTables, (ulong)m.StaticRanges };
        ulong* lengths = stackalloc ulong[5] { (ulong)sizeof(State), m.PoolPages * Page, m.TablePages * Page, m.TablePages * Page, m.StaticCount * (ulong)sizeof(X64UserMappingRange) };
        for (uint i = 0; i < 5; i++) for (uint j = 0; j < i; j++) if (Overlap(starts[i], lengths[i], starts[j], lengths[j])) return false;
        return m.Conditions.Validate() == X64PageTableEntryStatus.Success;
    }

    private static bool Accounting(Admission memory, Region* regions, uint count, out ulong reserved, out uint backed)
    {
        reserved = 0; backed = 0;
        for (uint i = 0; i < count; i++)
        {
            Region r = regions[i];
            if (!Range(r.Start, r.Bytes) || !Protection(r.Protection) ||
                (i != 0 && regions[i - 1].Start + regions[i - 1].Bytes > r.Start) ||
                r.Bytes > memory.ReservationBytes - reserved) return false;
            reserved += r.Bytes;
            if (r.Backing == 0) { if (r.Protection != 0) return false; continue; }
            if ((r.Backing & 4095) != 0 || r.Backing < memory.PoolPhysical || r.Bytes > memory.PoolPages * Page ||
                r.Backing - memory.PoolPhysical > memory.PoolPages * Page - r.Bytes) return false;
            for (uint j = 0; j < i; j++) if (regions[j].Backing != 0 && Overlap(r.Backing, r.Bytes, regions[j].Backing, regions[j].Bytes)) return false;
            backed += (uint)(r.Bytes / Page);
        }
        return backed <= memory.PoolPages;
    }

    private static bool Mappings(Admission memory, Region* regions, uint count, X64UserMappingRange* mappings, out uint mapped)
    {
        mapped = memory.StaticCount;
        for (uint i = 0; i < mapped; i++) mappings[i] = memory.StaticRanges[i];
        for (uint i = 0; i < count; i++)
        {
            Region r = regions[i];
            if (r.Protection == 0) continue;
            if (mapped == X64UserAddressSpace.MaximumRanges || r.Backing == 0) return false;
            mappings[mapped++] = new() { VirtualStart = r.Start, PhysicalStart = r.Backing, Pages = r.Bytes / Page,
                Protection = (PageProtection)r.Protection, Cache = PageCachePolicy.WriteBack, User = true };
        }
        return true;
    }

    private static bool Allocate(Admission memory, Region* regions, uint count, ref Region r)
    {
        if (r.Bytes > memory.PoolPages * Page) return false;
        for (ulong physical = memory.PoolPhysical; physical <= memory.PoolPhysical + memory.PoolPages * Page - r.Bytes; physical += Page)
        {
            bool free = true;
            for (uint i = 0; i < count; i++)
                if (regions[i].Backing != 0 && Overlap(physical, r.Bytes, regions[i].Backing, regions[i].Bytes)) { free = false; break; }
            if (free) { r.Backing = physical; r.Fresh = true; return true; }
        }
        return false;
    }

    private static bool Split(Region* regions, ref uint count, ulong address, ulong bytes, uint protection, bool remove, bool preserve = false)
    {
        Region* output = stackalloc Region[(int)WorkingRegions]; uint written = 0;
        for (uint i = 0; i < count; i++)
        {
            Region r = regions[i];
            if (!Overlap(r.Start, r.Bytes, address, bytes)) { if (!Append(output, ref written, r)) return false; continue; }
            if (!remove && !preserve && r.Protection == protection)
            { if (!Append(output, ref written, r)) return false; continue; }
            ulong start = r.Start > address ? r.Start : address;
            ulong end = r.Start + r.Bytes < address + bytes ? r.Start + r.Bytes : address + bytes;
            if (r.Start < start && !Append(output, ref written, Slice(r, r.Start, start - r.Start))) return false;
            if (!remove)
            {
                Region middle = Slice(r, start, end - start); if (!preserve) middle.Protection = protection;
                if (!Append(output, ref written, middle)) return false;
            }
            if (end < r.Start + r.Bytes && !Append(output, ref written, Slice(r, end, r.Start + r.Bytes - end))) return false;
        }
        count = written; for (uint i = 0; i < count; i++) regions[i] = output[i];
        return true;
    }

    private static Region Slice(Region r, ulong start, ulong bytes) => new()
    { Start = start, Bytes = bytes, Backing = r.Backing == 0 ? 0 : r.Backing + start - r.Start, Protection = r.Protection, Fresh = r.Fresh,
        FreshFrom = start - r.Start >= r.FreshFrom ? 0 : r.FreshFrom - (start - r.Start), NoDump = r.NoDump };
    private static bool Append(Region* regions, ref uint count, Region r)
    { if (count == WorkingRegions) return false; regions[count++] = r; return true; }
    private static uint CopyRegions(State* state, Region* output)
    {
        for (uint i = 0; i < state->Count; i++) output[i] = new() { Start = state->Regions[i * 4], Bytes = state->Regions[i * 4 + 1],
            Backing = state->Regions[i * 4 + 2], Protection = (uint)(state->Regions[i * 4 + 3] & ~0x100UL), NoDump = (state->Regions[i * 4 + 3] & 0x100) != 0 };
        return state->Count;
    }
    private static void Sort(Region* regions, uint count)
    {
        for (uint i = 1; i < count; i++)
        { Region r = regions[i]; uint j = i; while (j != 0 && regions[j - 1].Start > r.Start) { regions[j] = regions[j - 1]; j--; } regions[j] = r; }
    }
    private static uint Coalesce(Region* regions, uint count)
    {
        uint written = 0;
        for (uint i = 0; i < count; i++)
        {
            Region r = regions[i];
            if (written != 0)
            {
                Region* previous = regions + written - 1;
                if (previous->Start + previous->Bytes == r.Start && previous->Protection == r.Protection && previous->Fresh == r.Fresh &&
                    previous->FreshFrom == r.FreshFrom && previous->NoDump == r.NoDump &&
                    (previous->Backing == 0 && r.Backing == 0 || previous->Backing != 0 && previous->Backing + previous->Bytes == r.Backing))
                { previous->Bytes += r.Bytes; continue; }
            }
            regions[written++] = r;
        }
        return written;
    }
    private static bool Covered(Region* regions, uint count, ulong address, ulong bytes)
    {
        ulong cursor = address;
        for (uint i = 0; i < count && cursor < address + bytes; i++)
        { if (regions[i].Start > cursor) return false; if (regions[i].Start + regions[i].Bytes > cursor) cursor = regions[i].Start + regions[i].Bytes; }
        return cursor >= address + bytes;
    }
    private static bool Intersects(Region* regions, uint count, ulong address, ulong bytes)
    { for (uint i = 0; i < count; i++) if (Overlap(regions[i].Start, regions[i].Bytes, address, bytes)) return true; return false; }
    private static bool Protection(uint protection) => protection == 0 || protection == 1 || protection == 3 || protection == 5;
    private static bool Round(ulong bytes, out ulong rounded)
    { rounded = 0; if (bytes == 0 || bytes > X64UserAddressSpace.UserEnd - DynamicStart) return false; rounded = (bytes + 4095) & ~4095UL; return true; }
    private static bool Range(ulong address, ulong bytes) => address >= DynamicStart && (address & 4095) == 0 && bytes != 0 &&
        (bytes & 4095) == 0 && address < X64UserAddressSpace.UserEnd && bytes <= X64UserAddressSpace.UserEnd - address;
    private static bool Buffer(void* pointer, ulong bytes, ulong alignment) => pointer != null && ((ulong)pointer & (alignment - 1)) == 0 && (ulong)pointer <= ~0UL - bytes;
    private static bool Overlap(ulong a, ulong bytesA, ulong b, ulong bytesB) => a < b + bytesB && b < a + bytesA;
    private static void Clear(byte* bytes, ulong count) { for (ulong i = 0; i < count; i++) bytes[i] = 0; }
    private static void CopyTables(Admission memory)
    { for (uint i = 0; i < memory.TablePages * 512; i++) memory.Tables[i] = memory.CandidateTables[i]; }
}
