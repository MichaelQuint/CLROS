using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Borrow bounded raw serial/package values and an exact prepared
    /// B8 write/verify lookup. Frame stdout before splitting rows, require the
    /// pinned phase package and copied instruction boundaries, then check the
    /// finite source5 Stage/Wake/Prepare/Resume relationships. Policy is16
    /// workers/32fds, idle-only, IF disabled and exit0. Invalid, incomplete,
    /// conflicting or unknown027 rows throw. No input is executed or retained.
    /// This does not replay generic clone/futex/pipe/global FIFO, authenticate
    /// actual syscall copies, join parent/startup/media/children or grant any
    /// original instruction/process/native/guest/persistence authority. Original
    /// source/package/dump/output holds remain the enclosing owner's duty.</summary>

    public static NativeAotLogUploadProtocolReport InspectNativeAotLogUploadProtocolValues(ReadOnlySpan<byte> rawCapture, ReadOnlySpan<byte> canonicalPackage, NativeAotLogUploadProgram program)
    {
        LpNeed(program is not null, "program");
        var package = NativeAotLogUploadContract.InspectClrosPackage(canonicalPackage, program!.Phase);
        var output = NativeAotLogUploadOutput.Read(rawCapture, program.Phase);
        LpNeed(output.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "terminal");
        string[] rows = output.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        return InspectNativeAotLogUploadProtocolCore(rows, program.Phase, package.PackageSha256, output, program);
    }

    // Synthetic copied ordering controls enter here, never through a fabricated
    // public package/program. The public gate supplies both pinned objects.
    internal static NativeAotLogUploadProtocolReport InspectNativeAotLogUploadProtocolCore(string[] originalRows, string phase, string? packageHash = null, NativeAotLogUploadOutput.Capture? output = null, NativeAotLogUploadProgram? program = null)
    {
        LpNeed(phase is "write" or "verify" && originalRows is { Length: > 0 and <= MaximumNativeAotLogUploadWaitRows }, "input");
        string[] rows = originalRows!.ToArray();
        var waits = ReadNativeAotLogUploadWritableWaits(rows, 32, 8192);
        int first = LpUnique(rows, "TEST_START m16-009"), last = LpUnique(rows, "TEST_PASS m16-009");
        LpNeed(first < last, "leaf");
        var callByIndex = waits.Calls.ToDictionary(x => x.RecordIndex);
        var beginByIndex = waits.Operations.ToDictionary(x => x.Begin.RecordIndex);
        var completeByIndex = waits.Operations.SelectMany(op => op.Attempts.Select(attempt => (op, attempt))).ToDictionary(x => x.attempt.Complete.RecordIndex);
        var wakeByIndex = waits.Operations.SelectMany(op => op.Attempts.Select(attempt => attempt.Wake)).ToDictionary(x => x.RecordIndex);
        var envelope = waits.EnvelopeRows.ToDictionary(x => x.RecordIndex);
        LpNeed(waits.Calls.All(x => x.RecordIndex > first && x.RecordIndex < last) && waits.EnvelopeRows.All(x => x.RecordIndex > first && x.RecordIndex < last), "outside_leaf");
        Queue<(ulong Identity, ulong Sequence)> ready = [];
        HashSet<ulong> queued = [], retired = [], blockedSource5 = [];
        List<WritableDiskRow> selected = [], residual = [];
        List<NativeAotLogUploadProtocolTurn> turns = [];
        ulong current = 0, selectedPoll = 0, ending = 0, clock = 0, started = 0, epoch = 0, transfers = 0, reap = 0;
        int turnFirst = -1, selectedPollIndex = -1, drain = -1, complete = -1, resume = -1, stdout = 0;
        ulong? expectedResume = null;
        bool prepareOnly = false, writable = false, exited = false, exitOnly = false, groupExit = false;
        bool policy = false, bound = false, budget = false, budgetEnd = false, owner = false, restore = false, terminal = false;
        ulong awaitReap = 0;
        void Clock(ulong now)
        {
            LpNeed(now >= clock && now <= long.MaxValue, "clock");
            clock = now;
        }

        void Turn(ulong identity, int index)
        {
            LpIdentity(identity);
            LpNeed(!retired.Contains(identity) && awaitReap == 0 && !owner, "turn_lifetime");
            if (current == 0)
            {
                LpNeed(selectedPoll == 0 || selectedPoll == identity, "turn_selection");
                current = identity;
                turnFirst = selectedPollIndex < 0 ? index : selectedPollIndex;
                selectedPoll = 0;
                selectedPollIndex = -1;
            }

            LpNeed(current == identity, "turn_identity");
        }

        for (int i = 0; i < rows.Length; i++)
        {
            string text = rows[i], tag = text.Split(' ', 2)[0];
            bool used = false;
            if (i < first || i > last)
            {
                residual.Add(new(i, text));
                continue;
            }

            LpNeed(tag != "M16_009_IRQ" && tag != "M16_008_TICK", "user_irq_or_tick_unsupported");
            if (text == "TEST_START m16-009")
            {
                used = true;
            }
            else if (tag == "M16_007_POLICY")
            {
                LpNeed(!policy && !bound && text == "M16_007_POLICY descriptors=32 pipes=2 capacity=4096 scratch=8192 packageBytes=" + NativeAotLogUploadContract.ClrosElfBytes + " package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler", "policy");
                policy = used = true;
            }
            else if (tag == "M16_008_BOUND")
            {
                const string suffix = " timer=idle-only clock=hpet-nanoseconds reuse=off-stack";
                LpNeed(policy && !bound && text.EndsWith(suffix, StringComparison.Ordinal), "bound");
                var r = LpRecord(text[..^suffix.Length], tag, "root pool workers code metadata userImage");
                LpNeed(r["workers"] == 16 && r["root"] >= 4096 && r["root"] % 4096 == 0 && r["pool"] >= 4096 && r["pool"] % 4096 == 0 && r["code"] == LinuxThreadProgram.Bytes && r["metadata"] == LinuxThreadProgram.MetadataPages * 4096UL && r["userImage"] == 728, "bound");
                bound = used = true;
            }
            else if (tag == "M16_008_BUDGET")
            {
                var r = LpRecord(text, tag, "started epochs steps nanos");
                LpNeed(bound && !budget && r["epochs"] == 8192 && r["steps"] == 16384 && r["nanos"] == LinuxThreadExecutionBudget.RuntimeNanoseconds, "budget");
                Clock(r["started"]);
                started = clock;
                budget = used = true;
            }
            else if (callByIndex.TryGetValue(i, out var call))
            {
                LpNeed(budget && !prepareOnly && expectedResume is null && drain < 0 && ending == 0 && !groupExit, "call_order");
                Turn(call["identity"], i);
                Clock(call["clock"]);
                LpNeed(LinuxSyscallGateway.ValidateUserFlags(call["flags"], false) && LinuxSyscallGateway.ValidateUserFlags(call["returnFlags"], false), "flags");
                LpNeed(program is null || program.IsSyscallReturn(call["rip"]) && program.IsInstruction(call["returnRip"]), "instruction_values");
                LpNeed(call["action"] is >= 1 and <= 6, "call_action");
                if (call["waitSource"] == 5)
                    writable = true;
                if (call["action"] is 2 or 4)
                {
                    LpNeed(call["nr"] == (call["action"] == 2 ? 231UL : 60UL) && call["a0"] == 0 && call["valueBits"] == 0 && call["status"] == 2, "exit0");
                    if (call["action"] == 2)
                        groupExit = true;
                }

                if (call["action"] is 2 or 3 or 4 or 6)
                    ending = call["action"];
                used = true;
            }
            else if (beginByIndex.TryGetValue(i, out var begun))
            {
                // Exact immediate CALL/BEGIN already composed above.
                LpNeed(blockedSource5.Add(begun.Call["identity"]), "stage");
                used = true;
            }
            else if (wakeByIndex.TryGetValue(i, out var wake))
            {
                Clock(wake["clock"]);
                LpNeed(!retired.Contains(wake["identity"]) && queued.Add(wake["identity"]), "wake_queued");
                ready.Enqueue((wake["identity"], wake["sequence"]));
                used = true;
            }
            else if (completeByIndex.TryGetValue(i, out var item))
            {
                var row = item.attempt.Complete;
                LpNeed(current == 0 && ready.Count != 0 && ready.Peek() == (row["identity"], row["sequence"]), "source5_fifo");
                ready.Dequeue();
                queued.Remove(row["identity"]);
                Turn(row["identity"], i);
                Clock(row["clock"]);
                writable = true;
                complete = i;
                prepareOnly = item.attempt.Repark is not null;
                if (prepareOnly)
                    ending = 3;
                else
                {
                    LpNeed(blockedSource5.Remove(row["identity"]), "complete");
                    expectedResume = row["valueBits"];
                }

                used = true;
            }
            else if (tag == "M16_027_REPARK")
            {
                LpNeed(prepareOnly && complete == i - 1 && envelope.ContainsKey(i), "repark_order");
                Clock(envelope[i]["clock"]);
                used = true;
            }
            else if (tag == "M16_008_RESUME")
            {
                var r = LpRecord(text, tag, "identity valueBits status");
                LpIdentity(r["identity"]);
                LpNeed(!prepareOnly && r["status"] == 1, "resume");
                if (expectedResume.HasValue)
                {
                    LpNeed(current == r["identity"] && expectedResume.Value == r["valueBits"], "resume");
                    expectedResume = null;
                    resume = i;
                    used = true;
                }
                else
                    LpNeed(current == 0 && !queued.Contains(r["identity"]), "resume_unmatched");
            }
            else if (tag == "M16_012_POLL")
            {
                var r = LpRecord(text, tag, "identity clock ready idle");
                // A nonzero current has already left the real ready queue;
                // our relative queue is consumed later at COMPLETE. Compare
                // only the other known source5 entries, never a global count.
                ulong knownReady = (ulong)ready.Count - (queued.Contains(r["identity"]) ? 1UL : 0UL);
                LpNeed(budget && !owner && r["ready"] <= 16 && r["ready"] >= knownReady && r["idle"] <= 1 && (r["idle"] == 0 || r["identity"] == 0), "poll");
                if (current != 0)
                    LpNeed(r["identity"] == current, "poll");
                else if (r["identity"] == 0)
                    LpNeed(selectedPoll == 0, "poll");
                else
                {
                    // Native syscall-entry polling precedes the CALL row.
                    // Bind its copied selection at the next actual turn row;
                    // this is not independent scheduler selection authority.
                    LpIdentity(r["identity"]);
                    LpNeed(awaitReap == 0 && !retired.Contains(r["identity"]) && (!blockedSource5.Contains(r["identity"]) || queued.Contains(r["identity"])) && (selectedPoll == 0 || selectedPoll == r["identity"]), "poll");
                    if (selectedPoll == 0)
                        selectedPollIndex = i;
                    selectedPoll = r["identity"];
                }

                Clock(r["clock"]);
                used = true;
            }
            else if (tag == "M16_009_DRAIN")
            {
                var r = LpRecord(text, tag, "identity epoch nanos");
                LpNeed(current != 0 && current == r["identity"] && ending != 0 && !prepareOnly && !exitOnly && expectedResume is null && drain < 0 && epoch < 8192 && r["epoch"] == epoch + 1, "drain");
                Clock(r["nanos"]);
                epoch++;
                drain = i;
                used = true;
            }
            else if (tag == "M16_008_EXIT")
            {
                var r = LpRecord(text, tag, "identity clearTidResult");
                if (current == 0 && groupExit)
                {
                    Turn(r["identity"], i);
                    ending = 4;
                    exitOnly = true;
                }

                LpNeed(current == r["identity"] && ending is 2 or 4 && !exited && expectedResume is null && (exitOnly || drain >= 0) && r["clearTidResult"] is 0 or unchecked((ulong)-14L), "exit");
                exited = used = true;
            }
            else if (tag == "M16_008_SUPERVISOR")
            {
                var r = LpRecord(text, tag, "identity action transfers ready active");
                LpNeed(current != 0 && current == r["identity"] && ending != 0 && r["action"] == ending && expectedResume is null && (prepareOnly || exitOnly ? drain < 0 : drain >= 0) && (ending is 2 or 4) == exited && transfers / 2 < 16384 && r["transfers"] == transfers + 2 && r["ready"] <= 16 && r["ready"] >= (ulong)ready.Count && r["active"] is >= 1 and <= 16, "supervisor");
                if (writable)
                    turns.Add(new(current, turnFirst, i, complete < 0 ? null : complete, resume < 0 ? null : resume, drain < 0 ? null : drain, prepareOnly, ending));
                transfers += 2;
                if (exited)
                    awaitReap = current;
                current = ending = 0;
                turnFirst = drain = complete = resume = -1;
                prepareOnly = writable = exited = exitOnly = false;
                used = true;
            }
            else if (tag == "M16_008_REAP")
            {
                const string suffix = " context=zero stack=zero slot=zero";
                LpNeed(text.EndsWith(suffix, StringComparison.Ordinal), "reap");
                var r = LpRecord(text[..^suffix.Length], tag, "old next");
                LpNeed(awaitReap != 0 && r["old"] == awaitReap && r["old"] <= ulong.MaxValue - (1UL << 32) && r["next"] == r["old"] + (1UL << 32) && !queued.Contains(r["old"]) && retired.Add(r["old"]), "reap");
                awaitReap = 0;
                reap++;
                used = true;
            }
            else if (tag == "M16_008_BUDGET_END")
            {
                var r = LpRecord(text, tag, "nanos");
                LpNeed(budget && !budgetEnd && !owner && current == 0 && selectedPoll == 0 && awaitReap == 0 && r["nanos"] >= started && r["nanos"] - started <= LinuxThreadExecutionBudget.RuntimeNanoseconds, "budget_end");
                Clock(r["nanos"]);
                budgetEnd = used = true;
            }
            else if (tag == "M16_008_OWNER_END")
            {
                const string suffix = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
                LpNeed(text.EndsWith(suffix, StringComparison.Ordinal), "owner_end");
                var r = LpRecord(text[..^suffix.Length], tag, "reaped transfers irqs exit");
                LpNeed(budgetEnd && !owner && groupExit && current == 0 && selectedPoll == 0 && awaitReap == 0 && ready.Count == 0 && expectedResume is null && reap > 0 && r["reaped"] == reap && r["transfers"] == transfers && r["irqs"] == epoch && r["exit"] == 0, "owner_end");
                owner = used = true;
            }
            else if (tag == "M16_009_RESTORE")
            {
                LpNeed(owner && !restore && text == "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "restore");
                restore = used = true;
            }
            else if (text == "CLROS_M16_009_READY")
            {
                LpNeed(restore && !terminal, "ready");
                terminal = used = true;
            }
            else if (text == "TEST_PASS m16-009")
            {
                LpNeed(terminal, "terminal");
                used = true;
            }
            else if (tag == "M16_005_WRITE")
            {
                LpNeed(!prepareOnly && expectedResume is null && !owner, "stdout_order");
                if (output is not null)
                {
                    LpNeed(stdout < output.WriteLengths.Count && text == "M16_005_WRITE fd=1 bytes=" + output.WriteLengths[stdout] + " data=", "stdout");
                    stdout++;
                }

                // The output framer owns this row's bytes. Synthetic cores do
                // not claim that raw phase output has been checked.
                used = output is not null;
            }

            if (used)
                selected.Add(new(i, text));
            else
                residual.Add(new(i, text));
        }

        LpNeed(policy && bound && budget && owner && restore && terminal && current == 0 && selectedPoll == 0 && ready.Count == 0 && blockedSource5.Count == 0 && expectedResume is null && (output is null || stdout == output.WriteLengths.Count), "incomplete");
        LpNeed(selected.Count + residual.Count == rows.Length, "census");
        return new(phase, packageHash, rows.Length, first, last, waits, selected, residual, turns, checked((int)transfers), checked((int)epoch), checked((int)reap), output is not null);
    }

    private static Dictionary<string, ulong> LpRecord(string text, string tag, string fields) => ReadRecord(text, tag, fields.Split(' ').Select(x => x + ":dec").ToArray());
    private static int LpUnique(string[] rows, string text)
    {
        int found = Array.IndexOf(rows, text);
        LpNeed(found >= 0 && Array.LastIndexOf(rows, text) == found, "leaf");
        return found;
    }

    private static void LpIdentity(ulong identity) => LpNeed(identity >> 32 != 0 && (uint)identity is >= 1 and <= 16, "identity");
    private static void LpNeed(bool value, string reason) => Need(value, "log_upload_protocol_" + reason);
}
