using System.Buffers.Binary;
namespace OS.X64TextSerializerGenerator;

// Fixed special-ABI bytes. Returning callback, full stack and SIMD admission require separate native proof.
public static class CheckedReferenceBarrierThunkGenerator
{
    public const string Callback = "CLROS_CheckedReferenceBarrier_Apply";

    public static Amd64CoffObjectDefinition Define()
    {
        List<byte> text = [];
        List<CoffRelocationDefinition> calls = [];
        List<CoffSymbolDefinition> symbols = [];
        string[] names = ["RhpAssignRef", "RhpCheckedAssignRef", "RhpByRefAssignRef"];
        for (int operation = 1; operation <= 3; operation++)
        {
            while ((text.Count & 15) != 0)
                text.Add(0x90);
            symbols.Add(new(names[operation - 1], (uint)text.Count, 1, 0x20, 2));
            List<int> rejects = [];
            void Add(params byte[] bytes) => text.AddRange(bytes);
            void I32(int value)
            {
                Span<byte> b = stackalloc byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(b, value);
                text.AddRange(b.ToArray());
            }
            void RejectIfNotZero()
            {
                Add(0x0F, 0x85);
                rejects.Add(text.Count);
                I32(0);
            }
            void Xmm(bool restore, int register)
            {
                Add(0xF3);
                if (register >= 8)
                    Add(0x44);
                Add(0x0F, restore ? (byte)0x6F : (byte)0x7F, (byte)(0x84 + ((register & 7) << 3)), 0x24);
                I32(32 + register * 16);
            }

            Add(0x9C, 0x55, 0x48, 0x89, 0xE5); // pushfq; push rbp; mov rbp,rsp
            Add(0x50, 0x51, 0x52, 0x53, 0x56, 0x57);
            for (byte r = 0x50; r <= 0x57; r++)
                Add(0x41, r);
            Add(0x48, 0x81, 0xEC);
            I32(304);
            Add(0x48, 0x83, 0xE4, 0xF0);
            Add(0x48, 0xF7, 0x45, 0x08);
            I32(0x400); // original DF, before callback/SIMD work
            RejectIfNotZero();
            for (int r = 0; r < 16; r++)
                Xmm(false, r);
            Add(0x0F, 0xAE, 0x9C, 0x24);
            I32(288); // stmxcsr
            Add(0xB9);
            I32(operation);
            Add(0x48, 0x8B, 0x55, operation == 3 ? (byte)0xD0 : (byte)0xF0);
            Add(0x4C, 0x8B, 0x45, operation == 3 ? (byte)0xD8 : (byte)0xE8);
            Add(0x41, 0xB9);
            I32(8);
            Add(0xE8);
            calls.Add(new((uint)text.Count, 3, 4));
            I32(0);
            Add(0x83, 0xF8, 0x01); // full EAX status, not AL
            RejectIfNotZero();
            Add(0x9C, 0x41, 0x5B, 0x49, 0xF7, 0xC3);
            I32(0x400);
            RejectIfNotZero();
            Add(0x0F, 0xAE, 0x94, 0x24);
            I32(288); // ldmxcsr
            for (int r = 0; r < 16; r++)
                Xmm(true, r);
            if (operation == 3)
                Add(0x48, 0x83, 0x45, 0xD8, 0x08, 0x48, 0x83, 0x45, 0xD0, 0x08);
            Add(0x48, 0x8D, 0x65, 0x90); // rsp = rbp - 112
            for (byte r = 0x5F; r >= 0x58; r--)
                Add(0x41, r);
            Add(0x5F, 0x5E, 0x5B, 0x5A, 0x59, 0x58, 0x5D, 0x9D, 0xC3);
            int reject = text.Count;
            Add(0xEB, 0xFE); // local terminal loop, never resume as success
            foreach (int offset in rejects)
            {
                byte[] relative = BitConverter.GetBytes(reject - (offset + 4));
                for (int b = 0; b < 4; b++)
                    text[offset + b] = relative[b];
            }
        }
        symbols.Add(new(Callback, 0, 0, 0x20, 2));
        return new(0x8664, 0, [new(".text", text.ToArray(), 0x60500020, calls.ToArray())], symbols.ToArray());
    }

    public static bool TryGenerate(Span<byte> destination, out int written, out CoffWriteError error)
    {
        var definition = Define();
        return Amd64CoffWriter.TryWrite(in definition, destination, true, out written, out error);
    }
}
