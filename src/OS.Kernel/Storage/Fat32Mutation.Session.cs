using System;
using OS.Std.Binary;
using OS.Std.Hash;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Storage;

public static unsafe partial class Fat32Mutation
{
    /// <summary>A caller-owned, noncopyable-by-contract planning/transaction
    /// scope. Start borrows the ENTIRE stable raw-image snapshot for this host
    /// preparation path, not a synchronous live-disk reader. No whole-file
    /// preload API or runtime mount is introduced. Changes and both byte buffers
    /// are exclusive until successful commit/rollback. Never discard a dirty
    /// scope, its journal, or an uncertain block/DMA owner.</summary>
    public ref partial struct Session
    {
        private Span<byte> image, before, after;
        private Span<Change> changes;
        private Span<uint> scratch;
        private LiveFat32Volume volume;
        private Block.State* block;
        private ulong owner, epoch, generation;
        private int count, cursor, phase, issuedIndex;
        private uint work, budget;
        private bool initialized, planned, dirty, pending, flushing, recovering, cancelled, issued, flushOnly, allocationChanged;
        private Block.RequestToken request;
        private Block.Status blockFailure;
        private Status failure;
        public readonly bool Initialized => initialized;
        public readonly bool Dirty => dirty;
        public readonly bool HasPlan => planned;
        public readonly bool RecoveryNeeded => failure == Status.RecoveryRequired;
        public readonly int ChangeCount => count;
        public readonly ulong Generation => generation;
        public readonly Status LastFailure => failure;
        public readonly Block.Status LastBlockFailure => blockFailure;

        /// <summary>Copy journal framing and CRC while buffers remain borrowed
        /// and unchanged. The caller must retain all named bytes, not just this
        /// receipt; this does not persist them or acknowledge any device I/O.</summary>
        public Status InspectJournal(out JournalReceipt receipt)
        {
            receipt = default;
            if (!initialized || !planned || count == 0) return Status.InvalidJournal;
            receipt = new(owner, epoch, generation, count, JournalCrc(count, generation)); return Status.Success;
        }

        /// <summary>Reopen separately retained before/after buffers after an
        /// uncertain operation. Start must first authenticate the same raw
        /// volume/snapshot identity and a quiesced fresh block owner. CRC detects
        /// accidental substitution; it is not authenticity or persistence proof.
        /// The scope remains unusable until all rollback writes and FLUSH pass.</summary>
        public Status ResumeRecovery(JournalReceipt receipt)
        {
            if (!Available() || receipt.Owner != owner || receipt.Epoch != epoch || receipt.Generation < generation ||
                receipt.Count is < 1 || receipt.Count > changes.Length)
                return Status.InvalidJournal;
            for (int i = 0; i < receipt.Count; i++) if (!ValidChange(changes[i])) return Status.InvalidJournal;
            if (JournalCrc(receipt.Count, receipt.Generation) != receipt.Crc32) return Status.InvalidJournal;
            count = receipt.Count; generation = receipt.Generation; planned = dirty = issued = true;
            failure = Status.RecoveryRequired; return Status.RecoveryRequired;
        }

        /// <summary>Validate supported mirrored512-byte FAT32 geometry through
        /// the existing numeric parser. Every backing span must be disjoint;
        /// metadata is bounded to4096 changes, traversal to the supplied positive
        /// budget<=1M, and each chain to4096 clusters. The caller authenticates
        /// the stable image/device identity and exclusive writable volume. This
        /// snapshot acknowledgement is not evidence of a physical disk read.
        /// Owner/Epoch must be a fresh externally assigned pair for each clean
        /// lifetime; reuse is only for restoring its authenticated journal.</summary>
        public Status Start(Span<byte> rawImage, ulong partitionFirst, ulong partitionLast,
            ulong ownerIdentity, ulong volumeEpoch, Block.State* device,
            Span<Change> journal, Span<byte> beforeImages, Span<byte> afterImages,
            Span<uint> clusterScratch, uint workBudget = 100000)
        {
            if (initialized || ownerIdentity == 0 || volumeEpoch == 0 || rawImage.Length % 512 != 0 ||
                rawImage.Length < 512 || journal.Length is < 1 or > MaximumChanges ||
                beforeImages.Length != journal.Length * 512 || afterImages.Length != beforeImages.Length ||
                clusterScratch.Length != ScratchClusters || workBudget is 0 or > MaximumWork ||
                partitionFirst >= (ulong)rawImage.Length / 512 || partitionLast >= (ulong)rawImage.Length / 512 ||
                !Storage(rawImage, journal, beforeImages, afterImages, clusterScratch, device)) return Status.InvalidStorage;
            if (device == null || !device->Running || device->RequestPending || device->Dirty ||
                device->SectorCount != (ulong)rawImage.Length / 512) return Status.BlockFailure;
            LiveFat32Volume parsed = default;
            if (Chain.ParseBpb(rawImage.Slice((int)partitionFirst * 512, 512), (ulong)rawImage.Length / 512,
                partitionFirst, partitionLast, BlockReadResult.Success(1, 512), ref parsed) != LiveFat32Status.Success)
                return Status.InvalidVolume;
            image = rawImage; changes = journal; before = beforeImages; after = afterImages; scratch = clusterScratch;
            volume = parsed; block = device; owner = ownerIdentity; epoch = volumeEpoch; generation = 1; budget = workBudget;
            initialized = true; failure = Status.Success; blockFailure = Block.Status.Success; return Status.Success;
        }

        /// <summary>Resolve a rooted path of at most8 components and copy its
        /// current file identity. Inputs/output are ordinary exclusive caller
        /// memory; no path or output reference is retained. Planning, dirty or
        /// recovering scopes reject; aliases and malformed metadata fail closed.</summary>
        public Status Open(ReadOnlySpan<char> path, out FileToken file)
        {
            file = default; if (!Available()) return Current(); work = 0;
            if (!Inputs(path, default)) return Status.InvalidStorage;
            Status result = Resolve(path, out Entry entry, out _, out _);
            if (result != Status.Success) return result;
            if ((entry.Attributes & 0x10) != 0) return Status.NotFile;
            file = new(owner, epoch, generation, entry.Offset, entry.Cluster, entry.Length); return Status.Success;
        }

        /// <summary>Plan creation with an explicit unique11-byte short alias.
        /// All allocations and directory growth are reserved in the journal
        /// before I/O; capacity failure publishes no partial allocation. Empty
        /// files have cluster0. Names/aliases are checked against both spellings.</summary>
        public Status Create(ReadOnlySpan<char> path, ReadOnlySpan<byte> alias, ReadOnlySpan<byte> data)
        {
            Status status = Begin(data); if (status != Status.Success) return status;
            if (!Inputs(path, alias)) return FinishPlan(Status.InvalidStorage);
            status = Parent(path, out uint directory, out ReadOnlySpan<char> name);
            if (status == Status.Success) status = Unique(directory, name, alias, ulong.MaxValue);
            if (status == Status.Success) status = ValidateTree(volume.RootCluster, ulong.MaxValue, 0, 0);
            uint first = 0;
            if (status == Status.Success) status = AllocateFile(data, out first);
            if (status == Status.Success) status = Insert(directory, name, alias, first, (uint)data.Length, default, out _);
            return FinishPlan(status, allocated: first != 0);
        }

        /// <summary>Plan overwrite/grow, zeroing all new allocation bytes and
        /// any gap before the enlarged entry is exposed. At most64KiB of caller
        /// data is borrowed only for this call; no caller data pointer is kept.</summary>
        public Status Write(FileToken file, ulong offset, ReadOnlySpan<byte> data)
        {
            Status status = Begin(data); if (status != Status.Success) return status;
            status = Token(file, out Entry entry);
            if (status != Status.Success) return FinishPlan(status);
            if (offset > uint.MaxValue || data.Length == 0 || (ulong)data.Length > uint.MaxValue - offset)
                return FinishPlan(Status.CapacityExceeded);
            uint length = Math.Max(entry.Length, (uint)(offset + (uint)data.Length));
            status = Resize(entry, length, offset, data);
            return FinishPlan(status, length != entry.Length);
        }

        /// <summary>Plan shrink or zero-filled growth. Shrink publishes the
        /// smaller entry before freeing any detached tail in both FAT copies.
        /// Retained file bytes are preserved; later regrowth never exposes the
        /// old tail. Equal length is a no-op with no synthetic FLUSH.</summary>
        public Status Truncate(FileToken file, uint length)
        {
            Status status = Begin(default); if (status != Status.Success) return status;
            status = Token(file, out Entry entry);
            if (status == Status.Success) status = Resize(entry, length, 0, default);
            return FinishPlan(status, status == Status.Success && entry.Length != length);
        }

        /// <summary>Rename a regular file without replacing a destination.
        /// Destination publication precedes source tombstones; an interrupted
        /// rename can temporarily have two names and requires journal recovery.
        /// No atomic cross-sector rename or directory-move promise is made.</summary>
        public Status Rename(FileToken file, ReadOnlySpan<char> destination, ReadOnlySpan<byte> alias)
        {
            Status status = Begin(default); if (status != Status.Success) return status;
            if (!Inputs(destination, alias)) return FinishPlan(Status.InvalidStorage);
            status = Token(file, out Entry entry);
            uint directory = 0; ReadOnlySpan<char> name = default;
            if (status == Status.Success) status = FileChain(entry, out _);
            if (status == Status.Success) status = Parent(destination, out directory, out name);
            if (status == Status.Success) status = Unique(directory, name, alias, ulong.MaxValue);
            if (status == Status.Success) status = Insert(directory, name, alias, entry.Cluster, entry.Length,
                image.Slice((int)entry.Offset, 32), out _);
            if (status == Status.Success) status = Tombstone(entry);
            return FinishPlan(status);
        }

        /// <summary>Plan scrubbed E5 tombstones for the entire owned LFN/short
        /// run, preserving later entries and the end marker, then free the data
        /// chain after directory FLUSH. Directories and readonly files reject.</summary>
        public Status Delete(FileToken file)
        {
            Status status = Begin(default); if (status != Status.Success) return status;
            status = Token(file, out Entry entry);
            int clusters = 0;
            if (status == Status.Success) status = FileChain(entry, out clusters);
            if (status == Status.Success) status = Tombstone(entry);
            for (int i = 0; status == Status.Success && i < clusters; i++) status = SetFat(scratch[MaximumClusters + i], 0, Stage.ReleaseFat);
            return FinishPlan(status, clusters != 0);
        }

        /// <summary>Validate the original generation BEFORE any device action.
        /// This explicit flush cannot make a stale unlinked handle update a newly
        /// reused entry. A clean flush still requires a real FLUSH completion.</summary>
        public Status Flush(FileToken file)
        {
            Status status = Begin(default); if (status != Status.Success) return status;
            status = Token(file, out _);
            if (status != Status.Success) return FinishPlan(status);
            planned = true; flushOnly = true; phase = 6; return Status.Planned;
        }

        /// <summary>At most one submit or one poll per call. Each nonempty stage
        /// ends with an actual FLUSH before the next metadata publication stage.
        /// Dirty is retained through all mirrors, directory and advisory FSInfo
        /// writes and final FLUSH. Only successful flush completion publishes
        /// journal after-images into the logical snapshot. No synchronous spin.</summary>
        public Status Pump()
        {
            if (!initialized) return Status.Uninitialized;
            if (failure == Status.RecoveryRequired) return Status.RecoveryRequired;
            if (!planned) return Status.Success;
            if (pending)
            {
                Block.Completion completion = default;
                var result = Block.Poll(block, &completion);
                if (result == Block.Status.Pending) return Status.Pending;
                if (result is not (Block.Status.Success or Block.Status.Cancelled)) return Fail(result);
                if (completion.Token.Owner != request.Owner || completion.Token.Epoch != request.Epoch ||
                    completion.Token.Generation != request.Generation || completion.Kind != (flushing ? Block.Operation.Flush : Block.Operation.Write) ||
                    (!flushing && completion.Sector != changes[issuedIndex].Sector)) return Fail(Block.Status.InvalidCompletion);
                pending = false;
                if (completion.DeviceResult != Block.Status.Success || result != Block.Status.Success || cancelled)
                    return Fail(result == Block.Status.Success ? Block.Status.Cancelled : result);
                if (flushing)
                {
                    if (recovering) { CompleteSnapshot(true); return Status.Success; }
                    phase++; cursor = 0;
                    if (phase > 6) { CompleteSnapshot(false); return Status.Success; }
                }
                return Status.Pending;
            }
            if (cancelled) return issued ? Fail(Block.Status.Cancelled) : Discard();
            if (block == null || !block->Running || block->RequestPending) return Fail(Block.Status.InvalidState);
            if (recovering)
            {
                if (cursor != 0)
                {
                    int index = --cursor;
                    return Submit(index, before.Slice(index * 512, 512), false);
                }
                return Submit(-1, default, true);
            }
            while (phase <= 6)
            {
                for (; cursor < count; cursor++)
                    if ((int)changes[cursor].Phase == phase)
                    {
                        int index = cursor++;
                        if (!image.Slice((int)changes[index].Sector * 512, 512).SequenceEqual(before.Slice(index * 512, 512)))
                            return issued ? Fail(Block.Status.InvalidState, Status.SnapshotChanged) : Abandon(Status.SnapshotChanged);
                        return Submit(index, after.Slice(index * 512, 512), false);
                    }
                bool hasPhase = flushOnly;
                for (int i = 0; i < count && !hasPhase; i++) hasPhase = (int)changes[i].Phase == phase;
                if (hasPhase) return Submit(-1, default, true);
                phase++; cursor = 0;
            }
            CompleteSnapshot(false); return Status.Success;
        }

        /// <summary>Discard before first issue; otherwise revoke continuation,
        /// drain/cancel the actual outstanding token and require explicit
        /// rollback. Cancellation never retracts DMA or cleans a partial write.</summary>
        public Status Cancel()
        {
            if (!initialized) return Status.Uninitialized;
            if (failure == Status.RecoveryRequired) return Status.RecoveryRequired;
            if (!planned) return Status.Success;
            cancelled = true;
            if (pending)
            {
                var result = Block.Cancel(block, request);
                if (result != Block.Status.Success) return Fail(result);
                return Status.Pending;
            }
            return issued ? Fail(Block.Status.Cancelled) : Discard();
        }

        /// <summary>Idempotently restore every original sector then FLUSH.
        /// A faulted device's DMA owner must first be successfully retired; only
        /// then may a fresh exclusive session be supplied. Failed recovery keeps
        /// all journal bytes and permits another explicit recovery attempt.
        /// Recovery authenticates local journal bounds, not persistent provenance.</summary>
        public Status Recover(Block.State* replacement)
        {
            if (!initialized || failure != Status.RecoveryRequired || pending || !dirty) return Status.InvalidJournal;
            if (!Storage(image, changes, before, after, scratch, replacement) || !replacement->Running || replacement->RequestPending ||
                replacement->SectorCount != (ulong)image.Length / 512 ||
                (replacement != block && (block == null || !block->Retired))) return Status.BlockFailure;
            for (int i = 0; i < count; i++) if (!ValidChange(changes[i])) return Status.InvalidJournal;
            block = replacement; failure = Status.Success; recovering = true; cancelled = false;
            if (generation != ulong.MaxValue) generation++;
            cursor = count; planned = true; return Status.Planned;
        }

        private Status Submit(int index, ReadOnlySpan<byte> bytes, bool flush)
        {
            if (!issued && generation == ulong.MaxValue) return Abandon(Status.GenerationExhausted);
            Block.RequestToken token = default;
            var result = flush ? Block.SubmitFlush(block, &token) : Block.SubmitWrite(block, changes[index].Sector, bytes, &token);
            // A failing notify can follow actual publication; preserve recovery
            // state even when Submit returns an error and no token is published.
            if (result != Block.Status.Success)
            {
                if (!issued && block->Submitted != 0 && block->LastFailure != Block.Status.Success) { generation++; issued = true; }
                return Fail(result);
            }
            if (!issued) { generation++; issued = true; }
            dirty = true; request = token; pending = true; flushing = flush; issuedIndex = index;
            return Status.Pending;
        }

        private Status Fail(Block.Status result, Status status = Status.BlockFailure)
        {
            blockFailure = blockFailure == Block.Status.Success ? result : blockFailure;
            failure = Status.RecoveryRequired; dirty = true; planned = true; pending = false;
            return Status.RecoveryRequired;
        }
        private Status Discard() { planned = false; count = 0; cancelled = false; failure = Status.Success; return Status.Cancelled; }
        private Status Abandon(Status status) { planned = false; count = 0; failure = Status.Success; return status; }
        private void CompleteSnapshot(bool rollback)
        {
            for (int stage = 1; stage <= 6; stage++)
                for (int i = 0; i < count; i++)
                    if ((int)changes[i].Phase == stage)
                        (rollback ? before : after).Slice(i * 512, 512).CopyTo(image.Slice((int)changes[i].Sector * 512, 512));
            planned = dirty = pending = recovering = cancelled = issued = flushing = flushOnly = false;
            failure = Status.Success; blockFailure = Block.Status.Success; count = 0;
        }

        private bool Available() => initialized && !planned && !dirty;
        private Status Current() => !initialized ? Status.Uninitialized : failure == Status.RecoveryRequired ? Status.RecoveryRequired : Status.Busy;
        private Status Begin(ReadOnlySpan<byte> data)
        {
            if (!Available()) return Current();
            if (generation == ulong.MaxValue) return Status.GenerationExhausted;
            if (data.Length > MaximumWriteBytes || data.Overlaps(image) || data.Overlaps(before) || data.Overlaps(after) || !DataStorage(data)) return Status.InvalidStorage;
            count = cursor = 0; phase = 1; work = 0; blockFailure = Block.Status.Success; failure = Status.Success;
            cancelled = recovering = issued = flushOnly = allocationChanged = false; return Status.Success;
        }
        private Status FinishPlan(Status status, bool allocated = false)
        {
            if (status == Status.Success && (allocated || allocationChanged)) status = PlanFsInfo();
            if (status != Status.Success) { count = 0; planned = false; return status; }
            planned = count != 0; return planned ? Status.Planned : Status.Success;
        }

        private Status Token(FileToken token, out Entry entry)
        {
            entry = default;
            if (token.Owner != owner || token.Epoch != epoch || token.Generation != generation ||
                token.EntryOffset > (ulong)image.Length - 32 || (token.EntryOffset & 31) != 0 ||
                token.EntryOffset / 512 < volume.FirstDataLba) return Status.StaleToken;
            Status result = FindPhysical(token.EntryOffset, out entry);
            if (result != Status.Success || entry.Cluster != token.FirstCluster || entry.Length != token.Length) return Status.StaleToken;
            return (entry.Attributes & 1) != 0 ? Status.ReadOnly : (entry.Attributes & 0x10) != 0 ? Status.NotFile : Status.Success;
        }
        private Status FindPhysical(ulong offset, out Entry entry)
        {
            // Reachability is authenticated by the current token generation;
            // Start never accepts externally manufactured entry identities.
            entry = default;
            Span<byte> bytes = image.Slice((int)offset, 32);
            if (bytes[0] is 0 or 0xe5 || bytes[11] == 15 || (bytes[11] & 0x18) != 0) return Status.StaleToken;
            // Recover the preceding LFN run across a noncontiguous directory by
            // bounded traversal from the root, rather than physical subtraction.
            return FindOffset(volume.RootCluster, offset, 0, out entry);
        }

        private Status Parent(ReadOnlySpan<char> path, out uint directory, out ReadOnlySpan<char> name)
        {
            directory = 0; name = default;
            if (path.Length < 2 || path[0] != '/' || path[^1] == '/') return Status.InvalidName;
            int slash = path.LastIndexOf('/'); name = path[(slash + 1)..];
            if (!Name(name)) return Status.InvalidName;
            if (slash == 0) { directory = volume.RootCluster; return Status.Success; }
            Status result = Resolve(path[..slash], out Entry entry, out _, out _);
            if (result != Status.Success) return result;
            if ((entry.Attributes & 0x10) == 0) return Status.CorruptDirectory;
            if ((entry.Attributes & 1) != 0) return Status.ReadOnly;
            directory = entry.Cluster; return Status.Success;
        }
        private Status Resolve(ReadOnlySpan<char> path, out Entry entry, out uint parent, out int depth)
        {
            entry = default; parent = volume.RootCluster; depth = 0;
            if (path.Length < 2 || path[0] != '/' || path[^1] == '/') return Status.InvalidName;
            int start = 1;
            while (start < path.Length)
            {
                if (++depth > 8) return Status.TraversalBudgetExceeded;
                int length = path[start..].IndexOf('/'); if (length < 0) length = path.Length - start;
                ReadOnlySpan<char> component = path.Slice(start, length);
                if (!Name(component)) return Status.InvalidName;
                Status result = Search(parent, component, default, ulong.MaxValue, false, out entry);
                if (result != Status.Success) return result;
                start += length + 1;
                if (start < path.Length)
                {
                    if ((entry.Attributes & 0x10) == 0) return Status.NotFound;
                    parent = entry.Cluster;
                }
            }
            return Status.Success;
        }

        private Status Unique(uint directory, ReadOnlySpan<char> name, ReadOnlySpan<byte> alias, ulong excluded)
        {
            if (alias.Length != 11 || !Alias(alias)) return Status.InvalidAlias;
            Status result = Search(directory, name, alias, excluded, true, out _);
            return result == Status.NotFound ? Status.Success : result == Status.Success ? Status.AlreadyExists : result;
        }
        private Status Search(uint directory, ReadOnlySpan<char> wanted, ReadOnlySpan<byte> alias, ulong excluded, bool collision, out Entry found)
        {
            found = default; Status status = DirectoryChain(directory, out int clusters); if (status != Status.Success) return status;
            Span<ushort> words = stackalloc ushort[260]; Span<char> actual = stackalloc char[260]; LiveVfatState state = default; int start = 0;
            int slots = clusters * (int)volume.SectorsPerCluster * 16;
            for (int slot = 0; slot < slots; slot++)
            {
                if (!Work()) return Status.TraversalBudgetExceeded;
                ulong offset = DirectoryOffset(slot); Span<byte> raw = View(offset / 512).Slice((int)(offset % 512), 32);
                if (state.Slots == 0) start = slot;
                var result = Vfat.Consume(raw, ref state, words, out var parsed);
                if (result == LiveDirectoryStatus.End) return Status.NotFound;
                if (result is LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName) continue;
                if (result != LiveDirectoryStatus.Entry) return Status.CorruptDirectory;
                int length = 0;
                if (parsed.NameLength != 0) { length = parsed.NameLength; for (int i = 0; i < length; i++) actual[i] = (char)words[i]; }
                else length = ShortName(raw, actual);
                bool equal = Equal(actual[..length], wanted);
                int shortLength = ShortName(raw, actual); equal |= Equal(actual[..shortLength], wanted);
                if (collision && alias.Length == 11) equal |= EqualAlias(raw[..11], alias) || AliasNameEquals(actual[..shortLength], wanted) || LongAliasEquals(words, parsed.NameLength, alias);
                if (offset != excluded && equal)
                {
                    found = new() { Offset = offset, Cluster = parsed.FirstCluster, Length = parsed.ByteCount, Attributes = parsed.Attributes,
                        Directory = directory, StartSlot = start, Slots = slot - start + 1 };
                    return Status.Success;
                }
            }
            return Vfat.Finish(state) == LiveDirectoryStatus.End ? Status.NotFound : Status.CorruptDirectory;
        }
        private Status FindOffset(uint directory, ulong wanted, int depth, out Entry found)
        {
            found = default; if (depth > 8) return Status.TraversalBudgetExceeded;
            // Recursive descent cannot retain the shared directory chain. Save
            // only a child's cluster and restart at the next logical slot.
            int resume = 0;
            Span<ushort> words = stackalloc ushort[260];
            while (true)
            {
                Status status = DirectoryChain(directory, out int clusters); if (status != Status.Success) return status;
                LiveVfatState state = default; int start = 0; uint child = 0;
                int slots = clusters * (int)volume.SectorsPerCluster * 16;
                for (int slot = 0; slot < slots; slot++)
                {
                    if (!Work()) return Status.TraversalBudgetExceeded;
                    ulong offset = DirectoryOffset(slot); var raw = View(offset / 512).Slice((int)(offset % 512), 32);
                    if (state.Slots == 0) start = slot;
                    var parsed = Vfat.Consume(raw, ref state, words, out var entry);
                    if (parsed == LiveDirectoryStatus.End) break;
                    if (parsed is LiveDirectoryStatus.LongName or LiveDirectoryStatus.Skipped) continue;
                    if (parsed != LiveDirectoryStatus.Entry) return Status.CorruptDirectory;
                    if (offset == wanted)
                    {
                        found = new() { Offset = offset, Directory = directory, StartSlot = start, Slots = slot - start + 1,
                            Cluster = entry.FirstCluster, Length = entry.ByteCount, Attributes = entry.Attributes }; return Status.Success;
                    }
                    if (slot >= resume && (entry.Attributes & 0x10) != 0) { child = entry.FirstCluster; resume = slot + 1; break; }
                }
                if (child == 0) return Status.NotFound;
                status = FindOffset(child, wanted, depth + 1, out found);
                if (status != Status.NotFound) return status;
            }
        }

        private Status DirectoryChain(uint first, out int count) => GetChain(first, scratch[..MaximumClusters], out count);
        private Status FileChain(Entry entry, out int count)
        {
            count = 0;
            if (entry.Length == 0) return entry.Cluster == 0 ? ValidateTree(volume.RootCluster, entry.Offset, 0, 0) : Status.CorruptChain;
            Status status = GetChain(entry.Cluster, scratch.Slice(MaximumClusters, MaximumClusters), out count);
            if (status != Status.Success) return status;
            if ((ulong)count != ((ulong)entry.Length + volume.SectorsPerCluster * 512 - 1) / (volume.SectorsPerCluster * 512)) return Status.CorruptChain;
            return ValidateTree(volume.RootCluster, entry.Offset, count, 0);
        }
        // Before releasing or overwriting target clusters, reject any reachable
        // file/directory alias to them. Creation also rejects dangling chains
        // that would otherwise let a "free" cluster overwrite a live entry.
        // Every traversal and intersection comparison consumes the same budget.
        private Status ValidateTree(uint directory, ulong target, int targetClusters, int depth)
        {
            if (depth > 8) return Status.TraversalBudgetExceeded;
            int resume = 0; Span<ushort> words = stackalloc ushort[260];
            while (true)
            {
                Status status = DirectoryChain(directory, out int clusters); if (status != Status.Success) return status;
                status = Disjoint(scratch[..clusters], targetClusters); if (status != Status.Success) return status;
                LiveVfatState state = default; uint child = 0; int capacity = clusters * (int)volume.SectorsPerCluster * 16;
                for (int slot = 0; slot < capacity; slot++)
                {
                    if (!Work()) return Status.TraversalBudgetExceeded;
                    ulong offset = DirectoryOffset(slot); var raw = View(offset / 512).Slice((int)(offset % 512), 32);
                    var parsed = Vfat.Consume(raw, ref state, words, out var entry);
                    if (parsed == LiveDirectoryStatus.End) break;
                    if (parsed is LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName) continue;
                    if (parsed != LiveDirectoryStatus.Entry) return Status.CorruptDirectory;
                    if (slot < resume) continue;
                    if ((entry.Attributes & 0x10) != 0) { child = entry.FirstCluster; resume = slot + 1; break; }
                    if (offset == target) continue;
                    if (entry.ByteCount == 0) { if (entry.FirstCluster != 0) return Status.CorruptChain; continue; }
                    status = GetChain(entry.FirstCluster, scratch.Slice(MaximumClusters * 2, MaximumClusters), out int fileClusters);
                    if (status != Status.Success) return status;
                    if ((ulong)fileClusters != ((ulong)entry.ByteCount + volume.SectorsPerCluster * 512 - 1) / (volume.SectorsPerCluster * 512)) return Status.CorruptChain;
                    status = Disjoint(scratch.Slice(MaximumClusters * 2, fileClusters), targetClusters); if (status != Status.Success) return status;
                }
                if (child == 0) return Vfat.Finish(state) == LiveDirectoryStatus.End ? Status.Success : Status.CorruptDirectory;
                status = ValidateTree(child, target, targetClusters, depth + 1); if (status != Status.Success) return status;
            }
        }
        private Status Disjoint(ReadOnlySpan<uint> other, int targetClusters)
        {
            for (int i = 0; i < other.Length; i++) for (int j = 0; j < targetClusters; j++)
            {
                if (!Work()) return Status.TraversalBudgetExceeded;
                if (other[i] == scratch[MaximumClusters + j]) return Status.CorruptChain;
            }
            return Status.Success;
        }
        private Status GetChain(uint first, Span<uint> output, out int count)
        {
            count = 0; uint cluster = first;
            while (true)
            {
                if (!Work()) return Status.TraversalBudgetExceeded;
                if (!Cluster(cluster)) return Status.CorruptChain;
                if (count == output.Length) return Status.CapacityExceeded;
                for (int i = 0; i < count; i++) if (output[i] == cluster) return Status.CorruptChain;
                output[count++] = cluster;
                Status status = Fat(cluster, out uint next); if (status != Status.Success) return status;
                if (next >= 0x0ffffff8) return Status.Success;
                if (!Cluster(next)) return Status.CorruptChain; cluster = next;
            }
        }
        private Status Fat(uint cluster, out uint next)
        {
            next = 0; if (!Cluster(cluster)) return Status.CorruptChain;
            ulong sector = volume.FirstFatLba + cluster / 128; int offset = (int)(cluster % 128) * 4;
            var first = View(sector); var second = View(sector + volume.FatSectors);
            if (!first.SequenceEqual(second)) return Status.MirrorMismatch;
            next = Get32(first, offset) & 0x0fffffff; return Status.Success;
        }
        private Status SetFat(uint cluster, uint value, Stage stage)
        {
            Status status = Fat(cluster, out _); if (status != Status.Success) return status;
            ulong sector = volume.FirstFatLba + cluster / 128; int offset = (int)(cluster % 128) * 4;
            status = Edit(sector, stage, out int first); if (status != Status.Success) return status;
            status = Edit(sector + volume.FatSectors, stage, out int second); if (status != Status.Success) return status;
            Put32(after.Slice(first * 512, 512), offset, (Get32(after.Slice(first * 512, 512), offset) & 0xf0000000) | value);
            Put32(after.Slice(second * 512, 512), offset, (Get32(after.Slice(second * 512, 512), offset) & 0xf0000000) | value);
            return Status.Success;
        }
        private Status Allocate(int requested, Span<uint> allocated, out int count)
        {
            count = 0; if (requested < 0 || requested > allocated.Length) return Status.CapacityExceeded;
            for (uint cluster = 2; count < requested && (ulong)cluster < (ulong)volume.ClusterCount + 2; cluster++)
            {
                if (!Work()) return Status.TraversalBudgetExceeded;
                Status status = Fat(cluster, out uint next); if (status != Status.Success) return status;
                if (next != 0) continue;
                status = SetFat(cluster, 0x0fffffff, Stage.AllocateFat); if (status != Status.Success) return status;
                allocated[count++] = cluster;
                allocationChanged = true;
                ulong first = ClusterLba(cluster);
                for (uint sector = 0; sector < volume.SectorsPerCluster; sector++)
                {
                    status = Edit(first + sector, Stage.Data, out int index); if (status != Status.Success) return status;
                    after.Slice(index * 512, 512).Clear();
                }
            }
            if (count != requested) return Status.DiskFull;
            for (int i = 0; i + 1 < count; i++)
            { Status status = SetFat(allocated[i], allocated[i + 1], Stage.AllocateFat); if (status != Status.Success) return status; }
            return Status.Success;
        }
        private Status AllocateFile(ReadOnlySpan<byte> data, out uint first)
        {
            first = 0; int needed = (int)(((ulong)data.Length + volume.SectorsPerCluster * 512 - 1) / (volume.SectorsPerCluster * 512));
            Status status = Allocate(needed, scratch.Slice(MaximumClusters * 2, MaximumClusters), out int actual);
            if (status != Status.Success) return status;
            if (actual != 0) first = scratch[MaximumClusters * 2];
            return WriteRange(scratch.Slice(MaximumClusters * 2, actual), 0, data, false, 0);
        }
        private Status Resize(Entry entry, uint length, ulong offset, ReadOnlySpan<byte> data)
        {
            Status status = FileChain(entry, out int oldCount); if (status != Status.Success) return status;
            int required = (int)(((ulong)length + volume.SectorsPerCluster * 512 - 1) / (volume.SectorsPerCluster * 512));
            if (required > MaximumClusters) return Status.CapacityExceeded;
            Span<uint> newChain = scratch.Slice(MaximumClusters * 2, MaximumClusters);
            int keep = Math.Min(required, oldCount); scratch.Slice(MaximumClusters, keep).CopyTo(newChain);
            if (required > oldCount)
            {
                status = Allocate(required - oldCount, newChain[oldCount..], out int added); if (status != Status.Success) return status;
                if (oldCount != 0) { status = SetFat(newChain[oldCount - 1], newChain[oldCount], Stage.AllocateFat); if (status != Status.Success) return status; }
                if (added != required - oldCount) return Status.CorruptChain;
            }
            if (length > entry.Length)
            { status = WriteRange(newChain[..required], entry.Length, default, true, length - entry.Length); if (status != Status.Success) return status; }
            if (data.Length != 0)
            { status = WriteRange(newChain[..required], offset, data, false, 0); if (status != Status.Success) return status; }
            if (length != entry.Length)
            {
                status = Edit(entry.Offset / 512, Stage.PublishDirectory, out int index); if (status != Status.Success) return status;
                var raw = after.Slice(index * 512 + (int)(entry.Offset % 512), 32);
                uint first = required == 0 ? 0 : newChain[0]; Put16(raw, 20, (ushort)(first >> 16)); Put16(raw, 26, (ushort)first); Put32(raw, 28, length);
            }
            if (required < oldCount)
            {
                if (required != 0) { status = SetFat(newChain[required - 1], 0x0fffffff, Stage.ReleaseFat); if (status != Status.Success) return status; }
                for (int i = required; i < oldCount; i++)
                { status = SetFat(scratch[MaximumClusters + i], 0, Stage.ReleaseFat); if (status != Status.Success) return status; }
            }
            return Status.Success;
        }
        private Status WriteRange(ReadOnlySpan<uint> chain, ulong offset, ReadOnlySpan<byte> data, bool zero, uint zeroBytes)
        {
            ulong bytes = zero ? zeroBytes : (uint)data.Length, done = 0, clusterBytes = volume.SectorsPerCluster * 512UL;
            if (offset > (ulong)chain.Length * clusterBytes || bytes > (ulong)chain.Length * clusterBytes - offset) return Status.CapacityExceeded;
            while (done < bytes)
            {
                if (!Work()) return Status.TraversalBudgetExceeded;
                ulong logical = offset + done, sector = ClusterLba(chain[(int)(logical / clusterBytes)]) + logical % clusterBytes / 512;
                int inSector = (int)(logical % 512), take = (int)Math.Min(512UL - (uint)inSector, bytes - done);
                Status status = Edit(sector, Stage.Data, out int index); if (status != Status.Success) return status;
                var target = after.Slice(index * 512 + inSector, take);
                if (zero) target.Clear(); else data.Slice((int)done, take).CopyTo(target);
                done += (uint)take;
            }
            return Status.Success;
        }

        private Status Insert(uint directory, ReadOnlySpan<char> name, ReadOnlySpan<byte> alias, uint first, uint length,
            ReadOnlySpan<byte> template, out ulong shortOffset)
        {
            shortOffset = 0; Status status = DirectoryChain(directory, out int clusters); if (status != Status.Success) return status;
            int needed = (name.Length + 12) / 13 + 1;
            if (needed is < 2 or > 21) return Status.InvalidName;
            int capacity = clusters * (int)volume.SectorsPerCluster * 16, run = 0, start = 0;
            bool consumedTerminator = false, ended = false;
            int slot;
            for (slot = 0; slot < capacity; slot++)
            {
                if (!Work()) return Status.TraversalBudgetExceeded;
                ulong offset = DirectoryOffset(slot); byte marker = View(offset / 512)[(int)(offset % 512)];
                if (marker == 0) ended = true;
                if (ended || marker == 0xe5)
                {
                    if (run == 0) start = slot;
                    run++; consumedTerminator |= ended;
                    if (run == needed && (!consumedTerminator || slot + 1 < capacity)) break;
                }
                else { run = 0; consumedTerminator = false; }
            }
            if (slot == capacity)
            {
                if (run == 0) start = capacity;
                int missing = needed + 1 - run; // Include the restored end marker.
                int extra = (missing + (int)volume.SectorsPerCluster * 16 - 1) / ((int)volume.SectorsPerCluster * 16);
                if (clusters + extra > MaximumClusters) return Status.CapacityExceeded;
                status = Allocate(extra, scratch.Slice(MaximumClusters * 2, MaximumClusters), out int actual); if (status != Status.Success) return status;
                uint oldLast = scratch[clusters - 1];
                status = SetFat(oldLast, scratch[MaximumClusters * 2], Stage.AllocateFat); if (status != Status.Success) return status;
                scratch.Slice(MaximumClusters * 2, actual).CopyTo(scratch[clusters..]); clusters += actual;
                consumedTerminator = true;
            }
            Span<byte> shortEntry = stackalloc byte[32]; shortEntry.Clear();
            if (template.Length == 32) template.CopyTo(shortEntry); else shortEntry[11] = 0x20;
            alias.CopyTo(shortEntry);
            Put16(shortEntry, 20, (ushort)(first >> 16)); Put16(shortEntry, 26, (ushort)first); Put32(shortEntry, 28, length);
            Span<byte> entries = stackalloc byte[21 * 32];
            status = EncodeEntryRun(name, shortEntry, entries, out int encoded); if (status != Status.Success) return status;
            for (int i = 0; i < encoded; i++)
            {
                ulong offset = DirectoryOffset(start + i);
                status = Edit(offset / 512, Stage.PublishDirectory, out int index); if (status != Status.Success) return status;
                entries.Slice(i * 32, 32).CopyTo(after.Slice(index * 512 + (int)(offset % 512), 32));
                if (i == encoded - 1) shortOffset = offset;
            }
            if (consumedTerminator)
            {
                ulong offset = DirectoryOffset(start + encoded);
                status = Edit(offset / 512, Stage.PublishDirectory, out int index); if (status != Status.Success) return status;
                after.Slice(index * 512 + (int)(offset % 512), 32).Clear();
            }
            return Status.Success;
        }
        private Status Tombstone(Entry entry)
        {
            Status status = DirectoryChain(entry.Directory, out _); if (status != Status.Success) return status;
            for (int i = 0; i < entry.Slots; i++)
            {
                ulong offset = DirectoryOffset(entry.StartSlot + i);
                status = Edit(offset / 512, Stage.RetireDirectory, out int index); if (status != Status.Success) return status;
                // Scrub deleted metadata as well as the marker. A tombstone
                // never becomes an end marker or an apparent orphan LFN run.
                var deleted = after.Slice(index * 512 + (int)(offset % 512), 32);
                deleted.Clear(); deleted[0] = 0xe5;
            }
            return Status.Success;
        }
        private Status PlanFsInfo()
        {
            var boot = image.Slice((int)volume.PartitionFirstLba * 512, 512);
            uint info = Get16(boot, 48), backup = Get16(boot, 50);
            if (info == 0xffff) return Status.Success;
            if (info == 0 || info >= volume.ReservedSectors) return Status.InvalidVolume;
            Status status = FsInfo(volume.PartitionFirstLba + info);
            if (status != Status.Success || backup == 0 || backup == 0xffff) return status;
            if (backup >= volume.ReservedSectors || info >= volume.ReservedSectors - backup) return Status.InvalidVolume;
            return FsInfo(volume.PartitionFirstLba + backup + info);
        }
        private Status FsInfo(ulong sector)
        {
            var bytes = View(sector);
            if (Get32(bytes, 0) != 0x41615252 || Get32(bytes, 484) != 0x61417272 || Get32(bytes, 508) != 0xaa550000) return Status.InvalidVolume;
            Status status = Edit(sector, Stage.FsInfo, out int index); if (status != Status.Success) return status;
            // Advisory unknowns are valid; never publish a guessed free count.
            Put32(after.Slice(index * 512, 512), 488, uint.MaxValue); Put32(after.Slice(index * 512, 512), 492, uint.MaxValue);
            return Status.Success;
        }
        private Status Edit(ulong sector, Stage stage, out int index)
        {
            index = -1;
            if (sector < volume.PartitionFirstLba || sector - volume.PartitionFirstLba >= volume.TotalSectors) return Status.InvalidVolume;
            for (int i = 0; i < count; i++) if (changes[i].Sector == sector && changes[i].Phase == stage) { index = i; return Status.Success; }
            if (count == changes.Length) return Status.CapacityExceeded;
            var original = image.Slice((int)sector * 512, 512); var current = View(sector);
            index = count; original.CopyTo(before.Slice(count * 512, 512)); current.CopyTo(after.Slice(count * 512, 512));
            changes[count++] = new() { Sector = sector, Phase = stage }; return Status.Success;
        }
        private Span<byte> View(ulong sector)
        {
            // Last staged version is authoritative while building the plan.
            for (int i = count - 1; i >= 0; i--) if (changes[i].Sector == sector) return after.Slice(i * 512, 512);
            return image.Slice((int)sector * 512, 512);
        }
        private bool ValidChange(Change change) => change.Phase is >= Stage.Data and <= Stage.FsInfo &&
            change.Sector >= volume.PartitionFirstLba && change.Sector - volume.PartitionFirstLba < volume.TotalSectors &&
            (change.Phase is Stage.AllocateFat or Stage.ReleaseFat ?
                change.Sector >= volume.FirstFatLba && change.Sector - volume.FirstFatLba < (ulong)volume.FatSectors * 2 :
                change.Phase == Stage.FsInfo ? change.Sector > volume.PartitionFirstLba && change.Sector < volume.FirstFatLba :
                change.Sector >= volume.FirstDataLba);
        private uint JournalCrc(int entries, ulong receiptGeneration)
        {
            uint crc = Crc32.InitialState;
            crc = Frame(crc, owner); crc = Frame(crc, epoch); crc = Frame(crc, receiptGeneration); crc = Frame(crc, (uint)entries);
            for (int i = 0; i < entries; i++)
            {
                ulong sector = changes[i].Sector;
                for (int j = 0; j < 8; j++) crc = Crc32.UpdateByte(crc, (byte)(sector >> (j * 8)));
                crc = Crc32.UpdateByte(crc, (byte)changes[i].Phase);
                for (int j = 0; j < 512; j++) crc = Crc32.UpdateByte(crc, before[i * 512 + j]);
                for (int j = 0; j < 512; j++) crc = Crc32.UpdateByte(crc, after[i * 512 + j]);
            }
            return Crc32.Finish(crc);
        }
        private static uint Frame(uint crc, ulong value)
        { for (int i = 0; i < 8; i++) crc = Crc32.UpdateByte(crc, (byte)(value >> (i * 8))); return crc; }
        private bool Cluster(uint value) => value >= 2 && value - 2 < volume.ClusterCount;
        private ulong ClusterLba(uint value) => volume.FirstDataLba + (ulong)(value - 2) * volume.SectorsPerCluster;
        private ulong DirectoryOffset(int slot)
        {
            int slotsPerCluster = (int)volume.SectorsPerCluster * 16;
            return ClusterLba(scratch[slot / slotsPerCluster]) * 512 + (uint)(slot % slotsPerCluster) * 32;
        }
        private bool Work() { if (work == budget) return false; work++; return true; }
        private bool DataStorage(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return true;
            fixed (byte* p = data) fixed (Change* c = changes) fixed (uint* s = scratch)
                return Ordinary((ulong)p, (ulong)data.Length) &&
                    !Range((ulong)p, (ulong)data.Length, (ulong)c, (ulong)changes.Length * (ulong)sizeof(Change)) &&
                    !Range((ulong)p, (ulong)data.Length, (ulong)s, (ulong)scratch.Length * 4) &&
                    DeviceDisjoint((ulong)p, (ulong)data.Length, block);
        }
        private bool Inputs(ReadOnlySpan<char> path, ReadOnlySpan<byte> alias)
        {
            if (path.Length > 2049 || !InputBytes(alias)) return false;
            fixed (char* characters = path) return InputBytes(new ReadOnlySpan<byte>(characters, path.Length * 2));
        }
        private bool InputBytes(ReadOnlySpan<byte> bytes) => !bytes.Overlaps(image) && !bytes.Overlaps(before) && !bytes.Overlaps(after) && DataStorage(bytes);
    }
}
