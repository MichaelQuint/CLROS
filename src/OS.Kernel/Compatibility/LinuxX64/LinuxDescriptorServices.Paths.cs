using OS.Std.Text;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    // Bounded readonly observations for an explicitly admitted package process.
    // The IRQ-off descriptor dispatcher has already audited the retained owner.
    // No host namespace, directory fd, traversal, mutable cwd or writable filesystem is
    // supplied. stat/lstat use the same measured144-byte Linux x64 wire as fstat.
    private static long PackagePathStat(State* state, ulong pathAddress, ulong output, bool noFollow)
    {
        if (state->Owner.PackageProcessId == 0) return Unsupported;
        byte* path = stackalloc byte[(int)MaximumPath];
        long status = PackageReadPath(state, pathAddress, path, out uint length, out bool directory);
        if (status != 0) return status;
        status = PackagePathMetadata(state, path, length, noFollow && !directory, out uint mode, out ulong bytes, out ulong inode);
        if (status != 0) return status;
        if (directory && (mode & 0xf000) != 0x4000) return NotDirectory;
        byte* wire = stackalloc byte[144]; Clear(wire, 144);
        *(ulong*)(wire + 8) = inode; *(ulong*)(wire + 16) = 1;
        *(uint*)(wire + 24) = mode; *(ulong*)(wire + 48) = bytes; *(ulong*)(wire + 56) = 4096;
        return Output(state, output, wire, 144) ? 0 : Fault;
    }

    private static long PackageGetCwd(State* state, ulong output, ulong capacity)
    {
        if (state->Owner.PackageProcessId == 0) return Unsupported;
        if (capacity < 2) return -34; // ERANGE; raw syscall count includes NUL.
        byte* root = stackalloc byte[2]; root[0] = (byte)'/'; root[1] = 0;
        return Output(state, output, root, 2) ? 2 : Fault;
    }

    private static long PackageReadLink(State* state, ulong pathAddress, ulong output, ulong capacity)
    {
        if (state->Owner.PackageProcessId == 0) return Unsupported;
        if (capacity == 0 || capacity > long.MaxValue) return Invalid;
        byte* path = stackalloc byte[(int)MaximumPath];
        long status = PackageReadPath(state, pathAddress, path, out uint length, out bool directory);
        if (status != 0) return status;
        status = PackagePathMetadata(state, path, length, true, out uint mode, out _, out ulong inode);
        if (status != 0) return status;
        if (directory) return (mode & 0xf000) == 0x4000 || inode == 67 ? Invalid : NotDirectory;
        if ((mode & 0xf000) != 0xa000) return Invalid;
        byte* value = stackalloc byte[(int)MaximumPath]; uint bytes;
        if (inode == 67) bytes = PackagePid(value, state->Owner.PackageProcessId);
        else
        {
            View executable = state->Owner.Views[0]; bytes = executable.PathBytes;
            for (uint i = 0; i < bytes; i++) value[i] = executable.Path[i];
        }
        if (capacity < bytes) bytes = (uint)capacity;
        // readlink copies a possibly truncated prefix without a terminator.
        return Output(state, output, value, bytes) ? bytes : Fault;
    }

    private static long PackageReadPath(State* state, ulong address, byte* path, out uint length, out bool directory)
    {
        var a = state->Owner; length = 0; directory = false;
        for (; length < MaximumPath; length++)
        {
            if (address > ulong.MaxValue - length || !LinuxSyscallGateway.ValidateRange(address + length, 1, LinuxUserPageAccess.Read, a.User) ||
                a.User.CopyFromUser(a.User.Context, address + length, path + length, 1) != 1) return Fault;
            if (path[length] == 0) break;
        }
        if (length == MaximumPath) return NameTooLong;
        if (length == 0) return Missing;
        if (path[0] == '/')
        {
            for (uint i = 1; i < length; i++) path[i - 1] = path[i];
            length--;
        }
        if (length == 0) { directory = true; return 0; }
        if (path[length - 1] == '/') { directory = true; length--; }
        return AsciiPointerMatcher.IsRelativePath(path, length) ? 0 : Denied;
    }

    private static long PackagePathMetadata(State* state, byte* path, uint length, bool noFollow,
        out uint mode, out ulong bytes, out ulong inode)
    {
        mode = 0; bytes = inode = 0;
        if (length == 0) { mode = 0x416d; inode = 64; return 0; } // readonly root0555
        if (AsciiPointerMatcher.Matches(path, length, 4, 0x636f7270))
        { mode = 0x416d; inode = 65; return 0; }
        byte* process = stackalloc byte[20];
        process[0] = (byte)'p'; process[1] = (byte)'r'; process[2] = (byte)'o'; process[3] = (byte)'c'; process[4] = (byte)'/';
        uint digits = PackagePid(process + 5, state->Owner.PackageProcessId), processBytes = 5 + digits;
        bool self = AsciiPointerMatcher.Matches(path, length, 9, 0x6c65732f636f7270, 0x66);
        bool pid = PackagePathEquals(path, length, process, processBytes);
        if (self || pid)
        {
            bool link = self && noFollow;
            mode = link ? 0xa1ffU : 0x416dU; bytes = link ? digits : 0; inode = link ? 67UL : 66UL; return 0;
        }
        process[processBytes] = (byte)'/'; process[processBytes + 1] = (byte)'e';
        process[processBytes + 2] = (byte)'x'; process[processBytes + 3] = (byte)'e';
        if (PackagePathPrefix(process, processBytes + 4, path, length) ||
            length > 13 && path[13] == '/' && AsciiPointerMatcher.Matches(path, 13, 13, 0x6c65732f636f7270, 0x6578652f66) ||
            length > 12 && path[12] == '/' && AsciiPointerMatcher.Matches(path, 12, 12, 0x6d656d2f636f7270, 0x006f666e69) ||
            length > 14 && path[14] == '/' && AsciiPointerMatcher.Matches(path, 14, 14, 0x6c65732f636f7270, 0x7370616d2f66)) return NotDirectory;
        if (PackagePathEquals(path, length, process, processBytes + 4) ||
            AsciiPointerMatcher.Matches(path, length, 13, 0x6c65732f636f7270, 0x6578652f66))
        {
            View executable = state->Owner.Views[0];
            mode = noFollow ? 0xa1ffU : 0x8124U;
            bytes = noFollow ? executable.PathBytes : executable.Bytes; inode = noFollow ? 68UL : executable.Inode; return 0;
        }
        if (AsciiPointerMatcher.Matches(path, length, 12, 0x6d656d2f636f7270, 0x006f666e69))
        { mode = 0x8124; inode = 17; return 0; }
        if (AsciiPointerMatcher.Matches(path, length, 14, 0x6c65732f636f7270, 0x7370616d2f66))
        { mode = 0x8124; inode = 18; return 0; }
        for (uint i = 0; i < state->Owner.ViewCount; i++)
        {
            View view = state->Owner.Views[i];
            if (PackagePathPrefix(view.Path + 1, view.PathBytes - 1, path, length)) return NotDirectory;
            if (PackagePathEquals(path, length, view.Path + 1, view.PathBytes - 1))
            { mode = 0x8124; bytes = view.Bytes; inode = view.Inode; return 0; }
            if (PackagePathPrefix(path, length, view.Path + 1, view.PathBytes - 1))
            { mode = 0x416d; inode = 0x100000000UL + i * MaximumPath + length; return 0; }
        }
        return Missing;
    }

    private static bool PackagePathEquals(byte* left, uint leftBytes, byte* right, uint rightBytes)
    {
        if (leftBytes != rightBytes) return false;
        for (uint i = 0; i < leftBytes; i++) if (left[i] != right[i]) return false;
        return true;
    }
    private static bool PackagePathPrefix(byte* parent, uint parentBytes, byte* child, uint childBytes) =>
        parentBytes < childBytes && child[parentBytes] == '/' && PackagePathEquals(parent, parentBytes, child, parentBytes);

    private static uint PackagePid(byte* destination, uint processId)
    {
        byte* reverse = stackalloc byte[10]; uint count = 0;
        do { reverse[count++] = (byte)('0' + processId % 10); processId /= 10; } while (processId != 0);
        for (uint i = 0; i < count; i++) destination[i] = reverse[count - i - 1];
        return count;
    }
}
