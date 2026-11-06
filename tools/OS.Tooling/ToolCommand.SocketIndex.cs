namespace OS.Tooling;

public static partial class ToolCommand
{
    private static int InspectSocketIndex(string socketElf, string socketDump)
    {
        Console.WriteLine("TEST_START linux.socket-index");
        using AuthorityFiles files = new();
        socketElf = Path.GetFullPath(socketElf);
        socketDump = Path.GetFullPath(socketDump);
        var elf = files.Open(Path.GetDirectoryName(socketElf)!, socketElf, "socket ELF", "socket_index_elf", 16 * 1024 * 1024, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(socketDump)!, socketDump, "socket disassembly", "socket_index_dump", NativeAotSocketProgram.MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(elf, dump);
        var report = NativeAotSocketProgram.Inspect(elf.ReadBytes(16 * 1024 * 1024, "socket_index_elf"), dump.ReadUtf8(NativeAotSocketProgram.MaximumDisassemblyBytes, "socket_index_dump"));
        files.VerifyUnchanged();
        Console.WriteLine("TEST_PASS linux.socket-index report_only=true " + report);
        return 0;
    }
}
