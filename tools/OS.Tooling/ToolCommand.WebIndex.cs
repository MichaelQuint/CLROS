namespace OS.Tooling;

public static partial class ToolCommand
{
    private static int InspectWebIndex(string webElf, string webDump)
    {
        Console.WriteLine("TEST_START linux.web-index");
        webElf = Path.GetFullPath(webElf);
        webDump = Path.GetFullPath(webDump);
        using AuthorityFiles files = new();
        var elf = files.Open(Path.GetDirectoryName(webElf)!, webElf, "web ELF", "web_index_elf", 16 * 1024 * 1024, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(webDump)!, webDump, "web disassembly", "web_index_dump", NativeAotWebProgram.MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(elf, dump);
        var report = NativeAotWebProgram.Inspect(elf.ReadBytes(16 * 1024 * 1024, "web_index_elf"), dump.ReadUtf8(NativeAotWebProgram.MaximumDisassemblyBytes, "web_index_dump"));
        files.VerifyUnchanged();
        Console.WriteLine("TEST_PASS linux.web-index report_only=true " + report);
        return 0;
    }
}
