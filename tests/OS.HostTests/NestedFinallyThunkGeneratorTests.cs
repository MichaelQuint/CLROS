using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;

internal static class NestedFinallyThunkGeneratorTests
{
    private static void Need(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
    private static T Read<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        HostCheck.Run("runtime.nested-finally-thunk.definition-and-transfer", () =>
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts", "nested-finally-thunk", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _ = ExceptionHandlingThunkGeneratorTests.Run(root, folder, "build", ["build", "tools/OS.X64TextSerializerGenerator", "--nologo", "-m:1"]);
            string tool = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/bin/Debug/net10.0/OS.X64TextSerializerGenerator.dll");
            Assembly assembly = Assembly.LoadFile(Path.GetFullPath(tool));
            Type generator = assembly.GetType("OS.X64TextSerializerGenerator.ExceptionHandlingThunkGenerator", true)!;
            MethodInfo define = generator.GetMethod("DefineNestedFinally")!;
            object first = define.Invoke(null, null)!, second = define.Invoke(null, null)!;
            Array sections = Read<Array>(first, "Sections"), symbols = Read<Array>(first, "Symbols");
            Need(Read<ushort>(first, "Machine") == 0x8664 && Read<uint>(first, "TimeDateStamp") == 0 &&
                sections.Length == 1 && symbols.Length == 6, "finite deterministic AMD64 definition");
            object section = sections.GetValue(0)!;
            byte[] code = Read<byte[]>(section, "RawData");
            Need(code.SequenceEqual(Read<byte[]>(Read<Array>(second, "Sections").GetValue(0)!, "RawData")), "repeat definition bytes");
            string[] names = ["RhpThrowEx", "CLROS_Nested_Select", "CLROS_Nested_ValidateContinuation", "CLROS_Nested_Clauses",
                "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NestedCatcher", "CLROS_Nested_ValidateCleanup"];
            for (int i = 0; i < names.Length; i++) Need(Read<string>(symbols.GetValue(i)!, "Name") == names[i], "separate symbol " + i);
            Need(Read<string>(section, "Name") == ".text" && Read<uint>(section, "Characteristics") == 0x60500020,
                "read-only executable text");
            uint getter = Read<uint>(symbols.GetValue(3)!, "Value");
            Need(getter % 16 == 0 && getter + 8 == code.Length &&
                code.AsSpan((int)getter).SequenceEqual(new byte[] {0x48,0x8D,0x05,0,0,0,0,0xC3}), "ordinary exact table getter");
            Array relocations = Read<Array>(section, "Relocations");
            Need(relocations.Length == 5, "selector, two cleanup validations, catch validation and original table");
            uint[] expectedSymbols = [1,5,5,2,4]; int[] calls = new int[4];
            uint previous = 0;
            for (int i = 0; i < relocations.Length; i++)
            {
                object relocation = relocations.GetValue(i)!;
                uint offset = Read<uint>(relocation, "Offset");
                Need(offset > previous && Read<uint>(relocation, "SymbolIndex") == expectedSymbols[i] &&
                    Read<ushort>(relocation, "Type") == 4 && BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan((int)offset,4)) == 0,
                    "ordered disjoint REL32 " + i);
                if (i < 4) { Need(code[offset-1] == 0xE8, "ordinary callback"); calls[i] = (int)offset - 1; }
                else Need(offset == getter + 3, "table address relocation");
                previous = offset;
            }
            Need(code.AsSpan(2,7).SequenceEqual(new byte[] {0x48,0x81,0xEC,0x58,3,0,0}), "856-byte aligned bridge frame");
            // This instruction interpreter was authored independently of the generator.
            // Each failure checks that a later special call/continuation is unreachable.
            for (int failure = 0; failure <= 12; failure++)
                ExceptionHandlingThunkGeneratorTests.VerifyTransfer(code, failure, true, calls[1], calls[3],
                    selector: calls[0], nestedFinally: true, secondFinallyValidator: calls[2]);
            File.WriteAllBytes(Path.Combine(folder, "nested.text.bin"), code);
            File.WriteAllLines(Path.Combine(folder, "definition.txt"),
                [$"TextBytes={code.Length}", $"TextSha256={Convert.ToHexString(SHA256.HashData(code))}", $"Calls={string.Join(',',calls)}", "TransferBytes=272 FrameBytes=856"]);
        });
    }
}
