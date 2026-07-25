using System.Runtime.CompilerServices;
using OS.Kernel.Build;

internal static class WorkloadProfileTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(SelectedProfileTestName, TestSelectedProfileMatchesCompileSymbol, ref failures);
        Run("build.profile selects exactly one profile", TestSelectsExactlyOneProfile, ref failures);
        Run("build.profile records capability requirements", TestRecordsCapabilityRequirements, ref failures);
        Run(
            "workload profile single minimal api keeps provider gates distinct",
            TestSingleMinimalApiProviderGateBoundaries,
            ref failures);
        Run(
            "docs.kestrel provider scope present",
            TestKestrelProviderScope,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Workload profile host tests failed: " + failures);
        }
    }

#if OS_PROFILE_SINGLE_MINIMAL_API
    private const string SelectedProfileTestName = "build.profile selected SingleMinimalApi";
#elif OS_PROFILE_MULTI_MINIMAL_API
    private const string SelectedProfileTestName = "build.profile selected MultiMinimalApi";
#else
#error WorkloadProfileTests require one OS workload profile symbol.
#endif

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestSelectedProfileMatchesCompileSymbol()
    {
#if OS_PROFILE_SINGLE_MINIMAL_API
        return WorkloadProfile.SelectedKind == WorkloadProfileKind.SingleMinimalApi
            && WorkloadProfile.SelectedName == WorkloadProfile.SingleMinimalApiName
            && WorkloadProfile.IsSingleMinimalApi
            && !WorkloadProfile.IsMultiMinimalApi;
#elif OS_PROFILE_MULTI_MINIMAL_API
        return WorkloadProfile.SelectedKind == WorkloadProfileKind.MultiMinimalApi
            && WorkloadProfile.SelectedName == WorkloadProfile.MultiMinimalApiName
            && !WorkloadProfile.IsSingleMinimalApi
            && WorkloadProfile.IsMultiMinimalApi;
#endif
    }

    private static bool TestSelectsExactlyOneProfile()
    {
        return WorkloadProfile.ExactlyOneProfileSelected
            && WorkloadProfile.IsSingleMinimalApi != WorkloadProfile.IsMultiMinimalApi;
    }

    private static bool TestRecordsCapabilityRequirements()
    {
#if OS_PROFILE_SINGLE_MINIMAL_API
        return !WorkloadProfile.RequiresProcessTable
            && !WorkloadProfile.RequiresMultiAppManifest
            && !WorkloadProfile.SupportsMultipleMinimalApiApps;
#elif OS_PROFILE_MULTI_MINIMAL_API
        return WorkloadProfile.RequiresProcessTable
            && WorkloadProfile.RequiresMultiAppManifest
            && WorkloadProfile.SupportsMultipleMinimalApiApps;
#endif
    }

    private static bool TestSingleMinimalApiProviderGateBoundaries()
    {
        string root = FindRepositoryRoot();
        string workloadProfiles = File.ReadAllText(
            Path.Combine(root, "docs", "workload-profiles.md"));
        string completionGates = File.ReadAllText(
            Path.Combine(root, "docs", "completion-gates.md"));

        return ContainsAll(
                workloadProfiles,
                "SingleMinimalApi",
                "QEMU/libvirt Kestrel pass proves only local virtualization",
                "does not prove AWS Nitro support",
                "does not prove Azure VM support",
                "Nitro/ENA/NVMe/DHCP/TCP/EC2 serial evidence",
                "VMBus",
                "NetVSC",
                "StorVSC",
                "WireServer/goal-state")
            && ContainsAll(
                completionGates,
                "Gate 14 Provider Split",
                "QEMU/libvirt Kestrel claim",
                "AWS Nitro Kestrel claim",
                "Azure VM Kestrel claim",
                "AWS IMDSv2",
                "Azure WireServer/goal-state",
                "MultiMinimalApi");
    }

    private static bool TestKestrelProviderScope()
    {
        string root = FindRepositoryRoot();
        string kestrel = File.ReadAllText(Path.Combine(root, "docs", "kestrel.md"));
        string aws = File.ReadAllText(Path.Combine(root, "docs", "aws-cloud-requirements.md"));
        string azure = File.ReadAllText(Path.Combine(root, "docs", "azure-cloud-requirements.md"));

        return ContainsAll(
                kestrel,
                "Provider Scope",
                "Do not promote evidence across provider gates",
                "Local QEMU/libvirt Kestrel evidence",
                "AWS Nitro Kestrel evidence",
                "ENA network support",
                "NVMe root storage support",
                "Azure VM Kestrel evidence",
                "VMBus negotiation",
                "NetVSC network support",
                "StorVSC storage support")
            && ContainsAll(
                aws,
                "CloudReady claim boundary",
                "SingleMinimalApi",
                "ENA networking",
                "NVMe root storage",
                "DHCP IPv4",
                "TCP listener",
                "IMDSv2",
                "cloud-init-free")
            && ContainsAll(
                azure,
                "CloudReady claim boundary",
                "SingleMinimalApi",
                "VMBus negotiation",
                "NetVSC networking",
                "StorVSC storage",
                "DHCP IPv4",
                "TCP listener",
                "WireServer metadata",
                "goal-state");
    }

    private static bool ContainsAll(string text, params string[] required)
    {
        foreach (string value in required)
        {
            if (!text.Contains(value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing OS.slnx.");
    }
}
