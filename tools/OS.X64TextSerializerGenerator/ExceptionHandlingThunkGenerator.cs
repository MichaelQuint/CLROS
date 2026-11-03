using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;

// Independently authored bounded Win64 throw/catch transfer. This is not an
// ordinary call wrapper: the handler's resulting nonvolatiles survive resume.
public static class ExceptionHandlingThunkGenerator
{
    public const string Entry = "RhpThrowEx";
    public const string Select = "CLROS_Eh_Select";
    public const string Validate = "CLROS_Eh_ValidateContinuation";
    public const string ValidateFinally = "CLROS_Eh_ValidateFinally";
    public const string GetClauseTable = "CLROS_Eh_GetClauseTable";
    public const string ClauseTable = "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__Catcher";
    public const int ContextBytes = 240;
    public const int Capture = 32, Transfer = 272, Selected = 288, Post = 528;
    public const int Exception = 768, EntryControl = 776, PostControl = 808;
    public const int FrameBytes = 840;

    public static Amd64CoffObjectDefinition Define() => Define(false);

    public static Amd64CoffObjectDefinition Define(bool singleFinally) => DefineCore(singleFinally, false, false);

    // These definitions are not linked by an EFI profile until its independent
    // active-exception, frame and handler admission has been implemented/reviewed.
    public static Amd64CoffObjectDefinition DefineRethrow(bool outer) => DefineCore(!outer, true, outer);

    public static Amd64CoffObjectDefinition DefineNestedFinally() => DefineCore(true, false, false, true);

    public static Amd64CoffObjectDefinition DefineFrameDispatch() => DefineCore(true, false, false, false, true);

    // Both real throws enter this bridge. The profile's selector must distinguish
    // the constructor exception from the later wrapper under retained authority.
    public static Amd64CoffObjectDefinition DefineConstructorFailure() =>
        DefineCore(false, false, false, constructorFailure: true);

    public static Amd64CoffObjectDefinition DefineFilteredCatch() =>
        DefineCore(false, false, false, filteredCatch: true);

    private static Amd64CoffObjectDefinition DefineCore(bool singleFinally, bool rethrowProfile, bool outer,
        bool nestedFinally = false, bool frameDispatch = false, bool constructorFailure = false, bool filteredCatch = false)
    {
        // The separate nested ABI adds two target slots before the aligned context.
        // Original definitions keep their exact byte layout and object identities.
        int FrameBytes = frameDispatch ? 1080 : nestedFinally ? 856 : ExceptionHandlingThunkGenerator.FrameBytes;
        int Selected = nestedFinally ? 304 : ExceptionHandlingThunkGenerator.Selected;
        int Cleanup = frameDispatch ? 528 : Selected;
        int Post = frameDispatch ? 768 : nestedFinally ? 544 : ExceptionHandlingThunkGenerator.Post;
        int Exception = frameDispatch ? 1008 : nestedFinally ? 784 : ExceptionHandlingThunkGenerator.Exception;
        int EntryControl = frameDispatch ? 1016 : nestedFinally ? 792 : ExceptionHandlingThunkGenerator.EntryControl;
        int PostControl = frameDispatch ? 1048 : nestedFinally ? 824 : ExceptionHandlingThunkGenerator.PostControl;
        List<byte> code = [];
        List<int> rejects = [];
        List<CoffRelocationDefinition> relocations = [];
        void Add(params byte[] bytes) => code.AddRange(bytes);
        void I32(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            foreach (byte b in bytes)
                code.Add(b);
        }
        void Mem(bool load, int register, int offset)
        {
            Add((byte)(0x48 | (register >= 8 ? 4 : 0)), load ? (byte)0x8B : (byte)0x89,
                (byte)(0x84 | ((register & 7) << 3)), 0x24);
            I32(offset);
        }
        void Lea(int register, int offset)
        {
            Add((byte)(0x48 | (register >= 8 ? 4 : 0)), 0x8D, (byte)(0x84 | ((register & 7) << 3)), 0x24);
            I32(offset);
        }
        void Reject(byte condition)
        {
            Add(0x0F, condition);
            rejects.Add(code.Count);
            I32(0);
        }
        void Call(uint symbol)
        {
            Add(0xE8);
            relocations.Add(new((uint)code.Count, symbol, 4));
            I32(0);
        }
        void Registers(bool load, int context)
        {
            int[] registers = [3, 5, 6, 7, 12, 13, 14, 15];
            for (int i = 0; i < registers.Length; i++)
                Mem(load, registers[i], context + 16 + 8 * i);
            for (int register = 6; register < 16; register++)
            {
                Add(0xF3);
                if (register >= 8)
                    Add(0x44);
                Add(0x0F, load ? (byte)0x6F : (byte)0x7F, (byte)(0x84 | ((register & 7) << 3)), 0x24);
                I32(context + 80 + (register - 6) * 16);
            }
        }
        void Control(int offset, bool first)
        {
            if (!first)
                Add(0x9C, 0x58); // preserve the handler's control observation
            Mem(false, 0, offset);
            Add(0x0F, 0x20, 0xE0);
            Mem(false, 0, offset + 8); // CR4
            Add(0x0F, 0xAE, 0x9C, 0x24);
            I32(offset + 16); // STMXCSR
            Add(0xD9, 0xBC, 0x24);
            I32(offset + 20); // FNSTCW
            Add(0x48, 0xF7, 0x84, 0x24);
            I32(offset);
            I32(0x400);
            Reject(0x85); // DF
            Add(0x48, 0xF7, 0x84, 0x24);
            I32(offset + 8);
            I32(1 << 23);
            Reject(0x85); // CET
        }

        Add(0x9C, 0x58); // capture flags before SUB changes arithmetic flags
        Add(0x48, 0x81, 0xEC);
        I32(FrameBytes); // entry8(mod16) -> callback0(mod16)
        if (outer)
            Add(0x31, 0xC9); // RhpRethrow has no exception argument; never trust incoming RCX.
        Mem(false, 1, Exception);
        Mem(false, 0, EntryControl);
        Mem(true, 0, FrameBytes);
        Mem(false, 0, Capture); // genuine raw return RIP
        Lea(0, FrameBytes + 8);
        Mem(false, 0, Capture + 8); // genuine throwing frame SP
        Registers(false, Capture);
        Mem(true, 0, EntryControl);
        Control(EntryControl, true);
        Mem(true, 1, Exception);
        Lea(2, Capture);
        Lea(8, Transfer);
        Lea(9, EntryControl);
        Call(1);
        Add(0x83, 0xF8, 0x01);
        Reject(0x85);
        if (singleFinally)
        {
            // The selector admits both targets before any special call. The
            // returning cleanup uses its selected physical frame. Its registers
            // and RAX are not the catch's eventual continuation state.
            for (int cleanup = 0; cleanup < (nestedFinally ? 2 : 1); cleanup++)
            {
                Registers(true, Cleanup);
                Add(0xFF, 0x94, 0x24);
                I32(Transfer + 8 + cleanup * 8);
                Control(PostControl, false);
                Lea(1, Transfer);
                Lea(2, EntryControl);
                Lea(8, PostControl);
                Call(5);
                Add(0x83, 0xF8, 0x01);
                Reject(0x85);
                Add(0x0F, 0xAE, 0x94, 0x24);
                I32(EntryControl + 16);
                Add(0xD9, 0xAC, 0x24);
                I32(EntryControl + 20);
            }
        }
        // No original RBX/RBP is used for bookkeeping across the special call.
        Registers(true, Selected);
        // Only the rethrow selector can supply the active rooted exception.
        // Its Reserved slot is not a cleanup target in the outer bridge.
        Mem(true, 1, outer ? Transfer + 8 : Exception);
        Add(0xFF, 0x94, 0x24);
        I32(Transfer); // call actual validated compiler funclet
        Mem(false, 0, Post); // preserve the actual returned continuation
        Mem(true, 0, Selected + 8);
        Mem(false, 0, Post + 8);
        Registers(false, Post);
        Control(PostControl, false);
        Mem(true, 1, Post);
        Lea(2, Post);
        Lea(8, Transfer);
        Lea(9, EntryControl);
        Call(2);
        Add(0x83, 0xF8, 0x01);
        Reject(0x85);
        // A validation callback may use registers, but cannot replace the
        // catch-produced set. Restore its FP controls and nonvolatile values.
        Add(0x0F, 0xAE, 0x94, 0x24);
        I32(PostControl + 16);
        Add(0xD9, 0xAC, 0x24);
        I32(PostControl + 20);
        Registers(true, Post);
        Mem(true, 0, Post);
        Mem(true, 11, Post + 8);
        Add(0x4C, 0x89, 0xDC, 0xFF, 0xE0); // RSP = handling frame SP; JMP returned RAX
        int reject = code.Count;
        Add(0xEB, 0xFE); // rejected transfer never returns into compiled throw
        foreach (int offset in rejects)
        {
            byte[] displacement = BitConverter.GetBytes(reject - offset - 4);
            for (int i = 0; i < 4; i++)
                code[offset + i] = displacement[i];
        }
        while ((code.Count & 15) != 0)
            Add(0x90);
        uint getter = (uint)code.Count;
        Add(0x48, 0x8D, 0x05);
        relocations.Add(new((uint)code.Count, 4, 4));
        I32(0);
        Add(0xC3); // Ordinary Win64 leaf: original linker-resolved EH contribution.
        string selector = filteredCatch ? "CLROS_Filter_Select" : constructorFailure ? "CLROS_CctorFailure_Select" : frameDispatch ? "CLROS_Frame_Select" : nestedFinally ? "CLROS_Nested_Select" : !rethrowProfile ? Select : outer ? "CLROS_Rethrow_SelectOuter" : "CLROS_Rethrow_SelectInner";
        string validator = filteredCatch ? "CLROS_Filter_ValidateContinuation" : constructorFailure ? "CLROS_CctorFailure_ValidateContinuation" : frameDispatch ? "CLROS_Frame_ValidateContinuation" : nestedFinally ? "CLROS_Nested_ValidateContinuation" : !rethrowProfile ? Validate : outer ? "CLROS_Rethrow_ValidateOuter" : "CLROS_Rethrow_RejectInnerReturn";
        string getterName = filteredCatch ? "CLROS_Filter_Clauses" : constructorFailure ? "CLROS_CctorFailure_InvokeClauses" : frameDispatch ? "CLROS_Frame_CatcherClauses" : nestedFinally ? "CLROS_Nested_Clauses" : !rethrowProfile ? GetClauseTable : outer ? "CLROS_Rethrow_OuterClauses" : "CLROS_Rethrow_InnerClauses";
        string table = filteredCatch ? "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FilterCatcher" : constructorFailure ? "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke" : frameDispatch ? "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FrameCatcher" : nestedFinally ? "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NestedCatcher" : !rethrowProfile ? ClauseTable : outer
            ? "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__RethrowOuter"
            : "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__RethrowInner";
        List<CoffSymbolDefinition> symbols =
            [new(outer ? "RhpRethrow" : Entry, 0, 1, 0x20, 2), new(selector, 0, 0, 0x20, 2), new(validator, 0, 0, 0x20, 2),
             new(getterName, getter, 1, 0x20, 2), new(table, 0, 0, 0, 2)];
        if (singleFinally)
            symbols.Add(new(frameDispatch ? "CLROS_Frame_ValidateCleanup" : nestedFinally ? "CLROS_Nested_ValidateCleanup" : rethrowProfile ? "CLROS_Rethrow_ValidateFinally" : ValidateFinally, 0, 0, 0x20, 2));
        if (frameDispatch || constructorFailure)
        {
            while ((code.Count & 15) != 0)
                Add(0x90);
            uint middleGetter = (uint)code.Count;
            Add(0x48, 0x8D, 0x05);
            relocations.Add(new((uint)code.Count, (uint)symbols.Count + 1, 4));
            I32(0);
            Add(0xC3);
            symbols.Add(new(constructorFailure ? "CLROS_CctorFailure_OuterClauses" : "CLROS_Frame_MiddleClauses", middleGetter, 1, 0x20, 2));
            symbols.Add(new(constructorFailure ? "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureOuter" : "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FrameMiddle", 0, 0, 0, 2));
        }
        return new(0x8664, 0, [new(".text", code.ToArray(), 0x60500020, relocations.ToArray())], symbols.ToArray());
    }

    public static bool TryGenerate(Span<byte> destination, out int written, out CoffWriteError error)
        => TryGenerate(destination, out written, out error, false);

    public static bool TryGenerate(Span<byte> destination, out int written, out CoffWriteError error, bool singleFinally)
    {
        var definition = Define(singleFinally);
        return Amd64CoffWriter.TryWrite(in definition, destination, true, true, out written, out error);
    }
}
