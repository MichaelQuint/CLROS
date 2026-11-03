using OS.Tooling;

internal static class EfiSourceAdmissionTests
{
    [HostTest("EFI extracted source families reject changed or missing siblings without widening admission")]
    private static void ExtractedSourceFamilies()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string[] includes = EfiContracts.CompileItems.Select(item => item.Split('|')[0]).ToArray();
        EfiProjectAdmission.VerifySourceFamilyInventory(includes);
        foreach (string include in EfiContracts.SourceFamilyIdentities.Keys)
            EfiProjectAdmission.VerifySourceFamilyPin(include, File.ReadAllText(Path.GetFullPath(include, Path.Combine(root, "src/OS"))));

        string sibling = @"Boot\EfiEntry.M01StageOneAdapter.M01StageOneMemoryMapBuffer.cs";
        string original = File.ReadAllText(Path.GetFullPath(sibling, Path.Combine(root, "src/OS")));
        Reject(() => EfiProjectAdmission.VerifySourceFamilyPin(sibling, original + "\n// changed sibling\n"), "source_family_identity_mismatch");
        Reject(() => EfiProjectAdmission.VerifySourceFamilyInventory(includes.Where(include => include != sibling)), "source_family_member_missing");
        string generic = @"..\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorStateTransition.cs";
        HostCheck.True(EfiSourceAdmission.Inspect("public static class Plain {}", generic).Succeeded,
            "family integrity remains separate from the existing generic admission policy");
        HostCheck.True(!EfiSourceAdmission.Inspect("public sealed class Forbidden {}", generic).Succeeded,
            "family pin does not bypass generic source admission");

        static void Reject(Action action, string reason)
        {
            try
            {
                action();
            }
            catch (ToolFailure failure)
            {
                HostCheck.Equal(reason, failure.Reason);
                return;
            }
            HostCheck.True(false, reason);
        }
    }

    [HostTest("EFI allocator firewall admits shared queue ownership and rejects extra or redirected calls")]
    private static void AllocatorOwnership()
    {
        string source = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/Boot/EfiEntry.M03FirstExitAdapter.cs"));
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

    [HostTest("EFI timer allocator firewall rejects rogue calls, redirected ownership and mismatched receipts")]
    private static void TimerAllocatorOwnership()
    {
        string source = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/Boot/EfiEntry.M03FirstExitAdapter.cs"));
        int start = source.IndexOf("private static bool RunSchedulerTimer(", StringComparison.Ordinal);
        HostCheck.True(start >= 0);
        int end = source.IndexOf("private static bool TimerPrepare(", start, StringComparison.Ordinal);
        HostCheck.True(end > start);
        string ChangeTimer(string before, string after)
        {
            string body = source[start..end];
            HostCheck.True(body.Contains(before, StringComparison.Ordinal), "independent timer mutation target: " + before);
            return source[..start] + body.Replace(before, after, StringComparison.Ordinal) + source[end..];
        }
        foreach (string call in new[] { "BootPageAllocatorMutator.AllocateOnePage", "BootPageAllocatorMutator.FreeOnePage" })
        {
            foreach (string changed in new[] {
                source + "\n#if M09_005_PROBE\nstatic class RogueTimer { static void Run() { " + call + "(); } }\n#endif",
                ChangeTimer(call + "(allocator, &node", call + "(otherAllocator, &node"),
                ChangeTimer(call + "(", "RedirectedAllocator.Call("),
                ChangeTimer("BootPageAllocatorOnePageResult[22]", "BootPageAllocatorOnePageResult[23]"),
                source.Replace("state, &RunSchedulerTimer)", "state, &UnreviewedTimer)", StringComparison.Ordinal),
                source.Replace("state->OwnedPhase == 2, state->ApicPhase == 2,", "state->OwnedPhase == 2, state->ApicPhase == 1,", StringComparison.Ordinal),
                source.Replace("if (!BootSequence.RunOwnedApicSetup", "if (BootSequence.RunOwnedApicSetup", StringComparison.Ordinal),
                source.Replace("#if M09_005_PROBE", "#if M09_004_PROBE", StringComparison.Ordinal) })
            {
                string rejected = changed;
                HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(ref rejected, call, out _));
            }
            string wrongLoop = call.EndsWith(".AllocateOnePage", StringComparison.Ordinal)
                ? ChangeTimer("borrowed < 22", "borrowed < 23") : ChangeTimer("receipts[i - 1]", "receipts[0]");
            HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(ref wrongLoop, call, out _));
        }
        string admitted = source;
        HostCheck.True(EfiProfileTestControls.RemoveOwnedCooperativeMaterializer(ref admitted, out string reason), reason);
        HostCheck.True(!admitted.Contains("BootPageAllocatorStateMaterializer", StringComparison.Ordinal));
        foreach (string changed in new[] {
            source + "\n#if M09_005_PROBE\nstatic class RogueTimer { static void Run() { BootPageAllocatorStateMaterializer.Materialize(); } }\n#endif",
            ChangeTimer("Materialize(&node, 1, 1, 1", "Materialize(&node, 2, 1, 1"),
            ChangeTimer("BootPageAllocatorStateMaterializer.Materialize", "OtherMaterializer.Materialize"),
            ChangeTimer("pool + 32 * Page, 32, 32 * Page", "pool + 31 * Page, 32, 32 * Page"),
            ChangeTimer("&allocator, 1).Succeeded) return false;", "&allocator, 1).Succeeded) return true;") })
        {
            string rejected = changed;
            HostCheck.True(!EfiProfileTestControls.RemoveOwnedCooperativeMaterializer(ref rejected, out _));
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
