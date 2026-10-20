#if M16_008_PROBE
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Native callbacks borrow the authenticated IRQ-off process owner. Bulk
        // copies admit only complete retained kernel extents; CopyLinuxAlias
        // preflights every user page before copying. No scratch/user alias lives
        // across a handoff. The switch-stack usable extent admits CommitExit's
        // actual C# copyout local; guards, code and the IRQ stack stay excluded.
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxThreadCopyInput(void* context, ulong address, byte* destination, uint bytes)
        {
            LinuxNativeControl* c = LinuxNativeOwner(context);
            return c != null && LinuxThreadKernelRange(c, destination, bytes, false)
                ? CopyLinuxAlias(c->State, address, destination, bytes, false) : (byte)0;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxThreadCopyOutput(void* context, ulong address, byte* source, uint bytes)
        {
            LinuxNativeControl* c = LinuxNativeOwner(context);
            return c != null && LinuxThreadKernelRange(c, source, bytes, true)
                ? CopyLinuxAlias(c->State, address, source, bytes, true) : (byte)0;
        }

        // Ordinary copy callbacks retain the 8 KiB ceiling. The synchronous
        // message observer may explicitly borrow a complete 16 KiB read-only
        // metadata snapshot; the entire range must fit one owned extent.
        private static bool LinuxThreadKernelRange(LinuxNativeControl* c, byte* pointer, uint bytes, bool output,
            uint maximumBytes = 8192)
        {
            ulong address = (ulong)pointer;
            if (maximumBytes != 8192 && (maximumBytes != 16384 || output) ||
                pointer == null || bytes == 0 || bytes > maximumBytes || address > ulong.MaxValue - bytes) return false;
            Storage* state = c->State;
            if (DescriptorKernelRange(state, pointer, bytes, output) ||
                FirstExitOwnedArena.InMainStack(state->Arena, address, bytes)) return true;
            ulong pool = state->CooperativePool;
            if (LinuxThreadContains(pool + LinuxThreadProgram.MetadataPage * Page, LinuxThreadProgram.MetadataPages * Page, address, bytes)) return true;
            if (LinuxThreadContains(pool + (LinuxThreadProgram.SwitchGuardPage + 1) * Page, (LinuxThreadProgram.SwitchPages - 2) * Page, address, bytes)) return true;
            for (uint worker = 0; worker < LinuxNativeWorkers; worker++)
                if (LinuxThreadContains(pool + (LinuxThreadProgram.WorkerPage + 1 + worker * LinuxThreadProgram.WorkerPages) * Page,
                    (LinuxThreadProgram.WorkerPages - 2) * Page, address, bytes)) return true;
            return false;
        }

        // Out parameters are distinct mapped native caller locals, including
        // switch/IRQ callback locals. Only actual readable USER leaves backed by
        // the image/pool may supply a supervisor RW/NX identity alias. Retained
        // cells own its lifetime; the mutation guard prevents remap/protect/
        // discard while references survive, including unconsumed completions.
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxThreadResolveFutex(void* context, ulong address, uint** alias, ulong* lifetime)
        {
            if (alias == null || lifetime == null) return 0;
            *alias = null; *lifetime = 0;
            LinuxNativeControl* c = LinuxNativeOwner(context);
            if (c == null || c->Cells == null || (address & 3) != 0 || address < Page ||
                address >= LinuxSyscallGateway.UserEnd || LinuxSyscallGateway.UserEnd - address < 4) return 0;
            Storage* state = c->State;
            if (!X64UserAddressSpace.TryReadLeaf((ulong*)state->LinuxUserTables, state->LinuxUserTables,
                X64UserAddressSpace.MaximumTables, address, out ulong raw) || (raw & 5) != 5) return 0;
            var leaf = X64PageTableEntry.TryDecode(X64PageTableLevel.Pte, raw & ~4UL, state->Memory.Tables.Preconditions);
            if (!leaf.Succeeded || leaf.Kind != X64PageTableEntryKind.Leaf || leaf.PageSizeBytes != Page ||
                leaf.CachePolicy != PageCachePolicy.WriteBack ||
                leaf.Protection != PageProtection.Read && leaf.Protection != (PageProtection.Read | PageProtection.Write) &&
                    leaf.Protection != (PageProtection.Read | PageProtection.Execute) ||
                leaf.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong physical) != AddressValueStatus.Success) return 0;
            physical += address & (Page - 1);
            if ((!LinuxThreadContains(state->LinuxImageBacking, state->LinuxImagePages * Page, physical, 4) &&
                !LinuxThreadContains(state->LinuxMemoryPool, LinuxMemoryPoolPages(state) * Page, physical, 4)) ||
                !LinuxThreadIdentityAlias(state, physical & ~(Page - 1))) return 0;
            uint* resolved = (uint*)physical; ulong retained = 0;
            for (uint i = 0; i < LinuxNativeFutexes; i++)
            {
                var cell = c->Cells[i];
                if (!cell.Binding.IsValid || cell.Address != address) continue;
                if (cell.Alias != resolved || cell.Lifetime == 0 || retained != 0 && retained != cell.Lifetime) return 0;
                retained = cell.Lifetime;
            }
            if (retained == 0)
            {
                if (c->AliasSerial == ulong.MaxValue) return 0;
                retained = ++c->AliasSerial;
            }
            *alias = resolved; *lifetime = retained; return 1;
        }

        // Walk only the actual retained kernel table arena. Strict decoding
        // rejects USER, large, foreign, non-WB or non-RW/NX identity leaves.
        private static bool LinuxThreadIdentityAlias(Storage* state, ulong address)
        {
            var memory = state->Memory; ulong root = (ulong)memory.Entries;
            if (root != state->LinuxDispatchRoot || root < Page || (root & (Page - 1)) != 0 ||
                root > X64UserAddressSpace.PhysicalEnd - TablePages * Page || memory.Tables.ArenaPageCount != TablePages ||
                memory.EntryCapacity != TablePages * 512 ||
                memory.Tables.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong declared) != AddressValueStatus.Success || declared != root) return false;
            ulong table = root, visited = 0;
            for (int level = 4; level >= 1; level--)
            {
                ulong page = (table - root) / Page, bit = 1UL << (int)page;
                if ((visited & bit) != 0) return false;
                visited |= bit;
                ulong raw = memory.Entries[page * 512 + ((address >> (12 + 9 * (level - 1))) & 511)];
                var entry = X64PageTableEntry.TryDecode((X64PageTableLevel)level, raw, memory.Tables.Preconditions);
                if (!entry.Succeeded || entry.CachePolicy != PageCachePolicy.WriteBack ||
                    entry.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong physical) != AddressValueStatus.Success) return false;
                if (level == 1) return entry.Kind == X64PageTableEntryKind.Leaf && entry.PageSizeBytes == Page &&
                    entry.Protection == (PageProtection.Read | PageProtection.Write) && physical == address;
                if (entry.Kind != X64PageTableEntryKind.TablePointer || physical < root || physical - root >= TablePages * Page ||
                    (physical & (Page - 1)) != 0) return false;
                table = physical;
            }
            return false;
        }

        // Use the owned HPET counter, not IRQ epochs. Quotient/remainder avoids
        // ticks*period overflow and retains the exact floor in nanoseconds.
        // Clock observations may advance on failure; scheduler time does not.
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxThreadReadNanoseconds(void* context, ulong* time)
        {
            if (time == null) return 0;
            *time = 0;
            LinuxNativeControl* c = LinuxNativeOwner(context);
            if (c == null) return 0;
            ulong ticks = 0;
            delegate* unmanaged<void*, ulong*, byte> read = &ReadLinuxClock;
            if (read(context, &ticks) != 1) return 0;
            uint period = c->State->Clock.PeriodFemtoseconds;
            const ulong femtosecondsPerNanosecond = 1_000_000, limit = long.MaxValue;
            ulong whole = ticks / femtosecondsPerNanosecond;
            if (period == 0 || whole > limit / period) return 0;
            ulong nanos = whole * period, fraction = ticks % femtosecondsPerNanosecond * period / femtosecondsPerNanosecond;
            if (fraction > limit - nanos) return 0;
            nanos += fraction;
            if (nanos < c->Threads.ObservedTicks) return 0;
            *time = nanos; return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxThreadMemoryGuard(void* context, ulong address, ulong bytes)
        {
            LinuxNativeControl* c = LinuxNativeOwner(context);
            return c != null && c->Process.MayChangeUserRange(c->Key, 2, address, bytes) ? (byte)1 : (byte)0;
        }

        private static bool LinuxThreadContains(ulong start, ulong bytes, ulong address, ulong count) =>
            bytes != 0 && count != 0 && address >= start && address - start < bytes && count <= bytes - (address - start);
    }
}
#endif
