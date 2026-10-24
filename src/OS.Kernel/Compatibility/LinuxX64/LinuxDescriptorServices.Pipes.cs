namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>One stable, noncopied kernel operation retaining its original
    /// open description after the fd closes/reuses. The thread owner admits its
    /// disjoint mapped storage, keeps it pinned across Park and releases it only
    /// after completion/exit. No user pointer or scratch buffer is pinned: every
    /// actual copy rechecks the current user mapping. Default is inactive.</summary>
    public struct PipeOperation
    {
        internal State* Owner;
        internal ulong Self, Inode, Address;
        internal uint Description, Pipe, Requested, Done, Active;
        internal bool Write;
        public readonly bool IsActive => Active == 1;
        public readonly uint TransferredBytes => Done;
    }

    // One actual thread service exclusively wraps every descriptor mutation.
    // Legacy dispatch/End cannot bypass its wake/reference transaction.
    internal static bool AttachThreads(State* state, ulong owner)
    {
        if (!Audit(state) || state->ThreadOwner != 0 || owner == 0 || (owner & 7) != 0) return false;
        state->ThreadOwner = owner; return true;
    }
    internal static bool CanDetachThreads(State* state, ulong owner)
    {
        if (!Attached(state, owner)) return false;
        for (uint i = 0; i < state->Owner.DescriptionCount; i++) if (state->Owner.Descriptions[i].Borrowers != 0) return false;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (state->Disk != null)
            for (uint i = 0; i < DiskSlots; i++)
                if (state->Disk->Requests[i].Phase != 0) return false;
#endif
        return true;
    }
    internal static bool DetachThreads(State* state, ulong owner)
    {
        if (!CanDetachThreads(state, owner)) return false;
        state->ThreadOwner = 0; return true;
    }
    internal static bool Attached(State* state, ulong owner) => Audit(state) && owner != 0 && state->ThreadOwner == owner;
    // One synchronous admission covers pipe selection and ordinary dispatch.
    // The false BeginPipe branch only reads metadata; the snapshot also only
    // reads. Neither can invalidate the audit before DispatchCore. The caller
    // supplies disjoint stack storage for MaximumPipes * 3 counts and retains
    // all post-mutation wake/operation checks under the same exclusive owner.
    internal static bool DispatchThreads(State* state, ulong owner, LinuxSyscallFrame frame,
        ref PipeOperation operation, uint* before, out bool pipe, out long value)
    {
        pipe = false; value = Fault;
        if (!Attached(state, owner)) return false;
        pipe = BeginPipe(state, frame, ref operation, out value);
        if (pipe) return true;
        var a = state->Owner;
        for (uint i = 0; i < a.PipeCount; i++)
        { before[i * 3] = a.Pipes[i].BufferedBytes; before[i * 3 + 1] = a.Pipes[i].ReaderCount; before[i * 3 + 2] = a.Pipes[i].WriterCount; }
        value = DispatchCore(state, frame); return true;
    }

    // False selects the unchanged descriptor path (non-pipe/nonblocking/I/Ov).
    // True either returns an immediate value or publishes one retained operation.
    private static bool BeginPipe(State* state, LinuxSyscallFrame frame, ref PipeOperation operation, out long value)
    {
        value = Fault;
        if (frame.Number is not (0 or 1)) return false;
        Description* d = Find(state, unchecked((uint)frame.Argument0));
        if (d == null || (Kind)d->Type is not (Kind.PipeRead or Kind.PipeWrite) || (d->Flags & NonBlock) != 0) return false;
        bool write = frame.Number == 1;
        if (write != ((Kind)d->Type == Kind.PipeWrite)) { value = BadDescriptor; return true; }
        if (frame.Argument2 > MaximumIo) { value = Invalid; return true; }
        if (frame.Argument2 == 0) { value = 0; return true; }
        if (frame.Argument1 < 4096 || frame.Argument1 >= LinuxSyscallGateway.UserEnd || frame.Argument2 > LinuxSyscallGateway.UserEnd - frame.Argument1)
            return true;
        if (operation.Active != 0 || operation.Self != 0 || d->Borrowers == MaximumDescriptors) { value = FileTableFull; return true; }
        ulong self; fixed (PipeOperation* p = &operation) self = (ulong)p;
        operation = new() { Owner = state, Self = self, Inode = d->Inode, Address = frame.Argument1,
            Description = state->Owner.Slots[unchecked((uint)frame.Argument0)].Description - 1,
            Pipe = d->Pipe, Requested = (uint)frame.Argument2, Write = write, Active = 1 };
        d->Borrowers++; value = 0; return true;
    }

    internal static bool AttemptPipe(State* state, ulong owner, ref PipeOperation operation, out long value, out bool blocked)
    {
        value = Fault; blocked = false;
        if (!CurrentPipe(state, owner, ref operation, out var d)) return false;
        var a = state->Owner; LinuxPipe* pipe = a.Pipes + operation.Pipe;
        if (!operation.Write)
        {
            uint wanted = operation.Requested < a.ScratchBytes ? operation.Requested : a.ScratchBytes;
            value = pipe->Peek(a.Scratch, wanted, true);
            if (value > 0)
            {
                if (!Output(state, operation.Address, a.Scratch, (uint)value)) value = Fault;
                else if (!pipe->Consume((uint)value)) { Clear(a.Scratch, a.ScratchBytes); return false; }
                else operation.Done = (uint)value;
            }
            else if (value == -11 && (d->Flags & NonBlock) == 0) { blocked = true; value = 0; }
        }
        else
        {
            while (operation.Done < operation.Requested)
            {
                if (pipe->ReaderCount == 0) { value = operation.Done != 0 ? operation.Done : a.SigpipeIgnored ? -32 : Unsupported; break; }
                uint remaining = operation.Requested - operation.Done, available = pipe->Capacity - pipe->BufferedBytes;
                if (available == 0 || operation.Requested <= LinuxPipe.PipeBuf && remaining > available)
                {
                    blocked = (d->Flags & NonBlock) == 0;
                    value = blocked ? 0 : operation.Done != 0 ? operation.Done : -11; break;
                }
                uint bytes = remaining < a.ScratchBytes ? remaining : a.ScratchBytes;
                if (bytes > available) bytes = available;
                ulong source = operation.Address + operation.Done;
                if (!LinuxSyscallGateway.ValidateRange(source, bytes, LinuxUserPageAccess.Read, a.User) ||
                    a.User.CopyFromUser(a.User.Context, source, a.Scratch, bytes) != 1)
                { value = operation.Done != 0 ? operation.Done : Fault; break; }
                long written = operation.Requested > LinuxPipe.PipeBuf ? pipe->WriteStreaming(a.Scratch, bytes, a.SigpipeIgnored)
                    : pipe->Write(a.Scratch, bytes, true, a.SigpipeIgnored);
                Clear(a.Scratch, a.ScratchBytes);
                if (written <= 0 || written > bytes) return false;
                operation.Done += (uint)written;
                value = operation.Done;
            }
        }
        Clear(a.Scratch, a.ScratchBytes); return true;
    }

    internal static bool EndPipe(State* state, ulong owner, ref PipeOperation operation)
    {
        if (!CurrentPipe(state, owner, ref operation, out _)) return false;
        if (!DropDescription(state, operation.Description, true)) return false;
        operation = default; return true;
    }
    private static bool CurrentPipe(State* state, ulong owner, ref PipeOperation operation, out Description* description)
    {
        description = null;
        if (!Attached(state, owner) || operation.Active != 1 || operation.Owner != state || operation.Description >= state->Owner.DescriptionCount) return false;
        fixed (PipeOperation* p = &operation) if ((ulong)p != operation.Self) return false;
        Description* d = state->Owner.Descriptions + operation.Description;
        if (d->Borrowers == 0 || d->Inode != operation.Inode || d->Pipe != operation.Pipe ||
            (Kind)d->Type != (operation.Write ? Kind.PipeWrite : Kind.PipeRead)) return false;
        description = d; return true;
    }
}
