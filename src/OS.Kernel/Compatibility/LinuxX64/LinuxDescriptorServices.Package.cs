using OS.Manifest;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>
    /// Validate one retained ELF package and construct its executable/data views
    /// below /app/. The serialized supervisor caller proves trusted readable
    /// package bytes and writable, zeroed, disjoint output storage. Package bytes
    /// must remain immutable; paths/views must remain stable until every borrowed
    /// descriptor is closed and End succeeds. No file is opened or allocated.
    /// Invalid bounds, aliases, package data, occupied output or paths exceeding
    /// the descriptor limit return false/count0 without changing either output.
    /// V1 exposes only the executable; V2 adds its declared immutable data files.
    /// This grants no user mapping, concurrent access or mutable-file support.
    /// </summary>
    public static bool CreatePackageViews(byte* package, uint packageBytes,
        View* views, uint capacity, byte* paths, uint pathBytes, out uint count)
    {
        count = 0;
        if (capacity == 0 || capacity > MaximumViews || ((ulong)views & 7) != 0 ||
            pathBytes < capacity * MaximumPath || pathBytes > MaximumViews * MaximumPath || !Extent(package, packageBytes) ||
            !Extent(views, capacity * (ulong)sizeof(View)) || !Extent(paths, pathBytes) ||
            Overlap((ulong)package, packageBytes, (ulong)views, capacity * (ulong)sizeof(View)) ||
            Overlap((ulong)package, packageBytes, (ulong)paths, pathBytes) ||
            Overlap((ulong)views, capacity * (ulong)sizeof(View), (ulong)paths, pathBytes)) return false;

        var admitted = LinuxElfPackage.Validate(package, packageBytes);
        if (!admitted.Succeeded || admitted.DataFileCount + 1 > capacity ||
            admitted.Manifest.Path.Length > MaximumPath - 6) return false;
        LinuxElfPackage.DataFile* files = stackalloc LinuxElfPackage.DataFile[(int)LinuxElfPackage.MaximumDataFiles];
        for (uint i = 0; i < admitted.DataFileCount; i++)
            if (LinuxElfPackage.GetDataFile(package, packageBytes, i, out files[i]) != LinuxElfStatus.Success ||
                files[i].PathBytes > MaximumPath - 6) return false;
        for (uint i = 0; i < capacity; i++)
            if (views[i].Path != null || views[i].PathBytes != 0 || views[i].Data != null ||
                views[i].Bytes != 0 || views[i].Inode != 0) return false;
        for (uint i = 0; i < capacity * MaximumPath; i++) if (paths[i] != 0) return false;

        WritePackageView(views, paths, package + admitted.ManifestOffset + admitted.Manifest.Path.Offset,
            admitted.Manifest.Path.Length, package + admitted.PayloadOffset, admitted.PayloadBytes, 256);
        for (uint i = 0; i < admitted.DataFileCount; i++)
            WritePackageView(views + i + 1, paths + (i + 1) * MaximumPath,
                package + files[i].PathOffset, files[i].PathBytes,
                package + files[i].DataOffset, files[i].DataBytes, 257 + i);
        count = admitted.DataFileCount + 1;
        return true;
    }

    /// <summary>
    /// Admit a nonempty bounded copy-out source wholly within one immutable
    /// data view of a live descriptor owner. The serialized supervisor caller
    /// retains the same trusted mappings and package lifetime as Begin. Ranges
    /// outside one retained data view and consumed owners fail closed. This
    /// checks source ownership only; user destinations still require validation.
    /// </summary>
    public static bool ContainsPackageData(State* state, byte* source, uint bytes)
    {
        if (bytes == 0 || bytes > MaximumIo || !Extent(source, bytes) || !Audit(state)) return false;
        ulong start = (ulong)source;
        for (uint i = 0; i < state->Owner.ViewCount; i++)
        {
            View view = state->Owner.Views[i];
            if (start >= (ulong)view.Data && start - (ulong)view.Data <= view.Bytes &&
                bytes <= view.Bytes - (start - (ulong)view.Data)) return true;
        }
        return false;
    }

    private static void WritePackageView(View* view, byte* path, byte* relative, uint length,
        byte* data, uint bytes, ulong inode)
    {
        path[0] = (byte)'/'; path[1] = (byte)'a'; path[2] = (byte)'p'; path[3] = (byte)'p'; path[4] = (byte)'/';
        for (uint i = 0; i < length; i++) path[5 + i] = relative[i];
        *view = new() { Path = path, PathBytes = 5 + length, Data = data, Bytes = bytes, Inode = inode };
    }
}
