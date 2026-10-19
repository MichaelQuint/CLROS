using System.Buffers.Binary;
using OS.Manifest;
using OS.Tooling;

internal static class NativeAotServiceProgramTests
{
    private static byte[] Image()
    {
        byte[] package = LinuxElfPackageTests.CapturePackage(); var view = LinuxElfPackage.Validate(package);
        byte[] elf = package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
        elf[512] = 0x0f; elf[513] = 0x05; elf[514] = 0xc3; return elf;
    }

    [HostTest("nativeaot.service offline exact row report cannot authorize synthetic service ELF")]
    private static void Offline()
    {
        byte[] elf = Image(); const string dump = "400200: 0f 05\tsyscall\n400202: c3\tretq\n";
        var report = NativeAotServiceProgram.Inspect(elf, dump);
        HostCheck.Equal(2, report.InstructionCount); HostCheck.Equal(1, report.SyscallCount);
        HostCheck.Equal(64, report.Sha256.Length);
        string root = Path.Combine(Path.GetTempPath(), "clros-nonexistent-service-" + Guid.NewGuid().ToString("N"));
        bool rejected = false;
        try { NativeAotServiceProgram.Create(root, NativeAotServicePackage.Create(elf), dump); }
        catch (InvalidDataException error) { rejected = error.Message == "service_package_identity"; }
        HostCheck.True(rejected);
    }

    [HostTest("nativeaot.service offline report rejects changed bytes duplicate order padding and data rows")]
    private static void InvalidRows()
    {
        byte[] elf = Image();
        foreach (string dump in new[] { "", "400200: 0f 04\tsyscall\n", "400200: 0f 05\tretq\n",
            "400202: c3\tretq\n400201: 05\tbyte\n", "400fff: c3\tretq\n",
            "400200: 0f 05\tsyscall\n400200: 0f 05\tsyscall\n", "400200: 0f 05\t<unknown>\n" })
        {
            bool rejected = false;
            try { NativeAotServiceProgram.Inspect(elf, dump); } catch (InvalidDataException) { rejected = true; }
            HostCheck.True(rejected);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(68), 4);
        bool dataRejected = false;
        try { NativeAotServiceProgram.Inspect(elf, "400200: 0f 05\tsyscall\n"); }
        catch (InvalidDataException) { dataRejected = true; }
        HostCheck.True(dataRejected);
    }
}
