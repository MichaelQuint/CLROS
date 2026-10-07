using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;

// Independently authored bounded Win64 throw/catch transfer. This is not an
// ordinary call wrapper: the handler's resulting nonvolatiles survive resume.
public static class ExceptionHandlingThunkGenerator
{
    public const string Entry = "RhpThrowEx";
    public const string Select = "CLROS_Eh_Select";
    public const string Validate = "CLROS_Eh_ValidateContinuation";
    public const string GetClauseTable = "CLROS_Eh_GetClauseTable";
    public const string ClauseTable = "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__Catcher";
    public const int ContextBytes = 240;
    public const int Capture = 32, Transfer = 272, Selected = 288, Post = 528;
    public const int Exception = 768, EntryControl = 776, PostControl = 808;
    public const int FrameBytes = 840;

    public static Amd64CoffObjectDefinition Define()
    {
        List<byte> code = [];
        List<int> rejects = [];
        List<CoffRelocationDefinition> relocations = [];
        void Add(params byte[] bytes) => code.AddRange(bytes);
        void I32(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            foreach (byte b in bytes) code.Add(b);
        }
        void Mem(bool load, int register, int offset)
        {
            Add((byte)(0x48 | (register >= 8 ? 4 : 0)), load ? (byte)0x8B : (byte)0x89,
                (byte)(0x84 | ((register & 7) << 3)), 0x24); I32(offset);
        }
        void Lea(int register, int offset)
        { Add((byte)(0x48 | (register >= 8 ? 4 : 0)), 0x8D, (byte)(0x84 | ((register & 7) << 3)), 0x24); I32(offset); }
        void Reject(byte condition)
        { Add(0x0F, condition); rejects.Add(code.Count); I32(0); }
        void Call(uint symbol)
        { Add(0xE8); relocations.Add(new((uint)code.Count, symbol, 4)); I32(0); }
        void Registers(bool load, int context)
        {
            int[] registers = [3, 5, 6, 7, 12, 13, 14, 15];
            for (int i = 0; i < registers.Length; i++) Mem(load, registers[i], context + 16 + 8 * i);
            for (int register = 6; register < 16; register++)
            {
                Add(0xF3);
                if (register >= 8) Add(0x44);
                Add(0x0F, load ? (byte)0x6F : (byte)0x7F, (byte)(0x84 | ((register & 7) << 3)), 0x24);
                I32(context + 80 + (register - 6) * 16);
            }
        }
        void Control(int offset, bool first)
        {
            if (!first) Add(0x9C, 0x58); // preserve the handler's control observation
            Mem(false, 0, offset);
            Add(0x0F, 0x20, 0xE0); Mem(false, 0, offset + 8); // CR4
            Add(0x0F, 0xAE, 0x9C, 0x24); I32(offset + 16); // STMXCSR
            Add(0xD9, 0xBC, 0x24); I32(offset + 20); // FNSTCW
            Add(0x48, 0xF7, 0x84, 0x24); I32(offset); I32(0x400); Reject(0x85); // DF
            Add(0x48, 0xF7, 0x84, 0x24); I32(offset + 8); I32(1 << 23); Reject(0x85); // CET
        }

        Add(0x9C, 0x58); // capture flags before SUB changes arithmetic flags
        Add(0x48, 0x81, 0xEC); I32(FrameBytes); // entry8(mod16) -> callback0(mod16)
        Mem(false, 1, Exception);
        Mem(false, 0, EntryControl);
        Mem(true, 0, FrameBytes); Mem(false, 0, Capture); // genuine raw return RIP
        Lea(0, FrameBytes + 8); Mem(false, 0, Capture + 8); // genuine throwing frame SP
        Registers(false, Capture);
        Mem(true, 0, EntryControl); Control(EntryControl, true);
        Mem(true, 1, Exception); Lea(2, Capture); Lea(8, Transfer); Lea(9, EntryControl);
        Call(1); Add(0x83, 0xF8, 0x01); Reject(0x85);
        // No original RBX/RBP is used for bookkeeping across the special call.
        Registers(true, Selected);
        Mem(true, 1, Exception);
        Add(0xFF, 0x94, 0x24); I32(Transfer); // call actual validated compiler funclet
        Mem(false, 0, Post); // preserve the actual returned continuation
        Mem(true, 0, Selected + 8); Mem(false, 0, Post + 8);
        Registers(false, Post);
        Control(PostControl, false);
        Mem(true, 1, Post); Lea(2, Post); Lea(8, Transfer); Lea(9, EntryControl);
        Call(2); Add(0x83, 0xF8, 0x01); Reject(0x85);
        // A validation callback may use registers, but cannot replace the
        // catch-produced set. Restore its FP controls and nonvolatile values.
        Add(0x0F, 0xAE, 0x94, 0x24); I32(PostControl + 16);
        Add(0xD9, 0xAC, 0x24); I32(PostControl + 20);
        Registers(true, Post);
        Mem(true, 0, Post); Mem(true, 11, Post + 8);
        Add(0x4C, 0x89, 0xDC, 0xFF, 0xE0); // RSP = handling frame SP; JMP returned RAX
        int reject = code.Count;
        Add(0xEB, 0xFE); // rejected transfer never returns into compiled throw
        foreach (int offset in rejects)
        {
            byte[] displacement = BitConverter.GetBytes(reject - offset - 4);
            for (int i = 0; i < 4; i++) code[offset + i] = displacement[i];
        }
        while ((code.Count & 15) != 0) Add(0x90);
        uint getter = (uint)code.Count;
        Add(0x48, 0x8D, 0x05);
        relocations.Add(new((uint)code.Count, 4, 4)); I32(0);
        Add(0xC3); // Ordinary Win64 leaf: original linker-resolved EH contribution.
        return new(0x8664, 0, [new(".text", code.ToArray(), 0x60500020, relocations.ToArray())],
            [new(Entry, 0, 1, 0x20, 2), new(Select, 0, 0, 0x20, 2), new(Validate, 0, 0, 0x20, 2),
             new(GetClauseTable, getter, 1, 0x20, 2), new(ClauseTable, 0, 0, 0, 2)]);
    }

    public static bool TryGenerate(Span<byte> destination, out int written, out CoffWriteError error)
    {
        var definition = Define();
        return Amd64CoffWriter.TryWrite(in definition, destination, true, true, out written, out error);
    }
}
