namespace OS.Tooling;

public static partial class ToolCommand
{
    private static int InspectStaticIndex(string staticElf, string staticDump)
    {
        using AuthorityFiles files = new();
        staticElf = Path.GetFullPath(staticElf);
        staticDump = Path.GetFullPath(staticDump);
        var elf = files.Open(Path.GetDirectoryName(staticElf)!, staticElf, "static ELF", "static_index_elf", 16 * 1024 * 1024, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(staticDump)!, staticDump, "static disassembly", "static_index_dump", NativeAotStaticFileProgram.MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(elf, dump);
        var report = NativeAotStaticFileProgram.Inspect(elf.ReadBytes(16 * 1024 * 1024, "static_index_elf"), dump.ReadUtf8(NativeAotStaticFileProgram.MaximumDisassemblyBytes, "static_index_dump"));
        files.VerifyUnchanged();
        Console.WriteLine("TEST_PASS nativeaot.static-files.index report_only=true " + report);
        return 0;
    }
}
