using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Text;

namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxElfStartupStatus : byte
{ Uninitialized, Ready, InvalidManifest, InvalidElf, InvalidStorage, InvalidEntropy, OverlappingStorage, InsufficientStack, ReadBackFailed }

public struct LinuxElfStartupPlan
{
    public LinuxElfStartupStatus Status;
    public uint ArgumentCount, EnvironmentCount, UsedBytes;
    public ulong StackStart, StackBytes, StackPointer, RandomAddress, ExecutableName, ProgramHeaders, Entry;
    public readonly bool Succeeded => Status == LinuxElfStartupStatus.Ready;
}

/// <summary>
/// Builds the static Linux x64 argc/argv/envp/auxv entry stack in exclusive
/// caller-owned RW/NX backing. Every pointer is a declared user virtual address.
/// The adapter supplies genuine pre-exit entropy and CPUID.1 EDX for AT_HWCAP,
/// owns allocation/mappings and clears its seed after copying. No vDSO, runtime
/// entry, TLS thread pointer, syscall or application execution is implied.
/// </summary>
public static unsafe class LinuxElfStartup
{
    private const uint AuxiliaryPairs = 17;
    private struct Layout
    {
        internal LinuxElfStartupPlan Plan;
        internal uint Platform, Random, Frame, ArgBytes;
        internal fixed uint Arguments[129], Environment[16];
    }

    public static LinuxElfStartupStatus Build(byte* manifestBytes, uint manifestLength, byte* elf, uint elfLength,
        byte* entropy, uint entropyBytes, byte* stack, uint stackBytes, ulong stackStart, uint hwcap,
        out LinuxElfStartupPlan plan)
    {
        plan = default;
        byte* args = stackalloc byte[512];
        AsciiArgumentSlice* slices = stackalloc AsciiArgumentSlice[128];
        var status = Prepare(manifestBytes, manifestLength, elf, elfLength, entropy, entropyBytes, stack, stackBytes,
            stackStart, args, slices, out var manifest, out var image, out var layout);
        if (status != LinuxElfStartupStatus.Ready) return status;
        for (uint i = 0; i < stackBytes; i++) stack[i] = 0;
        CopyString(stack + layout.Arguments[0], manifestBytes + manifest.Path.Offset, manifest.Path.Length);
        for (uint i = 1; i < layout.Plan.ArgumentCount; i++)
            CopyString(stack + layout.Arguments[i], args + slices[i - 1].Offset, slices[i - 1].Length);
        for (uint i = 0; i < manifest.EnvironmentCount; i++)
        {
            uint row = i * 4, offset = layout.Environment[i], keyBytes = manifest.EnvironmentRanges[row + 1];
            byte* destination = stack + offset;
            for (uint j = 0; j < keyBytes; j++) destination[j] = manifestBytes[manifest.EnvironmentRanges[row] + j];
            destination[keyBytes] = (byte)'=';
            CopyString(destination + keyBytes + 1, manifestBytes + manifest.EnvironmentRanges[row + 2], manifest.EnvironmentRanges[row + 3]);
        }
        // x86_64, NUL; architecture string is independent of any firmware pointer.
        stack[layout.Platform] = (byte)'x'; stack[layout.Platform + 1] = (byte)'8'; stack[layout.Platform + 2] = (byte)'6';
        stack[layout.Platform + 3] = (byte)'_'; stack[layout.Platform + 4] = (byte)'6'; stack[layout.Platform + 5] = (byte)'4';
        for (uint i = 0; i < 16; i++) stack[layout.Random + i] = entropy[i];
        ulong* frame = (ulong*)(stack + layout.Frame);
        uint word = 0; frame[word++] = layout.Plan.ArgumentCount;
        for (uint i = 0; i < layout.Plan.ArgumentCount; i++) frame[word++] = stackStart + layout.Arguments[i];
        frame[word++] = 0;
        for (uint i = 0; i < manifest.EnvironmentCount; i++) frame[word++] = stackStart + layout.Environment[i];
        frame[word++] = 0;
        for (uint pair = 0; pair < AuxiliaryPairs; pair++)
        {
            frame[word++] = AuxiliaryType(pair);
            frame[word++] = AuxiliaryValue(pair, image, layout, hwcap);
        }
        plan = layout.Plan;
        if (!ValidateReadBack(manifestBytes, manifestLength, elf, elfLength, entropy, entropyBytes,
            stack, stackBytes, stackStart, hwcap, plan))
        { for (uint i = 0; i < stackBytes; i++) stack[i] = 0; plan = default; return LinuxElfStartupStatus.ReadBackFailed; }
        return LinuxElfStartupStatus.Ready;
    }

    // The same numeric user addresses are expected when stack points at a
    // genuine mapped user alias under the new CR3. Hardware read-back is an
    // adapter obligation; this host-callable checker cannot prove a CR3 switch.
    public static bool ValidateReadBack(byte* manifestBytes, uint manifestLength, byte* elf, uint elfLength,
        byte* entropy, uint entropyBytes, byte* stack, uint stackBytes, ulong stackStart, uint hwcap,
        LinuxElfStartupPlan plan)
    {
        byte* args = stackalloc byte[512];
        AsciiArgumentSlice* slices = stackalloc AsciiArgumentSlice[128];
        if (Prepare(manifestBytes, manifestLength, elf, elfLength, entropy, entropyBytes, stack, stackBytes, stackStart,
            args, slices, out var manifest, out var image, out var layout) != LinuxElfStartupStatus.Ready ||
            !SamePlan(plan, layout.Plan)) return false;
        ulong* frame = (ulong*)(stack + layout.Frame);
        uint word = 0;
        if (frame[word++] != plan.ArgumentCount) return false;
        for (uint i = 0; i < plan.ArgumentCount; i++)
        {
            if (frame[word++] != stackStart + layout.Arguments[i]) return false;
            byte* expected = i == 0 ? manifestBytes + manifest.Path.Offset : args + slices[i - 1].Offset;
            uint length = i == 0 ? manifest.Path.Length : slices[i - 1].Length;
            if (!SameString(stack + layout.Arguments[i], expected, length)) return false;
        }
        if (frame[word++] != 0) return false;
        for (uint i = 0; i < manifest.EnvironmentCount; i++)
        {
            if (frame[word++] != stackStart + layout.Environment[i]) return false;
            uint row = i * 4, keyBytes = manifest.EnvironmentRanges[row + 1];
            byte* actual = stack + layout.Environment[i];
            for (uint j = 0; j < keyBytes; j++) if (actual[j] != manifestBytes[manifest.EnvironmentRanges[row] + j]) return false;
            if (actual[keyBytes] != '=' || !SameString(actual + keyBytes + 1,
                manifestBytes + manifest.EnvironmentRanges[row + 2], manifest.EnvironmentRanges[row + 3])) return false;
        }
        if (frame[word++] != 0) return false;
        for (uint pair = 0; pair < AuxiliaryPairs; pair++)
            if (frame[word++] != AuxiliaryType(pair) || frame[word++] != AuxiliaryValue(pair, image, layout, hwcap)) return false;
        if (EndianPrimitives.ReadUInt32LittleEndian(stack + layout.Platform) != 0x5f363878 ||
            stack[layout.Platform + 4] != '6' || stack[layout.Platform + 5] != '4' || stack[layout.Platform + 6] != 0) return false;
        for (uint i = 0; i < 16; i++) if (stack[layout.Random + i] != entropy[i]) return false;
        for (uint i = 0; i < layout.Frame; i++) if (stack[i] != 0) return false;
        for (uint i = layout.Frame + word * 8; i < layout.Random; i++) if (stack[i] != 0) return false;
        for (uint i = layout.Random + 16; i < layout.Platform; i++) if (stack[i] != 0) return false;
        return true;
    }

    private static LinuxElfStartupStatus Prepare(byte* bytes, uint length, byte* elf, uint elfLength,
        byte* entropy, uint entropyBytes, byte* stack, uint stackBytes, ulong stackStart,
        byte* args, AsciiArgumentSlice* slices, out LinuxElfManifest manifest, out LinuxElfLoadPlan image, out Layout layout)
    {
        layout = default; image = default;
        manifest = LinuxElfPackage.ParseManifest(bytes, length);
        if (!manifest.Succeeded) return LinuxElfStartupStatus.InvalidManifest;
        if (LinuxElfLoader.Validate(elf, elfLength, manifest.MemoryBytes, manifest.StackBytes, out image) != LinuxElfLoadStatus.Ready)
            return LinuxElfStartupStatus.InvalidElf;
        if (stack == null || ((ulong)stack & 15) != 0 || stackBytes != manifest.StackBytes || (ulong)stack > ~0UL - stackBytes ||
            stackStart < 0x10000000 || (stackStart & 4095) != 0 || stackStart > 0x100000000UL - stackBytes)
            return LinuxElfStartupStatus.InvalidStorage;
        if (entropy == null || entropyBytes != 32 || (ulong)entropy > ~0UL - entropyBytes) return LinuxElfStartupStatus.InvalidEntropy;
        bool nonzero = false; for (uint i = 0; i < 16; i++) if (entropy[i] != 0) nonzero = true;
        if (!nonzero) return LinuxElfStartupStatus.InvalidEntropy;
        if (Overlaps(stack, stackBytes, bytes, length) || Overlaps(stack, stackBytes, elf, elfLength) ||
            Overlaps(stack, stackBytes, entropy, entropyBytes)) return LinuxElfStartupStatus.OverlappingStorage;
        if (AsciiArgumentSplitter.Split(bytes + manifest.Args.Offset, manifest.Args.Length, args, 512, slices, 128,
            out uint count, out uint argBytes) != AsciiArgumentStatus.Success) return LinuxElfStartupStatus.InvalidManifest;
        uint strings = manifest.Path.Length + 1 + argBytes + 7 + 16;
        for (uint i = 0; i < manifest.EnvironmentCount; i++) strings += manifest.EnvironmentRanges[i * 4 + 1] + manifest.EnvironmentRanges[i * 4 + 3] + 2;
        uint words = 1 + (count + 1) + 1 + manifest.EnvironmentCount + 1 + AuxiliaryPairs * 2;
        if (strings + words * 8 + 30 > stackBytes) return LinuxElfStartupStatus.InsufficientStack;
        uint cursor = stackBytes;
        layout.Arguments[0] = Take(ref cursor, manifest.Path.Length + 1);
        for (uint i = 0; i < count; i++) layout.Arguments[i + 1] = Take(ref cursor, slices[i].Length + 1);
        for (uint i = 0; i < manifest.EnvironmentCount; i++)
            layout.Environment[i] = Take(ref cursor, manifest.EnvironmentRanges[i * 4 + 1] + manifest.EnvironmentRanges[i * 4 + 3] + 2);
        layout.Platform = Take(ref cursor, 7);
        cursor = (cursor - 16) & ~15U; layout.Random = cursor;
        layout.Frame = (cursor - words * 8) & ~15U; layout.ArgBytes = argBytes;
        layout.Plan = new LinuxElfStartupPlan
        {
            Status = LinuxElfStartupStatus.Ready, ArgumentCount = count + 1, EnvironmentCount = manifest.EnvironmentCount,
            UsedBytes = stackBytes - layout.Frame, StackStart = stackStart, StackBytes = stackBytes,
            StackPointer = stackStart + layout.Frame, RandomAddress = stackStart + layout.Random,
            ExecutableName = stackStart + layout.Arguments[0], ProgramHeaders = image.ProgramHeaders, Entry = image.Entry
        };
        return LinuxElfStartupStatus.Ready;
    }

    private static uint Take(ref uint cursor, uint bytes) { cursor -= bytes; return cursor; }
    private static bool Overlaps(byte* a, uint aBytes, byte* b, uint bBytes) => (ulong)a < (ulong)b + bBytes && (ulong)b < (ulong)a + aBytes;
    private static void CopyString(byte* destination, byte* source, uint length)
    { for (uint i = 0; i < length; i++) destination[i] = source[i]; destination[length] = 0; }
    private static bool SameString(byte* actual, byte* expected, uint length)
    { for (uint i = 0; i < length; i++) if (actual[i] != expected[i]) return false; return actual[length] == 0; }
    private static uint AuxiliaryType(uint pair) => pair switch
    { 0 => 3, 1 => 4, 2 => 5, 3 => 6, 4 => 7, 5 => 8, 6 => 9, 7 => 11, 8 => 12, 9 => 13, 10 => 14, 11 => 15, 12 => 16, 13 => 23, 14 => 25, 15 => 31, _ => 0 };
    private static ulong AuxiliaryValue(uint pair, LinuxElfLoadPlan image, Layout layout, uint hwcap) => pair switch
    { 0 => image.ProgramHeaders, 1 => 56, 2 => image.ProgramHeaderCount, 3 => 4096, 6 => image.Entry,
      11 => layout.Plan.StackStart + layout.Platform, 12 => hwcap, 14 => layout.Plan.RandomAddress, 15 => layout.Plan.ExecutableName, _ => 0 };
    private static bool SamePlan(LinuxElfStartupPlan a, LinuxElfStartupPlan b) =>
        a.Status == b.Status && a.ArgumentCount == b.ArgumentCount && a.EnvironmentCount == b.EnvironmentCount && a.UsedBytes == b.UsedBytes &&
        a.StackStart == b.StackStart && a.StackBytes == b.StackBytes && a.StackPointer == b.StackPointer && a.RandomAddress == b.RandomAddress &&
        a.ExecutableName == b.ExecutableName && a.ProgramHeaders == b.ProgramHeaders && a.Entry == b.Entry;
}
