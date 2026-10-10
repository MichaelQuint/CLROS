using OS.Tooling;

internal static class EfiSourceAdmissionTests
{
    [HostTest("EFI allocator firewall admits shared queue ownership and rejects extra or redirected calls")]
    private static void AllocatorOwnership()
    {
        string source = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/Boot/EfiEntry.cs"));
        foreach (string call in new[] { "BootPageAllocatorMutator.AllocateOnePage", "BootPageAllocatorMutator.FreeOnePage" })
        {
            string admitted = source;
            HostCheck.True(EfiProfileTestControls.RemoveOwnedDmaCalls(ref admitted, call, out string reason), reason);
            HostCheck.True(!admitted.Contains(call, StringComparison.Ordinal));
            foreach (string changed in new[] {
                source + "\nstatic class Rogue { static void Run() { " + call + "(); } }",
                source.Replace("RunQueueCore(state)", "UnreviewedQueue(state)", StringComparison.Ordinal),
                source.Replace("RunBlockQueueCore(state, common)", "UnreviewedBlockQueue(state, common)", StringComparison.Ordinal),
                source.Replace(call + "(", "UnreviewedAllocator.Call(", StringComparison.Ordinal) })
            {
                string rejected = changed;
                HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(ref rejected, call, out _));
            }
        }
    }

    [HostTest("EFI source admission evaluates only the supported inactive managed branch")]
    private static void Preprocessor()
    {
        const string active = "public static class C { public const ulong Value = ulong.MaxValue; }";
        foreach (string text in new[] { active, "#if !NOSTDLIB_EFI\nusing System; string secret;\n#endif\n" + active,
            "// using System;\n/* string managed; */\n" + active }) HostCheck.True(EfiSourceAdmission.Inspect(text, "ordinary.cs").Succeeded);
        foreach (string text in new[] { "#if OTHER\n#endif", "#if NOSTDLIB_EFI\n#endif", "#if !nostdlib_efi\n#endif", "#if\n#endif",
            "#if !NOSTDLIB_EFI\n#if !NOSTDLIB_EFI\n#endif\n#endif", "#if !NOSTDLIB_EFI\n#else\n#endif", "#endif", "#if !NOSTDLIB_EFI", "#nullable enable" })
            HostCheck.True(!EfiSourceAdmission.Inspect(text, "ordinary.cs").Succeeded, "unsupported condition rejected: " + text);
    }

    [HostTest("EFI source admission rejects every unsupported managed construct")]
    private static void ManagedSurface()
    {
        foreach (string code in new[] { @"int \u0041;", "int @escaped;", "Span<byte> value;", "[Flags] enum E { A }", "System.Console.WriteLine();",
            "namespace System { }", "using OS.Boot;", "string value;", "object value;", "int[] value;", "Type value;", "record R(int A);", "class C<T> {}",
            "void Generic<T>() {}", "where T : struct", "new { A = 1 }", "interface I {}", "[SomeAttribute] struct C {}", "try {} finally {}",
            "typeof(C)", "delegate void D();", "Task value;", "int.MaxValue", "class C {}", "static garbage class C {}" })
        {
            var result = EfiSourceAdmission.Inspect(code, "ordinary.cs");
            HostCheck.True(!result.Succeeded, "unsupported construct accepted: " + code);
            HostCheck.Equal("forbidden_active_source", result.Reason);
        }
        HostCheck.True(EfiSourceAdmission.Inspect("unsafe static class C { delegate* unmanaged<int, int> callback; }", "ordinary.cs").Succeeded);
    }

    [HostTest("EFI source admission binds reviewed production bytes to exact ordinal paths")]
    internal static void ReviewedIdentity()
    {
        const string name = @"..\OS.Kernel\Runtime\ObjectArrayStringEETypeMethodTableLayoutContracts.cs";
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.GetFullPath(name, Path.Combine(root, "src/OS"))).Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (string valid in new[] { source, source.Replace("\n", "\r\n", StringComparison.Ordinal) })
            HostCheck.True(EfiSourceAdmission.Inspect(valid, name).Succeeded);
        HostCheck.True(source.Contains(EfiContracts.NativePackageVersion, StringComparison.Ordinal));
        foreach (string changed in new[] { source + " ", source + "\r", source.Replace(EfiContracts.NativePackageVersion, "0.0.0-unreviewed", StringComparison.Ordinal), source + "\n// changed" })
            HostCheck.True(!EfiSourceAdmission.Inspect(changed, name).Succeeded);
        foreach (string other in new[] { @"..\OS.Kernel\Runtime\Other.cs", name + " ", name.ToLowerInvariant(), name.Replace('\\', '/'), name.Replace("Runtime\\", "Runtime\\.\\", StringComparison.Ordinal), name.Insert(10, "\0") })
            HostCheck.True(!EfiSourceAdmission.Inspect(source, other).Succeeded, "altered authority path " + other);
    }
}
