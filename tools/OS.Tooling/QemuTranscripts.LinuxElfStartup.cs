using System.Runtime.InteropServices;
using OS.Manifest;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static IReadOnlyList<string> M16LinuxStartupMarkers { get; } = Array.AsReadOnly<string>(
        ["TEST_START m16-004", "M16_004_IMAGE", "M16_004_STACK", "M16_004_TABLES", "M16_004_READBACK", "CLROS_M16_004_READY", "TEST_PASS m16-004"]);

    public static unsafe string[] ValidateLinuxStartupTail(string[] lines, byte[] package)
    {
        Need(lines.Length == QemuContracts.M03OwnedSubsystemMarkers.Count + 14, "m16_004_record_count");
        string[] tail = lines[^7..];
        Need(tail[0] == "TEST_START m16-004" && tail[5] == "CLROS_M16_004_READY" && tail[6] == "TEST_PASS m16-004", "m16_004_tail_identity");
        var view = LinuxElfPackage.Validate(package);
        Need(view.Succeeded, "m16_004_expected_package_invalid");
        LinuxElfLoadPlan image;
        fixed (byte* bytes = package)
            Need(LinuxElfLoader.Validate(bytes + view.PayloadOffset, view.PayloadBytes, view.Manifest.MemoryBytes,
                view.Manifest.StackBytes, out image) == LinuxElfLoadStatus.Ready, "m16_004_expected_elf_invalid");
        var mapped = ReadRecord(tail[1], "M16_004_IMAGE", "base:dec", "bytes:dec", "loads:dec", "entry:dec", "phdr:dec", "tlsFile:dec", "tlsMemory:dec");
        Need(mapped["base"] == image.ImageStart && mapped["bytes"] == image.ImageBytes && mapped["loads"] == image.LoadCount &&
            mapped["entry"] == image.Entry && mapped["phdr"] == image.ProgramHeaders && mapped["tlsFile"] == image.TlsFileBytes &&
            mapped["tlsMemory"] == image.TlsMemoryBytes, "m16_004_image_geometry");
        const string stackSuffix = " entropyBytes=32 seedCleared=32";
        Need(tail[2].EndsWith(stackSuffix, StringComparison.Ordinal), "m16_004_entropy_receipt");
        var stack = ReadRecord(tail[2][..^stackSuffix.Length], "M16_004_STACK", "start:dec", "bytes:dec", "sp:dec", "argc:dec", "envc:dec", "random:dec");
        // Sentinel entropy computes deterministic offsets only. It is not a
        // replay of guest entropy or evidence that firmware supplied randomness.
        byte* seed = stackalloc byte[32]; for (uint i = 0; i < 32; i++) seed[i] = 1;
        byte* buffer = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096);
        try
        {
            fixed (byte* bytes = package)
            {
                Need(LinuxElfStartup.Build(bytes + view.ManifestOffset, view.ManifestBytes, bytes + view.PayloadOffset,
                    view.PayloadBytes, seed, 32, buffer, view.Manifest.StackBytes, 0x70000000UL - view.Manifest.StackBytes, 0,
                    out var expected) == LinuxElfStartupStatus.Ready, "m16_004_expected_stack_invalid");
                Need(stack["start"] == expected.StackStart && stack["bytes"] == expected.StackBytes && stack["sp"] == expected.StackPointer &&
                    stack["argc"] == expected.ArgumentCount && stack["envc"] == expected.EnvironmentCount && stack["random"] == expected.RandomAddress,
                    "m16_004_stack_geometry");
            }
        }
        finally { NativeMemory.AlignedFree(buffer); }
        var tables = ReadRecord(tail[3], "M16_004_TABLES", "root:dec", "used:dec", "leaves:dec", "ranges:dec");
        ulong loadPages = 0;
        for (uint i = 0; i < image.LoadCount; i++)
        {
            uint row = i * 5; ulong address = image.Loads[row], memory = image.Loads[row + 3];
            loadPages += (((address + memory + 4095) & ~4095UL) - (address & ~4095UL)) / 4096;
        }
        Need(tables["root"] is >= 4096 and < 0x100000000 && tables["root"] % 4096 == 0 &&
            tables["used"] is >= 4 and <= 128 && tables["ranges"] >= image.LoadCount + 2 && tables["ranges"] <= 64 &&
            tables["leaves"] >= loadPages + view.Manifest.StackBytes / 4096 + 128 && tables["leaves"] <= 32768 &&
            image.ImageBytes + view.Manifest.StackBytes <= 4096 * 4096,
            "m16_004_table_geometry");
        const string proofSuffix = " restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false";
        Need(tail[4].EndsWith(proofSuffix, StringComparison.Ordinal), "m16_004_readback_receipt");
        var proof = ReadRecord(tail[4][..^proofSuffix.Length], "M16_004_READBACK", "old:dec");
        Need(proof["old"] is >= 4096 and < 0x100000000 && proof["old"] % 4096 == 0 && proof["old"] != tables["root"], "m16_004_restored_root");
        return lines[..^7];
    }
}
