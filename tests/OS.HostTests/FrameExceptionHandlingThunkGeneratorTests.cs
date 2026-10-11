using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;

internal static class FrameExceptionHandlingThunkGeneratorTests
{
    private static void Need(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
    private static T Read<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        HostCheck.Run("runtime.frame-dispatch-thunk.distinct-contexts-and-rejections", () =>
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts", "frame-dispatch-thunk", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _ = ExceptionHandlingThunkGeneratorTests.Run(root, folder, "build",
                ["build", "tools/OS.X64TextSerializerGenerator", "--nologo", "-m:1"]);
            string tool = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/bin/Debug/net10.0/OS.X64TextSerializerGenerator.dll");
            Type generator = Assembly.LoadFile(Path.GetFullPath(tool))
                .GetType("OS.X64TextSerializerGenerator.ExceptionHandlingThunkGenerator", true)!;
            MethodInfo define = generator.GetMethod("DefineFrameDispatch")!;
            object first = define.Invoke(null, null)!, second = define.Invoke(null, null)!;
            Array sections = Read<Array>(first, "Sections"), symbols = Read<Array>(first, "Symbols");
            Need(Read<ushort>(first, "Machine") == 0x8664 && Read<uint>(first, "TimeDateStamp") == 0 &&
                sections.Length == 1 && symbols.Length == 8, "finite deterministic AMD64 definition");
            object section = sections.GetValue(0)!;
            byte[] code = Read<byte[]>(section, "RawData");
            Need(code.SequenceEqual(Read<byte[]>(Read<Array>(second, "Sections").GetValue(0)!, "RawData")), "repeat definition bytes");
            string[] names = ["RhpThrowEx", "CLROS_Frame_Select", "CLROS_Frame_ValidateContinuation", "CLROS_Frame_CatcherClauses",
                "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FrameCatcher", "CLROS_Frame_ValidateCleanup", "CLROS_Frame_MiddleClauses",
                "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FrameMiddle"];
            for (int i = 0; i < names.Length; i++) Need(Read<string>(symbols.GetValue(i)!, "Name") == names[i], "separate symbol " + i);
            Need(Read<string>(section, "Name") == ".text" && Read<uint>(section, "Characteristics") == 0x60500020,
                "read-only executable text");
            uint catcherGetter = Read<uint>(symbols.GetValue(3)!, "Value"), middleGetter = Read<uint>(symbols.GetValue(6)!, "Value");
            Need(catcherGetter % 16 == 0 && middleGetter == catcherGetter + 16 && middleGetter + 8 == code.Length,
                "two separate aligned original EH contribution getters");
            byte[] leaf = [0x48,0x8D,0x05,0,0,0,0,0xC3];
            foreach (uint getter in new[] {catcherGetter,middleGetter})
                Need(code.AsSpan((int)getter,8).SequenceEqual(leaf), "ordinary exact table getter");
            Need(code.AsSpan((int)catcherGetter+8,8).SequenceEqual(new byte[] {0x90,0x90,0x90,0x90,0x90,0x90,0x90,0x90}),
                "getter padding cannot execute a second getter by fall-through");
            Array relocations = Read<Array>(section, "Relocations");
            Need(relocations.Length == 5, "selector, cleanup validation, catch validation and two original tables");
            uint[] expectedSymbols = [1,5,2,4,7]; int[] calls = new int[3];
            uint previous = 0;
            for (int i = 0; i < relocations.Length; i++)
            {
                object relocation = relocations.GetValue(i)!;
                uint offset = Read<uint>(relocation, "Offset");
                Need(offset > previous && Read<uint>(relocation, "SymbolIndex") == expectedSymbols[i] &&
                    Read<ushort>(relocation, "Type") == 4 && BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan((int)offset,4)) == 0,
                    "ordered disjoint REL32 " + i);
                if (i < 3) { Need(code[offset-1] == 0xE8, "ordinary callback"); calls[i] = (int)offset - 1; }
                else Need(offset == (i == 3 ? catcherGetter : middleGetter) + 3, "table address relocation");
                previous = offset;
            }
            Need(code.AsSpan(2,7).SequenceEqual(new byte[] {0x48,0x81,0xEC,0x38,4,0,0}), "1080-byte aligned bridge frame");
            // Reuse the independent instruction interpreter, with different physical
            // frame registers/vectors and callback clobbers. Every rejected control
            // or callback prevents the next cleanup, catch or continuation transfer.
            for (int failure = 0; failure <= 9; failure++)
                ExceptionHandlingThunkGeneratorTests.VerifyTransfer(code, failure, true, calls[1], calls[2],
                    selector: calls[0], frameDispatch: true);
            File.WriteAllBytes(Path.Combine(folder, "frame.text.bin"), code);
            File.WriteAllLines(Path.Combine(folder, "definition.txt"),
                [$"TextBytes={code.Length}", $"TextSha256={Convert.ToHexString(SHA256.HashData(code))}",
                 $"Calls={string.Join(',',calls)}", "TransferBytes=496 FrameBytes=1080"]);
            VerifyCli(root, tool, folder, code);
        });
    }

    private static void VerifyCli(string root, string tool, string folder, byte[] modeledCode)
    {
        const string marker = "m04.021.frames.thunk.generate", file = "frame.obj";
        string hash = OS.Tooling.EfiContracts.FrameDispatchObjectSha256.ToUpperInvariant(), path = Path.Combine(folder, file);
        string[] outputArguments = ["--output-root", folder, "--output-file", file];
        string emitted = ExceptionHandlingThunkGeneratorTests.Run(root, folder, "frame-emit",
            [tool, "--frame-dispatch", .. outputArguments]);
        byte[] original = File.ReadAllBytes(path);
        Need(original.Length == OS.Tooling.EfiContracts.FrameDispatchObjectBytes && Convert.ToHexString(SHA256.HashData(original)) == hash,
            "complete pinned frame-dispatch COFF identity");
        Need(original.AsSpan(60, modeledCode.Length).SequenceEqual(modeledCode), "CLI emits the separately reviewed bridge definition");
        string Expected(string state) => "TEST_START " + marker + "\nM04_FRAME_DISPATCH_THUNK_READY bytes=" + original.Length +
            " sha256=" + hash + " state=" + state + "\nTEST_PASS " + marker;
        static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        Need(Normalize(emitted) == Expected("created"), "exact frame CLI publication markers");
        string repeated = ExceptionHandlingThunkGeneratorTests.Run(root, folder, "frame-repeat",
            [tool, "--frame-dispatch", .. outputArguments]);
        Need(Normalize(repeated) == Expected("unchanged") && File.ReadAllBytes(path).SequenceEqual(original), "repeat complete object unchanged");
        (string[] Modes, string Reason)[] rejected =
        [
            (["--frame-dispatch", "--frame-dispatch"], "argument_duplicate"),
            (["--frame-dispatch", "--nested-finally-catch"], "argument_mode_conflict"),
            (["--nested-finally-catch", "--frame-dispatch"], "argument_mode_conflict"),
            (["--frame-dispatch", "--rethrow-inner"], "argument_mode_conflict"),
            (["--frame-dispatch", "false"], "argument_unknown"),
            (["--frame-dispatch", "--unknown"], "argument_unknown"),
            (["--frame-dispatch", "--output-file"], "argument_value_missing")
        ];
        for (int i = 0; i < rejected.Length; i++)
        {
            string result = ExceptionHandlingThunkGeneratorTests.Run(root, folder, "frame-reject-" + i,
                [tool, .. rejected[i].Modes, .. outputArguments], expectedExit: 2);
            Need(Normalize(result) == "TEST_START " + marker + "\nTEST_FAIL " + marker + " " + rejected[i].Reason &&
                File.ReadAllBytes(path).SequenceEqual(original), "rejection preserves complete output " + i);
        }
    }
}
