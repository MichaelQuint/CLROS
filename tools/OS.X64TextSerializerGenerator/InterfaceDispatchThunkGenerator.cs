using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;

// Independently authored Win64 instance-interface bridge. R11 is the compiler cell,
// RCX is the receiver; the ordinary scalar-return target keeps the caller's stack.
// No unwind, generic hidden-argument, struct-return or concurrent-GC contract.
public static class InterfaceDispatchThunkGenerator
{
    public const string Entry = "RhpInitialDynamicInterfaceDispatch";
    public const string CellSectionGetter = "CLROS_InterfaceDispatch_GetCellSection";
    public const string InitialStubGetter = "CLROS_InterfaceDispatch_GetInitialStub";
    public const string CellSection = "__InterfaceDispatchCellSection_Start";
    public const string Callback = "CLROS_InterfaceDispatch_Resolve";

    public static Amd64CoffObjectDefinition Define()
    {
        List<byte> text = [];
        List<int> rejects = [];
        void Add(params byte[] bytes) => text.AddRange(bytes);
        void I32(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            foreach (byte item in bytes) text.Add(item);
        }
        void Reject(byte condition)
        { Add(0x0F, condition); rejects.Add(text.Count); I32(0); }
        void Xmm(bool restore, int register)
        {
            Add(0xF3);
            if (register >= 8) Add(0x44);
            Add(0x0F, restore ? (byte)0x6F : (byte)0x7F, (byte)(0x84 + ((register & 7) << 3)), 0x24);
            I32(32 + register * 16);
        }

        Add(0x9C, 0x55, 0x48, 0x89, 0xE5); // flags; rbp; rbp = entry rsp - 16
        Add(0x50, 0x51, 0x52, 0x53, 0x56, 0x57);
        for (byte register = 0x50; register <= 0x57; register++) Add(0x41, register);
        Add(0x48, 0x81, 0xEC); I32(304);
        Add(0x48, 0x83, 0xE4, 0xF0); // shadow32, XMM256, MXCSR4; align callback rsp
        Add(0x48, 0xF7, 0x45, 0x08); I32(0x400); // reject original DF before callback
        Reject(0x85);
        for (int register = 0; register < 16; register++) Xmm(false, register);
        Add(0x0F, 0xAE, 0x9C, 0x24); I32(288);
        Add(0x48, 0x8B, 0x4D, 0xF0); // original RCX -> callback receiver
        Add(0x48, 0x8B, 0x55, 0xB0); // original R11 -> ordinary callback RDX
        Add(0xE8); uint callOffset = (uint)text.Count; I32(0);
        Add(0x48, 0x85, 0xC0); Reject(0x84); // target zero cannot resume as success
        Add(0x48, 0x89, 0x45, 0xF8); // saved RAX slot becomes the tail-jump target
        Add(0x9C, 0x41, 0x5B, 0x49, 0xF7, 0xC3); I32(0x400);
        Reject(0x85);
        Add(0x0F, 0xAE, 0x94, 0x24); I32(288);
        for (int register = 0; register < 16; register++) Xmm(true, register);
        Add(0x48, 0x8D, 0x65, 0x90); // restore exact saved-GP frame, not rounded rsp
        for (byte register = 0x5F; register >= 0x58; register--) Add(0x41, register);
        Add(0x5F, 0x5E, 0x5B, 0x5A, 0x59, 0x58, 0x5D, 0x9D, 0xFF, 0xE0);
        int halt = text.Count;
        Add(0xEB, 0xFE);
        foreach (int offset in rejects)
        {
            byte[] relative = BitConverter.GetBytes(halt - (offset + 4));
            for (int index = 0; index < 4; index++) text[offset + index] = relative[index];
        }
        while ((text.Count & 15) != 0) Add(0x90);
        uint getter = (uint)text.Count;
        Add(0x48, 0x8D, 0x05); uint dataOffset = (uint)text.Count; I32(0);
        Add(0xC3); // actual linker-owned start symbol; no fabricated section ID/end
        while ((text.Count & 15) != 0) Add(0x90);
        uint stubGetter = (uint)text.Count;
        Add(0x48, 0x8D, 0x05); I32(-(text.Count + 4));
        Add(0xC3); // same-section entry at offset zero; no special-ABI managed ldftn
        return new(0x8664, 0,
            [new(".text", text.ToArray(), 0x60500020, [new(callOffset, 1, 4), new(dataOffset, 3, 4)])],
            [new(Entry, 0, 1, 0x20, 2), new(Callback, 0, 0, 0x20, 2),
             new(CellSectionGetter, getter, 1, 0x20, 2), new(CellSection, 0, 0, 0, 2),
             new(InitialStubGetter, stubGetter, 1, 0x20, 2)]);
    }

    public static bool TryGenerate(Span<byte> destination, out int written, out CoffWriteError error)
    {
        var definition = Define();
        return Amd64CoffWriter.TryWrite(in definition, destination, true, true, out written, out error);
    }
}
