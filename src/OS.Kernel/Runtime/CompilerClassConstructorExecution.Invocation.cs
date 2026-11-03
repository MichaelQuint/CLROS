namespace OS.Kernel.Runtime;

public static unsafe partial class CompilerClassConstructorExecution
{
    /// <summary>
    /// Admits one target without calling it. Success with a default ticket means
    /// completed access or active single-thread reentry. Failed records refuse
    /// reinvocation. The caller retains the original owner/storage/context and
    /// exclusively serializes nested calls until each active ticket is finished.
    /// </summary>

    public static CompilerClassConstructorStatus BeginLazy(byte* storage, ulong capacity, ulong contextAddress, out CompilerClassConstructorInvocation invocation)
    {
        invocation = default;
        CompilerClassConstructorStatus status = Validate(storage, capacity, out State* state);
        if (status != CompilerClassConstructorStatus.Success)
            return status;
        if (state->EagerPhase == 0)
            return CompilerClassConstructorStatus.EagerNotStarted;
        if (!Context(state, contextAddress))
            return CompilerClassConstructorStatus.ContextRejected;
        LazyRecord* records = Lazy(state);
        for (uint i = 0; i < state->LazyCount; i++)
            if (records[i].Context == contextAddress)
                return records[i].Phase == Failed ? CompilerClassConstructorStatus.InitializationFailed : CompilerClassConstructorStatus.Success;
        ulong target = *(ulong*)contextAddress;
        if (target == 0)
            return CompilerClassConstructorStatus.ContextRejected;
        if (!Executable(state, target))
            return CompilerClassConstructorStatus.TargetRejected;
        if (state->LazyCount == state->LazyCapacity)
            return CompilerClassConstructorStatus.CapacityExceeded;
        uint index = state->LazyCount, parent = state->ActiveLazy;
        records[index].Context = contextAddress;
        records[index].Target = target;
        records[index].Parent = parent;
        records[index].Phase = Running;
        state->LazyCount = index + 1;
        state->ActiveLazy = index;
        invocation = new((ulong)storage, capacity, contextAddress, target, index, parent);
        return CompilerClassConstructorStatus.Success;
    }

    /// <summary>
    /// Commits only the exact current active invocation. returnedNormally is the
    /// caller's assertion after the real managed call; false records failure and
    /// retains its original nonzero compiler word. Both paths pop that record.
    /// This operation neither executes a constructor nor authenticates a throw,
    /// exception root/cache or a caller's normal-return assertion.
    /// </summary>

    public static CompilerClassConstructorStatus FinishLazy(byte* storage, ulong capacity, CompilerClassConstructorInvocation invocation, bool returnedNormally)
    {
        CompilerClassConstructorStatus status = Validate(storage, capacity, out State* state);
        if (status != CompilerClassConstructorStatus.Success)
            return status;
        if (!invocation.RequiresInvocation || invocation.Storage != (ulong)storage || invocation.Capacity != capacity || invocation.RecordIndex >= state->LazyCount || state->ActiveLazy != invocation.RecordIndex)
            return CompilerClassConstructorStatus.InvocationRejected;
        LazyRecord* record = Lazy(state) + invocation.RecordIndex;
        if (record->Phase != Running || record->Context != invocation.Context || record->Target != invocation.Target || record->Parent != invocation.ParentIndex)
            return CompilerClassConstructorStatus.InvocationRejected;
        // All admission precedes the first write. Validate proved the original
        // compiler word equals this nonzero target and the full active chain.
        if (returnedNormally)
            *(ulong*)record->Context = 0;
        state->ActiveLazy = record->Parent;
        record->Parent = None;
        record->Phase = returnedNormally ? Complete : Failed;
        return CompilerClassConstructorStatus.Success;
    }
}
