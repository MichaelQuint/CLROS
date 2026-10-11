using System.Buffers.Binary;

namespace OS.X64TextSerializerGenerator;

// Ordinary returning Win64 wrapper for one authenticated, nonthrowing compiler
// filter. No metadata search, pointer authentication or exception root is owned
// here. The integrating profile must review the actual compiler filter ABI.
public static class ExceptionFilterThunkGenerator
{
    public const string Entry = "CLROS_Filter_Invoke";
    public const int FrameBytes = 328;
    public const int Caller = 32, Target = 272, Exception = 280, Parent = 288;
    public const int Control = 296, Result = 320;

    // RCX=filter, RDX=exception, R8=parent X64UnwindContext. EAX=filter result.
    public static Amd64CoffObjectDefinition Define()
    {
        List<byte> code = [];
        List<int> rejects = [];
        void Add(params byte[] values) => code.AddRange(values);
        void I32(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            foreach (byte b in bytes) code.Add(b);
        }
        void Stack(bool load, int register, int offset)
        {
            Add((byte)(0x48 | (register >= 8 ? 4 : 0)), load ? (byte)0x8B : (byte)0x89,
                (byte)(0x84 | ((register & 7) << 3)), 0x24); I32(offset);
        }
        void Registers(bool load, bool parent)
        {
            int[] registers = [3, 5, 6, 7, 12, 13, 14, 15];
            for (int i = 0; i < registers.Length; i++)
            {
                int r = registers[i], offset = 16 + i * 8;
                if (!parent) Stack(load, r, Caller + offset);
                else { Add((byte)(0x49 | (r >= 8 ? 4 : 0)), 0x8B, (byte)(0x83 | ((r & 7) << 3))); I32(offset); }
            }
            for (int r = 6; r < 16; r++)
            {
                Add(0xF3);
                if (parent || r >= 8) Add((byte)((parent ? 0x41 : 0x40) | (r >= 8 ? 4 : 0)));
                Add(0x0F, load ? (byte)0x6F : (byte)0x7F, (byte)((parent ? 0x83 : 0x84) | ((r & 7) << 3)));
                if (!parent) Add(0x24);
                I32((parent ? 0 : Caller) + 80 + (r - 6) * 16);
            }
        }
        void Reject() { Add(0x0F, 0x85); rejects.Add(code.Count); I32(0); }
        void CheckControls()
        {
            Add(0x48, 0xA9); I32(0x400); Reject(); // DF in captured flags
            Add(0x0F, 0x20, 0xE0, 0x48, 0xA9); I32(1 << 23); Reject(); // CR4.CET
        }

        Add(0x9C, 0x58, 0x48, 0x81, 0xEC); I32(FrameBytes);
        Stack(false, 0, Control);
        Stack(false, 1, Target); Stack(false, 2, Exception); Stack(false, 8, Parent);
        Registers(false, false);
        Stack(true, 0, Control); CheckControls();
        Stack(false, 0, Control + 8);
        Add(0x0F, 0xAE, 0x9C, 0x24); I32(Control + 16);
        Add(0xD9, 0xBC, 0x24); I32(Control + 20);
        Stack(true, 11, Parent);
        // Keep this wrapper's RSP/shadow area while exposing original parent's
        // nonvolatiles and establisher SP to the actual compiler funclet.
        Registers(true, true);
        Stack(true, 1, Exception);
        Add(0x49, 0x8B, 0x93); I32(8); // RDX=parent.Rsp
        Add(0xFF, 0x94, 0x24); I32(Target);
        Stack(false, 0, Result);
        Add(0x9C, 0x58); CheckControls();
        Add(0x0F, 0xAE, 0x94, 0x24); I32(Control + 16);
        Add(0xD9, 0xAC, 0x24); I32(Control + 20);
        Registers(true, false);
        Stack(true, 0, Result);
        Add(0x48, 0x81, 0xC4); I32(FrameBytes); Add(0xC3);
        int reject = code.Count;
        Add(0xEB, 0xFE);
        foreach (int offset in rejects)
        {
            byte[] bytes = BitConverter.GetBytes(reject - offset - 4);
            for (int i = 0; i < 4; i++) code[offset + i] = bytes[i];
        }
        return new(0x8664, 0, [new(".text", code.ToArray(), 0x60500020, [])], [new(Entry, 0, 1, 0x20, 2)]);
    }
}
