using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;

internal static class ConstructorFailureThunkGeneratorTests
{
    private static void Need(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
    private static T Read<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        HostCheck.Run("runtime.constructor-failure-thunk.capture-and-rejections", () =>
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts", "constructor-failure-thunk", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _ = ExceptionHandlingThunkGeneratorTests.Run(root, folder, "build",
                ["build", "tools/OS.X64TextSerializerGenerator", "--nologo", "-m:1"]);
            string tool = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/bin/Debug/net11.0/OS.X64TextSerializerGenerator.dll");
            Type generator = Assembly.LoadFile(Path.GetFullPath(tool))
                .GetType("OS.X64TextSerializerGenerator.ExceptionHandlingThunkGenerator", true)!;
            object definition = generator.GetMethod("DefineConstructorFailure")!.Invoke(null, null)!;
            object repeated = generator.GetMethod("DefineConstructorFailure")!.Invoke(null, null)!;
            object section = Read<Array>(definition, "Sections").GetValue(0)!;
            byte[] code = Read<byte[]>(section, "RawData");
            Need(code.SequenceEqual(Read<byte[]>(Read<Array>(repeated, "Sections").GetValue(0)!, "RawData")), "repeat bytes");
            object ordinary = generator.GetMethod("Define", Type.EmptyTypes)!.Invoke(null, null)!;
            byte[] original = Read<byte[]>(Read<Array>(ordinary, "Sections").GetValue(0)!, "RawData");
            Need(original.Length == 1032 && code.Length == 1048 && code.AsSpan(0, original.Length).SequenceEqual(original),
                "existing audited transfer instruction stream unchanged");
            Array symbols = Read<Array>(definition, "Symbols");
            string[] names = ["RhpThrowEx", "CLROS_CctorFailure_Select", "CLROS_CctorFailure_ValidateContinuation",
                "CLROS_CctorFailure_InvokeClauses", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke",
                "CLROS_CctorFailure_OuterClauses", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureOuter"];
            Need(symbols.Length == names.Length, "closed symbol inventory");
            for (int i = 0; i < names.Length; i++) Need(Read<string>(symbols.GetValue(i)!, "Name") == names[i], "symbol " + i);
            Need(Read<string>(section, "Name") == ".text" && Read<uint>(section, "Characteristics") == 0x60500020,
                "read-only executable contribution");
            Array relocations = Read<Array>(section, "Relocations");
            Need(relocations.Length == 4, "two callbacks and two actual table addresses");
            uint[] offsets = [330, 811, 1027, 1043], indices = [1, 2, 4, 6];
            for (int i = 0; i < offsets.Length; i++)
            {
                object r = relocations.GetValue(i)!;
                Need(Read<uint>(r, "Offset") == offsets[i] && Read<uint>(r, "SymbolIndex") == indices[i] &&
                    Read<ushort>(r, "Type") == 4 && BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan((int)offsets[i], 4)) == 0,
                    "exact disjoint REL32 " + i);
            }
            byte[] leaf = [0x48, 0x8D, 0x05, 0, 0, 0, 0, 0xC3];
            Need(Read<uint>(symbols.GetValue(3)!, "Value") == 1024 && Read<uint>(symbols.GetValue(5)!, "Value") == 1040 &&
                code.AsSpan(1024, 8).SequenceEqual(leaf) && code.AsSpan(1040, 8).SequenceEqual(leaf) &&
                code.AsSpan(1032, 8).SequenceEqual(new byte[] {0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90}), "separate aligned getters");
            // Independent instruction interpreter verifies genuine capture,
            // restored GP/XMM state, catch-produced state and all rejection exits.
            // Selector phase, rooted wrapper identity and abandoned first-bridge
            // lifetime require actual profile/native/guest evidence separately.
            for (int failure = 0; failure <= 6; failure++)
                ExceptionHandlingThunkGeneratorTests.VerifyTransfer(code, failure);
            File.WriteAllBytes(Path.Combine(folder, "constructor-failure.text.bin"), code);
            File.WriteAllLines(Path.Combine(folder, "definition.txt"),
                [$"TextBytes={code.Length}", $"TextSha256={Convert.ToHexString(SHA256.HashData(code))}",
                 "FrameBytes=840 ContextBytes=240 Getters=1024,1040"]);
        });
    }
}
