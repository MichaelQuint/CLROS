using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;
using System.Runtime.Loader;

internal static partial class EfiLinuxAliasCopyTests
{
    [HostTest("efi linux alias copy actual aligned image and pool copies in both directions")]
    private static void AlignedCopies()
    {
        using var fixture = Compile();
        fixture.Run(0);
    }

    [HostTest("efi linux alias copy actual unaligned noncontiguous pages preserve bounds")]
    private static void NoncontiguousPages()
    {
        using var fixture = Compile();
        fixture.Run(1);
    }

    [HostTest("efi linux alias copy later permission failures publish no partial output")]
    private static void Permissions()
    {
        using var fixture = Compile();
        fixture.Run(2);
    }

    [HostTest("efi linux alias copy later foreign backing rejects before any output")]
    private static void Ownership()
    {
        using var fixture = Compile();
        fixture.Run(3);
    }

    [HostTest("efi linux alias copy actual range and authority rejection preserves output")]
    private static void Admission()
    {
        using var fixture = Compile();
        fixture.Run(4);
    }

    [HostTest("efi linux alias copy translates by touched page rather than byte")]
    private static void TranslationCounts()
    {
        using var fixture = Compile();
        fixture.Run(5);
    }

    private static Fixture Compile()
    {
        // This fixture needs an actual identity table pointer below 4 GiB. A
        // high host pointer plus a rewritten physical-root argument would not
        // exercise CopyLinuxAlias's real TryReadLeaf admission.
        HostCheck.True(OperatingSystem.IsWindows(), "actual alias-copy controls require Windows low-address ordinary RAM; no skipped behavioral pass");
        var profile = EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM10018FullProbe"] = true });
        var catalog = HostSourceCatalog.Efi(profile);
        MethodDeclarationSyntax Method(string name) => catalog.Method("OS.Boot.EfiEntry+M03FirstExitAdapter", name);
        string Text(string name) => Method(name).WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString();
        HostCheck.Equal(5, Method("CopyLinuxAlias").ParameterList.Parameters.Count);
        string source = $$"""
            using System;
            using System.Runtime.InteropServices;
            using OS.Kernel.Compatibility.LinuxX64;
            using RealAddressSpace = OS.Kernel.Memory.X64UserAddressSpace;

            public static unsafe class AliasCopyProductionFixture
            {
                private const ulong Page = 4096, User = 0x400000, Nx = 1UL << 63;
                private const uint AllocationBytes = 0x180000, ImageOffset = 0x100000,
                    PoolOffset = 0x120000, RegionBytes = 8 * 4096, BufferBytes = 8192, Guard = 64;
                private struct Storage
                {
                    public ulong LinuxUserTables, LinuxImageBacking, LinuxImagePages, LinuxMemoryPool;
                    public uint PoolPages;
                }
                private static byte* allocation, kernel;
                private static ulong* tables;
                private static Storage* state;
                private static bool authority;
                private static int lookups;
                private static readonly int[] Map = [5, 0, 2];

                [DllImport("kernel32.dll", SetLastError = true)]
                private static extern void* VirtualAlloc(void* address, nuint bytes, uint allocation, uint protection);
                [DllImport("kernel32.dll", SetLastError = true)]
                private static extern bool VirtualFree(void* address, nuint bytes, uint kind);

                // Only privileged CPU authority and the configured pool extent
                // are host bindings. Both copy/permission methods below are
                // extracted production syntax. Every lookup reaches the real
                // Kernel walker with unchanged arguments and return values.
                private static bool LinuxSyscallAuthority(Storage* value) => value == State() && authority;
                private static uint LinuxMemoryPoolPages(Storage* value) => value->PoolPages;
                private static Storage* State() => state;
                private static class X64UserAddressSpace
                {
                    public const uint MaximumTables = RealAddressSpace.MaximumTables;
                    public static bool TryReadLeaf(ulong* t, ulong root, uint count, ulong address, out ulong leaf)
                    { lookups++; return RealAddressSpace.TryReadLeaf(t, root, count, address, out leaf); }
                }

                {{Text("CopyLinuxAlias")}}
                {{Text("LinuxUserPageAccess")}}

                private static void Need(bool value, string reason)
                { if (!value) throw new InvalidOperationException(reason); }
                private static byte Payload(int i) => (byte)(i * 37 + (i >> 8) * 11 + 17);
                private static byte* Region(bool pool) => allocation + (pool ? PoolOffset : ImageOffset);
                private static ulong* Leaf(int page) => tables + 3 * 512 + page;
                private static void Setup(bool pool)
                {
                    new Span<byte>(allocation, (int)AllocationBytes).Fill(0xa5);
                    new Span<byte>(allocation, (int)RealAddressSpace.MaximumTables * 4096).Clear();
                    new Span<byte>(kernel, (int)(BufferBytes + Guard * 2)).Fill(0xa5);
                    *state = new Storage { LinuxUserTables = (ulong)tables,
                        LinuxImageBacking = (ulong)(allocation + ImageOffset), LinuxImagePages = 8,
                        LinuxMemoryPool = (ulong)(allocation + PoolOffset), PoolPages = 8 };
                    ulong root = (ulong)tables;
                    tables[0] = (root + Page) | 7;
                    tables[512] = (root + Page * 2) | 7;
                    tables[2 * 512 + (User >> 21)] = (root + Page * 3) | 7;
                    for (int i = 0; i < 3; i++) *Leaf(i) = (ulong)(Region(pool) + Map[i] * 4096) | 7 | Nx;
                    authority = true; lookups = 0;
                }
                private static byte[] Snapshot(byte* pointer, int bytes) => new ReadOnlySpan<byte>(pointer, bytes).ToArray();
                private static byte[] UserImage()
                {
                    byte[] bytes = new byte[3 * 4096];
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = Payload(i);
                    return bytes;
                }
                private static void Prepare(bool pool, bool toUser)
                {
                    if (toUser)
                    { for (int i = 0; i < BufferBytes; i++) kernel[Guard + i] = Payload(i); }
                    else
                    {
                        byte[] user = UserImage();
                        for (int i = 0; i < 3; i++) user.AsSpan(i * 4096, 4096).CopyTo(new Span<byte>(Region(pool) + Map[i] * 4096, 4096));
                    }
                }
                private static byte Copy(ulong address, uint bytes, bool toUser) =>
                    CopyLinuxAlias(State(), address, kernel + Guard, bytes, toUser);

                private static void Valid(bool pool, bool toUser, int offset, uint bytes, bool count = false)
                {
                    Setup(pool); Prepare(pool, toUser);
                    byte[] imageBefore = Snapshot(allocation + ImageOffset, (int)RegionBytes);
                    byte[] poolBefore = Snapshot(allocation + PoolOffset, (int)RegionBytes);
                    byte[] kernelBefore = Snapshot(kernel, (int)(BufferBytes + Guard * 2));
                    Need(Copy(User + (uint)offset, bytes, toUser) == 1, "valid copy rejected");
                    int pages = (offset + (int)bytes + 4095) / 4096;
                    if (count)
                    {
                        // Counts for CopyLinuxAlias itself: range admission,
                        // owned-page preflight and copy lookup, each once/page.
                        // The descriptor Output's earlier range check is absent.
                        Need(lookups == 3 * pages, "actual per-page lookup count " + lookups + " expected " + (3 * pages));
                        Need(lookups - 2 * pages == pages, "copy-loop lookup contribution");
                        Need(lookups < (int)bytes + 2 * pages, "historical byte-lookup multiplier retained");
                    }
                    if (toUser)
                    {
                        byte[] logical = new byte[3 * 4096]; Array.Fill(logical, (byte)0xa5);
                        Array.Copy(kernelBefore, (int)Guard, logical, offset, (int)bytes);
                        byte[] expectedRegion = (byte[])(pool ? poolBefore : imageBefore).Clone();
                        for (int i = 0; i < 3; i++) Array.Copy(logical, i * 4096, expectedRegion, Map[i] * 4096, 4096);
                        Need(new ReadOnlySpan<byte>(Region(pool), (int)RegionBytes).SequenceEqual(expectedRegion), "user copy bytes or page/hole canaries");
                        Need(new ReadOnlySpan<byte>(Region(!pool), (int)RegionBytes).SequenceEqual(pool ? imageBefore : poolBefore), "other owned region changed");
                        Need(new ReadOnlySpan<byte>(kernel, kernelBefore.Length).SequenceEqual(kernelBefore), "kernel source changed");
                    }
                    else
                    {
                        byte[] expected = (byte[])kernelBefore.Clone();
                        Array.Copy(UserImage(), offset, expected, (int)Guard, (int)bytes);
                        Need(new ReadOnlySpan<byte>(kernel, expected.Length).SequenceEqual(expected), "kernel output bytes or canaries");
                        Need(new ReadOnlySpan<byte>(allocation + ImageOffset, (int)RegionBytes).SequenceEqual(imageBefore), "image source changed");
                        Need(new ReadOnlySpan<byte>(allocation + PoolOffset, (int)RegionBytes).SequenceEqual(poolBefore), "pool source changed");
                    }
                }
                private static void Rejected(bool pool, bool toUser, int damage, ulong address = User + 1, uint bytes = BufferBytes)
                {
                    Setup(pool); Prepare(pool, toUser);
                    switch (damage)
                    {
                        case 0: *Leaf(2) = 0; break;
                        case 1: *Leaf(2) &= ~4UL; break;
                        case 2: *Leaf(2) &= ~2UL; break;
                        case 3: *Leaf(2) &= ~Nx; break;
                        case 4: tables[0] = 0x87; break;
                        case 5: *Leaf(2) = (ulong)(allocation + 0x140000) | 7 | Nx; break;
                        case 6: *Leaf(2) = (ulong)(Region(pool) - 4096) | 7 | Nx; break;
                        case 7: *Leaf(2) = (ulong)(Region(pool) + RegionBytes) | 7 | Nx; break;
                        case 8: authority = false; break;
                        case 9: break;
                        default: throw new InvalidOperationException("unknown damage");
                    }
                    byte[] image = Snapshot(allocation + ImageOffset, (int)RegionBytes);
                    byte[] poolBytes = Snapshot(allocation + PoolOffset, (int)RegionBytes);
                    byte[] guard = Snapshot(allocation + ImageOffset - 4096,
                        (int)(0x141000 - ImageOffset + 4096));
                    byte[] output = Snapshot(kernel, (int)(BufferBytes + Guard * 2));
                    Need(Copy(address, bytes, toUser) == 0, "invalid copy accepted " + damage);
                    Need(new ReadOnlySpan<byte>(allocation + ImageOffset, image.Length).SequenceEqual(image), "partial image output " + damage);
                    Need(new ReadOnlySpan<byte>(allocation + PoolOffset, poolBytes.Length).SequenceEqual(poolBytes), "partial pool output " + damage);
                    Need(new ReadOnlySpan<byte>(allocation + ImageOffset - 4096, guard.Length).SequenceEqual(guard), "foreign page or gap output " + damage);
                    Need(new ReadOnlySpan<byte>(kernel, output.Length).SequenceEqual(output), "partial kernel output " + damage);
                }
                private static void ReadOnlyInput()
                {
                    Setup(false); Prepare(false, false); *Leaf(2) &= ~2UL;
                    byte[] source = Snapshot(allocation + ImageOffset, (int)RegionBytes);
                    byte[] expected = Snapshot(kernel, (int)(BufferBytes + Guard * 2));
                    Array.Copy(UserImage(), 1, expected, (int)Guard, (int)BufferBytes);
                    Need(Copy(User + 1, BufferBytes, false) == 1, "read-only input rejected");
                    Need(new ReadOnlySpan<byte>(kernel, expected.Length).SequenceEqual(expected), "read-only input bytes and canaries");
                    Need(new ReadOnlySpan<byte>(allocation + ImageOffset, source.Length).SequenceEqual(source), "read-only source changed");
                }
                public static void Run(int section)
                {
                    Need(OperatingSystem.IsWindows(), "low identity fixture requires Windows");
                    allocation = null;
                    for (uint hint = 0x30000000; hint < 0x60000000 && allocation == null; hint += 0x01000000)
                        allocation = (byte*)VirtualAlloc((void*)(nuint)hint, AllocationBytes, 0x3000, 4);
                    Need(allocation != null, "low identity table/backing allocation");
                    kernel = null; state = null;
                    try
                    {
                        Need((ulong)allocation + AllocationBytes < RealAddressSpace.PhysicalEnd, "actual low physical backing");
                        tables = (ulong*)allocation;
                        kernel = (byte*)NativeMemory.AlignedAlloc(BufferBytes + Guard * 2, 64);
                        Need(kernel != null, "kernel scratch allocation");
                        state = (Storage*)NativeMemory.AllocZeroed((nuint)sizeof(Storage));
                        Need(state != null, "host boundary carrier allocation");
                        if (section == 0)
                            foreach (bool pool in new[] { false, true }) foreach (bool output in new[] { false, true })
                                foreach (uint length in new uint[] { 1, 4096, 8192 }) Valid(pool, output, 0, length);
                        if (section == 1)
                            foreach (bool pool in new[] { false, true }) foreach (bool output in new[] { false, true })
                                foreach (int offset in new[] { 1, 4095 }) Valid(pool, output, offset, BufferBytes);
                        if (section == 2)
                        {
                            foreach (bool output in new[] { false, true }) foreach (int damage in new[] { 0, 1, 3, 4 }) Rejected(false, output, damage);
                            Rejected(false, true, 2); ReadOnlyInput();
                        }
                        if (section == 3)
                        {
                            foreach (bool pool in new[] { false, true }) foreach (bool output in new[] { false, true })
                                foreach (int damage in new[] { 5, 6, 7 }) Rejected(pool, output, damage);
                            Map[2] = 7;
                            try
                            { foreach (bool pool in new[] { false, true }) foreach (bool output in new[] { false, true }) Valid(pool, output, 1, BufferBytes); }
                            finally { Map[2] = 2; }
                        }
                        if (section == 4)
                            foreach (bool output in new[] { false, true })
                            {
                                Rejected(false, output, 8);
                                Rejected(false, output, 9, User, 0);
                                Rejected(false, output, 9, 4095, 1);
                                Rejected(false, output, 9, RealAddressSpace.UserEnd - 4096, BufferBytes);
                                Rejected(false, output, 9, ulong.MaxValue - 1, BufferBytes);
                            }
                        if (section == 5)
                            foreach (bool output in new[] { false, true }) foreach (int offset in new[] { 0, 1, 4095 })
                                Valid(false, output, offset, BufferBytes, count: true);
                    }
                    finally
                    {
                        if (kernel != null) NativeMemory.AlignedFree(kernel);
                        if (state != null) NativeMemory.Free(state);
                        Need(VirtualFree(allocation, 0, 0x8000), "low identity allocation release");
                        allocation = null; tables = null; kernel = null; state = null;
                    }
                }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Concat([typeof(LinuxSyscallGateway).Assembly.Location, typeof(OS.Boot.PhysicalAddress).Assembly.Location]).Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("AliasCopy" + Guid.NewGuid().ToString("N"), [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14))], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true));
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        HostCheck.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        image.Position = 0;
        var context = new AssemblyLoadContext("alias-copy-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            Type fixture = context.LoadFromStream(image).GetType("AliasCopyProductionFixture", throwOnError: true)!;
            return new Fixture(context, fixture.GetMethod("Run")!.CreateDelegate<Action<int>>());
        }
        catch
        {
            context.Unload();
            throw;
        }
    }
}
