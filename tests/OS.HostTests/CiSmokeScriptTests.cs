using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal static class CiSmokeScriptTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string tempRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "logs",
            "ci-smoke-tests-" + Guid.NewGuid().ToString("N"));

        CiSmokeDryRunResult dryRun = RunCiSmokeDryRun(repositoryRoot, tempRoot);

        try
        {
            Run("ci.smoke.dryrun writes artifact plan", ref failures, () => TestWritesArtifactPlan(dryRun));
            Run("ci.smoke.dryrun includes serial and summary logs", ref failures, () => TestIncludesSerialAndSummaryLogs(dryRun));
            Run("ci.smoke.dryrun includes diagnostics marker preset", ref failures, () => TestIncludesDiagnosticsMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes app-args marker preset", ref failures, () => TestIncludesAppArgsMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes app-exit marker preset", ref failures, () => TestIncludesAppExitMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes cctor-preflight marker preset", ref failures, () => TestIncludesCctorPreflightMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes cctor-work-items marker preset", ref failures, () => TestIncludesCctorWorkItemsMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes fetch-http marker preset", ref failures, () => TestIncludesFetchHttpMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes fetch-http-endpoint marker preset", ref failures, () => TestIncludesFetchHttpEndpointMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes fetch-http-tcp-exchange marker preset", ref failures, () => TestIncludesFetchHttpTcpExchangeMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes http-static-response marker preset", ref failures, () => TestIncludesHttpStaticResponseMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes http static tcp response marker preset", ref failures, () => TestIncludesHttpStaticTcpResponseMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes virtio-rx-tcp-dispatch marker preset", ref failures, () => TestIncludesVirtioRxTcpDispatchMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes manifest-env marker preset", ref failures, () => TestIncludesManifestEnvMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes nativeaot-heap marker preset", ref failures, () => TestIncludesNativeAotHeapMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes pal-export marker preset", ref failures, () => TestIncludesPalExportMarkerPreset(dryRun));
            Run("ci.smoke.dryrun includes qcow2 smoke intent", ref failures, () => TestIncludesQcow2SmokeIntent(dryRun));
            Run("ci.smoke.dryrun records missing qemu-img discovery", ref failures, () => TestCiSmokeDryRunRecordsMissingQemuImg(repositoryRoot, tempRoot));
            Run("ci.smoke.dryrun records found qemu-img discovery", ref failures, () => TestCiSmokeDryRunRecordsFoundQemuImg(repositoryRoot, tempRoot));
            Run("ci.smoke.dryrun includes libvirt smoke intent", ref failures, () => TestIncludesLibvirtSmokeIntent(dryRun));
            Run("ci.smoke.dryrun includes git diff check", ref failures, () => TestIncludesGitDiffCheck(dryRun));
            Run("ci.smoke.dryrun records default workload profile", ref failures, () => TestRecordsDefaultWorkloadProfile(dryRun));
            Run("ci.smoke.dryrun propagates multi workload profile", ref failures, () => TestPropagatesMultiWorkloadProfile(repositoryRoot, tempRoot));
            Run("ci.smoke rejects invalid workload profile", ref failures, () => TestRejectsInvalidWorkloadProfile(repositoryRoot, tempRoot));
            Run("ci.smoke returns nonzero on failed command", ref failures, () => TestReturnsNonzeroOnFailedCommand(repositoryRoot, tempRoot));
            Run("baseline evidence script wires qcow2 and libvirt intent", ref failures, () => TestBaselineEvidenceScriptWiresQcow2AndLibvirt(repositoryRoot));
            Run("baseline evidence script enforces complete host gate", ref failures, () => TestBaselineEvidenceScriptEnforcesCompleteHostGate(repositoryRoot));
            Run("baseline evidence self-test rejects malformed terminals and summary", ref failures, () => TestBaselineEvidenceSelfTest(repositoryRoot, tempRoot));
            Run("baseline evidence script records artifact summary lanes", ref failures, () => TestBaselineEvidenceScriptRecordsArtifactSummary(repositoryRoot, tempRoot));
            Run("baseline evidence script rejects nonempty log root", ref failures, () => TestBaselineEvidenceScriptRejectsNonEmptyLogRoot(repositoryRoot, tempRoot));
            Run("baseline evidence plan records missing qemu-img discovery", ref failures, () => TestBaselineEvidencePlanRecordsMissingQemuImg(repositoryRoot, tempRoot));
            Run("baseline evidence plan records found qemu-img discovery", ref failures, () => TestBaselineEvidencePlanRecordsFoundQemuImg(repositoryRoot, tempRoot));
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Cleanup failure should not hide the script contract result.
            }
        }

        if (failures != 0)
        {
            throw new InvalidOperationException("CI smoke script tests failed: " + failures);
        }
    }

    private static void Run(string name, ref int failures, Func<bool> test)
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

    private static bool TestWritesArtifactPlan(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun writes artifact plan", StringComparison.Ordinal)
            || !File.Exists(dryRun.ArtifactPlanPath)
            || !File.Exists(dryRun.SummaryPath))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        JsonElement root = plan.RootElement;

        return root.GetProperty("schema").GetString() == "os.ci.smoke.artifacts.v0"
            && root.GetProperty("mode").GetString() == "dry-run"
            && root.GetProperty("workloadProfile").GetString() == "SingleMinimalApi"
            && root.GetProperty("logDirectory").GetString()!.Contains("artifacts", StringComparison.Ordinal)
            && HasStep(root, "ci.smoke.build", "dotnet build .\\CLROS.slnx --nologo -m:1")
            && HasStep(root, "ci.smoke.host.tests", "dotnet run --project .\\tests\\OS.HostTests\\OS.HostTests.csproj --no-build")
            && HasStep(root, "ci.smoke.qemu.dryrun", "-DryRun")
            && HasStep(root, "ci.smoke.nativeapp.dryrun", "-RequireNativeAppMarkers")
            && HasStep(root, "ci.smoke.diagnostics.dryrun", "-MarkerPreset diagnostics")
            && HasStep(root, "ci.smoke.app_args.dryrun", "-MarkerPreset app-args")
            && HasStep(root, "ci.smoke.app_exit.dryrun", "-MarkerPreset app-exit")
            && HasStep(root, "ci.smoke.cctor_preflight.dryrun", "-MarkerPreset cctor-preflight")
            && HasStep(root, "ci.smoke.cctor_work_items.dryrun", "-MarkerPreset cctor-work-items")
            && HasStep(root, "ci.smoke.fetch_http.dryrun", "-MarkerPreset fetch-http")
            && HasStep(root, "ci.smoke.fetch_http_endpoint.dryrun", "-MarkerPreset fetch-http-endpoint")
            && HasStep(root, "ci.smoke.fetch_http_tcp_exchange.dryrun", "-MarkerPreset fetch-http-tcp-exchange")
            && HasStep(root, "ci.smoke.http_static_response.dryrun", "-MarkerPreset http-static-response")
            && HasStep(root, "ci.smoke.http_static_tcp_response.dryrun", "-MarkerPreset http-static-tcp-response")
            && HasStep(root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "-MarkerPreset virtio-rx-tcp-dispatch")
            && HasStep(root, "ci.smoke.manifest_env.dryrun", "-MarkerPreset manifest-env")
            && HasStep(root, "ci.smoke.nativeaot_heap.dryrun", "-MarkerPreset nativeaot-heap")
            && HasStep(root, "ci.smoke.pal_export.dryrun", "-MarkerPreset pal-export")
            && HasStep(root, "ci.smoke.qemu.qcow2.dryrun", "run-qemu-qcow2-smoke.ps1")
            && HasStep(root, "ci.smoke.libvirt.dryrun", "run-libvirt-smoke.ps1")
            && HasStep(root, "ci.smoke.libvirt.missingtool", "-Virsh")
            && HasStep(root, "ci.smoke.git.diffcheck", "git diff --check");
    }

    private static bool TestIncludesSerialAndSummaryLogs(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes serial and summary logs", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        JsonElement root = plan.RootElement;

        return StepHasSerialAndSummary(root, "ci.smoke.qemu.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.nativeapp.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.diagnostics.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.app_args.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.app_exit.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.cctor_preflight.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.cctor_work_items.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.fetch_http.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.fetch_http_endpoint.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.fetch_http_tcp_exchange.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.http_static_response.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.http_static_tcp_response.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.manifest_env.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.nativeaot_heap.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.pal_export.dryrun")
            && StepHasSerialAndSummary(root, "ci.smoke.qemu.qcow2.dryrun")
            && QemuStepCommandContains(root, "ci.smoke.qemu.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.nativeapp.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.nativeapp.dryrun", "-RequireNativeAppMarkers")
            && QemuStepCommandContains(root, "ci.smoke.diagnostics.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.diagnostics.dryrun", "-MarkerPreset diagnostics")
            && QemuStepCommandContains(root, "ci.smoke.app_args.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.app_args.dryrun", "-MarkerPreset app-args")
            && QemuStepCommandContains(root, "ci.smoke.app_exit.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.app_exit.dryrun", "-MarkerPreset app-exit")
            && QemuStepCommandContains(root, "ci.smoke.cctor_preflight.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.cctor_preflight.dryrun", "-MarkerPreset cctor-preflight")
            && QemuStepCommandContains(root, "ci.smoke.cctor_work_items.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.cctor_work_items.dryrun", "-MarkerPreset cctor-work-items")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http.dryrun", "-MarkerPreset fetch-http")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http_endpoint.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http_endpoint.dryrun", "-MarkerPreset fetch-http-endpoint")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http_tcp_exchange.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http_tcp_exchange.dryrun", "-MarkerPreset fetch-http-tcp-exchange")
            && QemuStepCommandContains(root, "ci.smoke.http_static_response.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.http_static_response.dryrun", "-MarkerPreset http-static-response")
            && QemuStepCommandContains(root, "ci.smoke.http_static_tcp_response.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.http_static_tcp_response.dryrun", "-MarkerPreset http-static-tcp-response")
            && QemuStepCommandContains(root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "-MarkerPreset virtio-rx-tcp-dispatch")
            && QemuStepCommandContains(root, "ci.smoke.manifest_env.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.manifest_env.dryrun", "-MarkerPreset manifest-env")
            && QemuStepCommandContains(root, "ci.smoke.nativeaot_heap.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.nativeaot_heap.dryrun", "-MarkerPreset nativeaot-heap")
            && QemuStepCommandContains(root, "ci.smoke.pal_export.dryrun", "-DryRun")
            && QemuStepCommandContains(root, "ci.smoke.pal_export.dryrun", "-MarkerPreset pal-export")
            && QemuStepCommandContains(root, "ci.smoke.qemu.qcow2.dryrun", "-DryRun");
    }

    private static bool TestIncludesDiagnosticsMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes diagnostics marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        JsonElement root = plan.RootElement;

        return HasStep(root, "ci.smoke.diagnostics.dryrun", "run-qemu-smoke.ps1")
            && HasStep(root, "ci.smoke.diagnostics.dryrun", "-DryRun")
            && HasStep(root, "ci.smoke.diagnostics.dryrun", "-MarkerPreset diagnostics")
            && StepHasSerialAndSummary(root, "ci.smoke.diagnostics.dryrun")
            && StepHasMarkerPreset(root, "ci.smoke.diagnostics.dryrun", "diagnostics");
    }

    private static bool TestIncludesAppArgsMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes app-args marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.app_args.dryrun", "app-args", "qemu-app-args")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.app_args.dryrun", "app-args", "qemu-app-args");
    }

    private static bool TestIncludesAppExitMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes app-exit marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.app_exit.dryrun", "app-exit", "qemu-app-exit")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.app_exit.dryrun", "app-exit", "qemu-app-exit");
    }

    private static bool TestIncludesCctorPreflightMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes cctor-preflight marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.cctor_preflight.dryrun", "cctor-preflight", "qemu-cctor-preflight")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.cctor_preflight.dryrun", "cctor-preflight", "qemu-cctor-preflight");
    }

    private static bool TestIncludesCctorWorkItemsMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes cctor-work-items marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.cctor_work_items.dryrun", "cctor-work-items", "qemu-cctor-work-items")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.cctor_work_items.dryrun", "cctor-work-items", "qemu-cctor-work-items");
    }

    private static bool TestIncludesFetchHttpMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes fetch-http marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.fetch_http.dryrun", "fetch-http", "qemu-fetch-http")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.fetch_http.dryrun", "fetch-http", "qemu-fetch-http");
    }

    private static bool TestIncludesFetchHttpEndpointMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes fetch-http-endpoint marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.fetch_http_endpoint.dryrun", "fetch-http-endpoint", "qemu-fetch-http-endpoint")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.fetch_http_endpoint.dryrun", "fetch-http-endpoint", "qemu-fetch-http-endpoint");
    }

    private static bool TestIncludesFetchHttpTcpExchangeMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes fetch-http-tcp-exchange marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(
                plan.RootElement,
                "ci.smoke.fetch_http_tcp_exchange.dryrun",
                "fetch-http-tcp-exchange",
                "qemu-fetch-http-tcp-exchange")
            && StepHasMarkerLane(
                summary.RootElement,
                "ci.smoke.fetch_http_tcp_exchange.dryrun",
                "fetch-http-tcp-exchange",
                "qemu-fetch-http-tcp-exchange");
    }

    private static bool TestIncludesHttpStaticResponseMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes http-static-response marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(
                plan.RootElement,
                "ci.smoke.http_static_response.dryrun",
                "http-static-response",
                "qemu-http-static-response")
            && StepHasMarkerLane(
                summary.RootElement,
                "ci.smoke.http_static_response.dryrun",
                "http-static-response",
                "qemu-http-static-response");
    }

    private static bool TestIncludesHttpStaticTcpResponseMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes http static tcp response marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(
                plan.RootElement,
                "ci.smoke.http_static_tcp_response.dryrun",
                "http-static-tcp-response",
                "qemu-http-static-tcp-response")
            && StepHasMarkerLane(
                summary.RootElement,
                "ci.smoke.http_static_tcp_response.dryrun",
                "http-static-tcp-response",
                "qemu-http-static-tcp-response");
    }

    private static bool TestIncludesVirtioRxTcpDispatchMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes virtio-rx-tcp-dispatch marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(
                plan.RootElement,
                "ci.smoke.virtio_rx_tcp_dispatch.dryrun",
                "virtio-rx-tcp-dispatch",
                "qemu-virtio-rx-tcp-dispatch")
            && StepHasMarkerLane(
                summary.RootElement,
                "ci.smoke.virtio_rx_tcp_dispatch.dryrun",
                "virtio-rx-tcp-dispatch",
                "qemu-virtio-rx-tcp-dispatch");
    }

    private static bool TestIncludesManifestEnvMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes manifest-env marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.manifest_env.dryrun", "manifest-env", "qemu-manifest-env")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.manifest_env.dryrun", "manifest-env", "qemu-manifest-env");
    }

    private static bool TestIncludesNativeAotHeapMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes nativeaot-heap marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.nativeaot_heap.dryrun", "nativeaot-heap", "qemu-nativeaot-heap")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.nativeaot_heap.dryrun", "nativeaot-heap", "qemu-nativeaot-heap");
    }

    private static bool TestIncludesPalExportMarkerPreset(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes pal-export marker preset", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return StepHasMarkerLane(plan.RootElement, "ci.smoke.pal_export.dryrun", "pal-export", "qemu-pal-export")
            && StepHasMarkerLane(summary.RootElement, "ci.smoke.pal_export.dryrun", "pal-export", "qemu-pal-export");
    }

    private static bool TestIncludesQcow2SmokeIntent(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes qcow2 smoke intent", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        JsonElement root = plan.RootElement;

        return HasStep(root, "ci.smoke.qemu.qcow2.dryrun", "run-qemu-qcow2-smoke.ps1")
            && HasStep(root, "ci.smoke.qemu.qcow2.dryrun", "-DryRun")
            && HasStep(root, "ci.smoke.qemu.qcow2.dryrun", "-Qcow2Image")
            && StepHasImageAndDiskFormat(root, "ci.smoke.qemu.qcow2.dryrun", "qcow2")
            && StepHasSerialAndSummary(root, "ci.smoke.qemu.qcow2.dryrun")
            && root.TryGetProperty("qemuImgDiscovery", out _)
            && Qcow2ConversionIntentMatches(root, "dry-run", "planned", qemuImgRequired: false, qemuImgAvailable: root.GetProperty("qemuImgDiscovery").GetProperty("available").GetBoolean())
            && StepHasQcow2ConversionIntent(root, "ci.smoke.qemu.qcow2.dryrun", "dry-run", "planned", qemuImgRequired: false, qemuImgAvailable: root.GetProperty("qemuImgDiscovery").GetProperty("available").GetBoolean());
    }

    private static bool TestCiSmokeDryRunRecordsMissingQemuImg(string repositoryRoot, string tempRoot)
    {
        string ciRoot = Path.Combine(tempRoot, "ci-missing-qemu-img");
        Directory.CreateDirectory(ciRoot);
        string missingQemuImg = Path.Combine(ciRoot, "missing-qemu-img.exe");

        CiSmokeDryRunResult result = RunCiSmoke(
            repositoryRoot,
            ciRoot,
            "-DryRun",
            "-RunQcow2RealSmoke",
            "-QemuImg",
            missingQemuImg);

        if (result.ExitCode != 0
            || !result.Output.Contains("TEST_PASS ci.smoke.qemu_img.discovery", StringComparison.Ordinal)
            || !result.Output.Contains("CI_SMOKE_QEMU_IMG_DISCOVERY requested=" + missingQemuImg + " source=configured available=false resolved=not_found", StringComparison.Ordinal)
            || !File.Exists(result.ArtifactPlanPath)
            || !File.Exists(result.SummaryPath))
        {
            PrintDryRunFailure(result);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(result.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(result.SummaryPath));

        return QemuImgDiscoveryMatches(plan.RootElement, expectedAvailable: false, expectedSource: "configured")
            && Qcow2ConversionIntentMatches(plan.RootElement, "real", "blocked_by_missing_qemu_img", qemuImgRequired: true, qemuImgAvailable: false)
            && StepHasQemuImgDiscovery(plan.RootElement, "ci.smoke.qemu.qcow2.real", expectedAvailable: false, expectedSource: "configured")
            && StepHasQcow2ConversionIntent(plan.RootElement, "ci.smoke.qemu.qcow2.real", "real", "blocked_by_missing_qemu_img", qemuImgRequired: true, qemuImgAvailable: false)
            && QemuImgDiscoveryMatches(summary.RootElement, expectedAvailable: false, expectedSource: "configured")
            && Qcow2ConversionIntentMatches(summary.RootElement, "real", "blocked_by_missing_qemu_img", qemuImgRequired: true, qemuImgAvailable: false)
            && StepHasQemuImgDiscovery(summary.RootElement, "ci.smoke.qemu.qcow2.real", expectedAvailable: false, expectedSource: "configured")
            && StepHasQcow2ConversionIntent(summary.RootElement, "ci.smoke.qemu.qcow2.real", "real", "blocked_by_missing_qemu_img", qemuImgRequired: true, qemuImgAvailable: false);
    }

    private static bool TestCiSmokeDryRunRecordsFoundQemuImg(string repositoryRoot, string tempRoot)
    {
        string ciRoot = Path.Combine(tempRoot, "ci-found-qemu-img");
        Directory.CreateDirectory(ciRoot);
        string fakeQemuImg = Path.Combine(ciRoot, "qemu-img.exe");
        string expectedResolvedPath = Path.GetRelativePath(repositoryRoot, fakeQemuImg);
        File.WriteAllText(fakeQemuImg, string.Empty);

        CiSmokeDryRunResult result = RunCiSmoke(
            repositoryRoot,
            ciRoot,
            "-DryRun",
            "-RunQcow2RealSmoke",
            "-QemuImg",
            fakeQemuImg);

        if (result.ExitCode != 0
            || !result.Output.Contains("TEST_PASS ci.smoke.qemu_img.discovery", StringComparison.Ordinal)
            || !result.Output.Contains("CI_SMOKE_QEMU_IMG_DISCOVERY requested=" + fakeQemuImg + " source=configured available=true resolved=" + expectedResolvedPath, StringComparison.Ordinal)
            || !File.Exists(result.ArtifactPlanPath)
            || !File.Exists(result.SummaryPath))
        {
            PrintDryRunFailure(result);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(result.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(result.SummaryPath));

        return QemuImgDiscoveryMatches(plan.RootElement, expectedAvailable: true, expectedSource: "configured")
            && Qcow2ConversionIntentMatches(plan.RootElement, "real", "planned", qemuImgRequired: true, qemuImgAvailable: true)
            && StepHasQemuImgDiscovery(plan.RootElement, "ci.smoke.qemu.qcow2.real", expectedAvailable: true, expectedSource: "configured")
            && StepHasQcow2ConversionIntent(plan.RootElement, "ci.smoke.qemu.qcow2.real", "real", "planned", qemuImgRequired: true, qemuImgAvailable: true)
            && QemuImgDiscoveryMatches(summary.RootElement, expectedAvailable: true, expectedSource: "configured")
            && Qcow2ConversionIntentMatches(summary.RootElement, "real", "planned", qemuImgRequired: true, qemuImgAvailable: true)
            && StepHasQemuImgDiscovery(summary.RootElement, "ci.smoke.qemu.qcow2.real", expectedAvailable: true, expectedSource: "configured")
            && StepHasQcow2ConversionIntent(summary.RootElement, "ci.smoke.qemu.qcow2.real", "real", "planned", qemuImgRequired: true, qemuImgAvailable: true);
    }

    private static bool TestIncludesLibvirtSmokeIntent(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes libvirt smoke intent", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        JsonElement root = plan.RootElement;

        return HasStep(root, "ci.smoke.libvirt.dryrun", "run-libvirt-smoke.ps1")
            && HasStep(root, "ci.smoke.libvirt.dryrun", "-DryRun")
            && HasStep(root, "ci.smoke.libvirt.missingtool", "-Virsh")
            && StepHasDomainXml(root, "ci.smoke.libvirt.dryrun")
            && StepHasDomainXml(root, "ci.smoke.libvirt.missingtool")
            && StepHasImageAndDiskFormat(root, "ci.smoke.libvirt.dryrun", "qcow2")
            && StepHasImageAndDiskFormat(root, "ci.smoke.libvirt.missingtool", "qcow2");
    }

    private static bool TestIncludesGitDiffCheck(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("TEST_PASS ci.smoke.dryrun includes git diff check", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return HasStep(plan.RootElement, "ci.smoke.git.diffcheck", "git diff --check")
            && StepHasLog(plan.RootElement, "ci.smoke.git.diffcheck", "git-diff-check.log")
            && HasStep(summary.RootElement, "ci.smoke.git.diffcheck", "git diff --check")
            && StepHasLog(summary.RootElement, "ci.smoke.git.diffcheck", "git-diff-check.log");
    }

    private static bool TestRecordsDefaultWorkloadProfile(CiSmokeDryRunResult dryRun)
    {
        if (dryRun.ExitCode != 0
            || !dryRun.Output.Contains("WORKLOAD_PROFILE SingleMinimalApi", StringComparison.Ordinal)
            || !dryRun.Output.Contains("TEST_PASS workload.profile.tooling default single", StringComparison.Ordinal))
        {
            PrintDryRunFailure(dryRun);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(dryRun.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(dryRun.SummaryPath));

        return plan.RootElement.GetProperty("workloadProfile").GetString() == "SingleMinimalApi"
            && summary.RootElement.GetProperty("workloadProfile").GetString() == "SingleMinimalApi"
            && AllStepsHaveWorkloadProfile(plan.RootElement, "SingleMinimalApi")
            && HasStep(plan.RootElement, "ci.smoke.build", "/p:OSWorkloadProfile=SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.qemu.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.diagnostics.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.app_args.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.app_exit.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.cctor_preflight.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.cctor_work_items.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.fetch_http.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.fetch_http_endpoint.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.fetch_http_tcp_exchange.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.http_static_response.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.http_static_tcp_response.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.manifest_env.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.nativeaot_heap.dryrun", "-WorkloadProfile SingleMinimalApi")
            && QemuStepCommandContains(plan.RootElement, "ci.smoke.qemu.qcow2.dryrun", "-WorkloadProfile SingleMinimalApi");
    }

    private static bool TestPropagatesMultiWorkloadProfile(string repositoryRoot, string tempRoot)
    {
        string multiRoot = Path.Combine(tempRoot, "multi-profile");
        Directory.CreateDirectory(multiRoot);

        CiSmokeDryRunResult result = RunCiSmoke(
            repositoryRoot,
            multiRoot,
            "-DryRun",
            "-WorkloadProfile",
            "MultiMinimalApi");

        if (result.ExitCode != 0
            || !result.Output.Contains("WORKLOAD_PROFILE MultiMinimalApi", StringComparison.Ordinal)
            || !result.Output.Contains("TEST_PASS workload.profile.tooling propagates multi", StringComparison.Ordinal)
            || !File.Exists(result.ArtifactPlanPath)
            || !File.Exists(result.SummaryPath))
        {
            PrintDryRunFailure(result);
            return false;
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(result.ArtifactPlanPath));
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(result.SummaryPath));
        JsonElement root = plan.RootElement;

        return root.GetProperty("workloadProfile").GetString() == "MultiMinimalApi"
            && summary.RootElement.GetProperty("workloadProfile").GetString() == "MultiMinimalApi"
            && AllStepsHaveWorkloadProfile(root, "MultiMinimalApi")
            && HasStep(root, "ci.smoke.build", "/p:OSWorkloadProfile=MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.qemu.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.nativeapp.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.diagnostics.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.app_args.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.app_exit.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.cctor_preflight.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.cctor_work_items.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http_endpoint.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.fetch_http_tcp_exchange.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.http_static_response.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.http_static_tcp_response.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.manifest_env.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.nativeaot_heap.dryrun", "-WorkloadProfile MultiMinimalApi")
            && QemuStepCommandContains(root, "ci.smoke.qemu.qcow2.dryrun", "-WorkloadProfile MultiMinimalApi");
    }

    private static bool TestRejectsInvalidWorkloadProfile(string repositoryRoot, string tempRoot)
    {
        string invalidRoot = Path.Combine(tempRoot, "invalid-profile");
        Directory.CreateDirectory(invalidRoot);

        CiSmokeDryRunResult result = RunCiSmoke(
            repositoryRoot,
            invalidRoot,
            "-DryRun",
            "-WorkloadProfile",
            "BadProfile");

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL workload.profile.tooling rejects invalid profile=BadProfile", StringComparison.Ordinal)
            && !File.Exists(result.ArtifactPlanPath)
            && !File.Exists(result.SummaryPath);
    }

    private static bool TestReturnsNonzeroOnFailedCommand(string repositoryRoot, string tempRoot)
    {
        string failureRoot = Path.Combine(tempRoot, "failed-command");
        Directory.CreateDirectory(failureRoot);

        string fakeDotNet = Path.Combine(failureRoot, "dotnet-fails.cmd");
        File.WriteAllLines(
            fakeDotNet,
            [
                "@echo off",
                "echo TEST_FAIL fake.dotnet forced_failure",
                "exit /b 42"
            ]);

        CiSmokeDryRunResult result = RunCiSmoke(
            repositoryRoot,
            failureRoot,
            "-DotNet",
            fakeDotNet);

        if (result.ExitCode != 1
            || !result.Output.Contains("TEST_FAIL ci.smoke failures=2", StringComparison.Ordinal)
            || !File.Exists(result.SummaryPath))
        {
            PrintDryRunFailure(result);
            return false;
        }

        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(result.SummaryPath));
        JsonElement root = summary.RootElement;
        return root.GetProperty("schema").GetString() == "os.ci.smoke.summary.v0"
            && root.GetProperty("status").GetString() == "failed"
            && StepHasExitCode(root, "ci.smoke.build", 42)
            && StepHasExitCode(root, "ci.smoke.host.tests", 42)
            && StepHasExitCode(root, "ci.smoke.qemu.dryrun", 0)
            && StepHasExitCode(root, "ci.smoke.nativeapp.dryrun", 0)
            && StepHasExitCode(root, "ci.smoke.diagnostics.dryrun", 0)
            && StepHasExitCode(root, "ci.smoke.app_args.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.app_args.dryrun", "QEMU_MARKER_PRESETS app-args")
            && StepLogContains(repositoryRoot, root, "ci.smoke.app_args.dryrun", "MARKER_PRESETS app-args")
            && StepLogContains(repositoryRoot, root, "ci.smoke.app_args.dryrun", "APP_ARGS_VECTOR_READY")
            && StepHasMarkerPreset(root, "ci.smoke.app_args.dryrun", "app-args")
            && StepHasExitCode(root, "ci.smoke.app_exit.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.app_exit.dryrun", "QEMU_MARKER_PRESETS app-exit")
            && StepLogContains(repositoryRoot, root, "ci.smoke.app_exit.dryrun", "MARKER_PRESETS app-exit")
            && StepLogContains(repositoryRoot, root, "ci.smoke.app_exit.dryrun", "APP_EXIT_OBSERVER_READY")
            && StepHasMarkerPreset(root, "ci.smoke.app_exit.dryrun", "app-exit")
            && StepHasExitCode(root, "ci.smoke.cctor_preflight.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.cctor_preflight.dryrun", "QEMU_MARKER_PRESETS cctor-preflight")
            && StepLogContains(repositoryRoot, root, "ci.smoke.cctor_preflight.dryrun", "MARKER_PRESETS cctor-preflight")
            && StepLogContains(repositoryRoot, root, "ci.smoke.cctor_preflight.dryrun", "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY")
            && StepHasMarkerPreset(root, "ci.smoke.cctor_preflight.dryrun", "cctor-preflight")
            && StepHasExitCode(root, "ci.smoke.cctor_work_items.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.cctor_work_items.dryrun", "QEMU_MARKER_PRESETS cctor-work-items")
            && StepLogContains(repositoryRoot, root, "ci.smoke.cctor_work_items.dryrun", "MARKER_PRESETS cctor-work-items")
            && StepLogContains(repositoryRoot, root, "ci.smoke.cctor_work_items.dryrun", "NATIVEAOT_CCTOR_WORK_ITEMS_READY")
            && StepHasMarkerPreset(root, "ci.smoke.cctor_work_items.dryrun", "cctor-work-items")
            && StepHasExitCode(root, "ci.smoke.fetch_http.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http.dryrun", "QEMU_MARKER_PRESETS fetch-http")
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http.dryrun", "MARKER_PRESETS fetch-http")
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http.dryrun", "FETCH_APP_HTTP_COMPLETION_READY")
            && StepHasMarkerPreset(root, "ci.smoke.fetch_http.dryrun", "fetch-http")
            && StepHasExitCode(root, "ci.smoke.fetch_http_endpoint.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http_endpoint.dryrun", "QEMU_MARKER_PRESETS fetch-http-endpoint")
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http_endpoint.dryrun", "MARKER_PRESETS fetch-http-endpoint")
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http_endpoint.dryrun", "FETCHAPP_HTTP_ENDPOINT_READY")
            && StepHasMarkerPreset(root, "ci.smoke.fetch_http_endpoint.dryrun", "fetch-http-endpoint")
            && StepHasExitCode(root, "ci.smoke.fetch_http_tcp_exchange.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http_tcp_exchange.dryrun", "QEMU_MARKER_PRESETS fetch-http-tcp-exchange")
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http_tcp_exchange.dryrun", "MARKER_PRESETS fetch-http-tcp-exchange")
            && StepLogContains(repositoryRoot, root, "ci.smoke.fetch_http_tcp_exchange.dryrun", "FETCH_APP_HTTP_TCP_EXCHANGE_READY")
            && StepHasMarkerPreset(root, "ci.smoke.fetch_http_tcp_exchange.dryrun", "fetch-http-tcp-exchange")
            && StepHasExitCode(root, "ci.smoke.http_static_response.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.http_static_response.dryrun", "QEMU_MARKER_PRESETS http-static-response")
            && StepLogContains(repositoryRoot, root, "ci.smoke.http_static_response.dryrun", "MARKER_PRESETS http-static-response")
            && StepLogContains(repositoryRoot, root, "ci.smoke.http_static_response.dryrun", "HTTP_STATIC_RESPONSE_READY")
            && StepLogContains(repositoryRoot, root, "ci.smoke.http_static_response.dryrun", "HTTP_STATIC_RESPONSE")
            && StepHasMarkerPreset(root, "ci.smoke.http_static_response.dryrun", "http-static-response")
            && StepHasExitCode(root, "ci.smoke.http_static_tcp_response.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.http_static_tcp_response.dryrun", "QEMU_MARKER_PRESETS http-static-tcp-response")
            && StepLogContains(repositoryRoot, root, "ci.smoke.http_static_tcp_response.dryrun", "MARKER_PRESETS http-static-tcp-response")
            && StepLogContains(repositoryRoot, root, "ci.smoke.http_static_tcp_response.dryrun", "HTTP_STATIC_TCP_RESPONSE_READY")
            && StepLogContains(repositoryRoot, root, "ci.smoke.http_static_tcp_response.dryrun", "HTTP_STATIC_TCP_RESPONSE")
            && StepHasMarkerPreset(root, "ci.smoke.http_static_tcp_response.dryrun", "http-static-tcp-response")
            && StepHasExitCode(root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "QEMU_MARKER_PRESETS virtio-rx-tcp-dispatch")
            && StepLogContains(repositoryRoot, root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "MARKER_PRESETS virtio-rx-tcp-dispatch")
            && StepLogContains(repositoryRoot, root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "VIRTIO_NET_RX_TCP_DISPATCH_READY")
            && StepLogContains(repositoryRoot, root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "VIRTIO_NET_RX_TCP_DISPATCH")
            && StepHasMarkerPreset(root, "ci.smoke.virtio_rx_tcp_dispatch.dryrun", "virtio-rx-tcp-dispatch")
            && StepHasExitCode(root, "ci.smoke.manifest_env.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.manifest_env.dryrun", "QEMU_MARKER_PRESETS manifest-env")
            && StepLogContains(repositoryRoot, root, "ci.smoke.manifest_env.dryrun", "MARKER_PRESETS manifest-env")
            && StepLogContains(repositoryRoot, root, "ci.smoke.manifest_env.dryrun", "NATIVE_MANIFEST_ENV_READY")
            && StepHasMarkerPreset(root, "ci.smoke.manifest_env.dryrun", "manifest-env")
            && StepHasExitCode(root, "ci.smoke.nativeaot_heap.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.nativeaot_heap.dryrun", "QEMU_MARKER_PRESETS nativeaot-heap")
            && StepLogContains(repositoryRoot, root, "ci.smoke.nativeaot_heap.dryrun", "MARKER_PRESETS nativeaot-heap")
            && StepLogContains(repositoryRoot, root, "ci.smoke.nativeaot_heap.dryrun", "NATIVEAOT_HEAP_BOOTSTRAP_READY")
            && StepHasMarkerPreset(root, "ci.smoke.nativeaot_heap.dryrun", "nativeaot-heap")
            && StepHasExitCode(root, "ci.smoke.pal_export.dryrun", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.pal_export.dryrun", "QEMU_MARKER_PRESETS pal-export")
            && StepLogContains(repositoryRoot, root, "ci.smoke.pal_export.dryrun", "MARKER_PRESETS pal-export")
            && StepLogContains(repositoryRoot, root, "ci.smoke.pal_export.dryrun", "PAL_EXPORT_CATALOG_READY")
            && StepHasMarkerPreset(root, "ci.smoke.pal_export.dryrun", "pal-export")
            && StepLogContains(repositoryRoot, root, "ci.smoke.diagnostics.dryrun", "QEMU_MARKER_PRESETS diagnostics")
            && StepLogContains(repositoryRoot, root, "ci.smoke.diagnostics.dryrun", "MARKER_PRESETS diagnostics")
            && StepLogContains(repositoryRoot, root, "ci.smoke.diagnostics.dryrun", "DIAGNOSTICS_CHAIN_READY")
            && StepLogContains(repositoryRoot, root, "ci.smoke.diagnostics.dryrun", "ORDERED_MARKERS FAULT_INFRA_READY;EARLY_DIAGNOSTICS_READY;DIAGNOSTIC_STUBS_MATERIALIZED;CONTROLLED_FAULT_PROBES_READY;CPU_FAULT_PANIC_READY;DIAGNOSTICS_CHAIN_READY;DIAGNOSTIC_STUB_INVOCATION_READY")
            && StepHasExitCode(root, "ci.smoke.qemu.qcow2.dryrun", 0)
            && StepHasExitCode(root, "ci.smoke.libvirt.dryrun", 0)
            && StepHasExitCode(root, "ci.smoke.libvirt.missingtool", 0)
            && StepHasExitCode(root, "ci.smoke.git.diffcheck", 0)
            && StepLogContains(repositoryRoot, root, "ci.smoke.git.diffcheck", "TEST_PASS ci.smoke.git.diffcheck");
    }

    private static bool TestBaselineEvidenceScriptWiresQcow2AndLibvirt(string repositoryRoot)
    {
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "run-baseline-evidence.ps1"));

        return script.Contains("baseline.qemu.qcow2.dryrun", StringComparison.Ordinal)
            && script.Contains("baseline.qemu.qcow2.real", StringComparison.Ordinal)
            && script.Contains("baseline.libvirt.dryrun", StringComparison.Ordinal)
            && script.Contains("baseline.libvirt.missingtool", StringComparison.Ordinal)
            && script.Contains("run-qemu-qcow2-smoke.ps1", StringComparison.Ordinal)
            && script.Contains("run-libvirt-smoke.ps1", StringComparison.Ordinal)
            && CountOccurrences(script, "\"-WorkloadProfile\"") >= 4
            && script.Contains("TEST_PASS baseline.smoke.intent_artifacts", StringComparison.Ordinal)
            && script.Contains("-Virsh", StringComparison.Ordinal);
    }

    private static bool TestBaselineEvidenceScriptEnforcesCompleteHostGate(
        string repositoryRoot)
    {
        string script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "run-baseline-evidence.ps1"));

        return script.Contains("baseline.host.build", StringComparison.Ordinal)
            && script.Contains("\"--nologo\"", StringComparison.Ordinal)
            && script.Contains("\"-m:1\"", StringComparison.Ordinal)
            && script.Contains("\"--no-build\"", StringComparison.Ordinal)
            && script.Contains("baseline.git.diffcheck", StringComparison.Ordinal)
            && script.Contains(
                "(?m)^TEST_PASS host\\.tests\\r?$",
                StringComparison.Ordinal)
            && script.Contains(
                "(?m)^TEST_FAIL host\\.tests(?:\\s.*)?\\r?$",
                StringComparison.Ordinal)
            && script.Contains(
                "TEST_PASS baseline.host.tests.terminal",
                StringComparison.Ordinal)
            && script.Contains(
                "TEST_PASS baseline.evidence",
                StringComparison.Ordinal);
    }

    private static bool TestBaselineEvidenceSelfTest(
        string repositoryRoot,
        string tempRoot)
    {
        string selfTestRoot = Path.Combine(tempRoot, "baseline-selftest");
        Directory.CreateDirectory(selfTestRoot);

        BaselineEvidencePlanResult result = RunBaselineEvidenceSelfTest(
            repositoryRoot,
            selfTestRoot);

        string[] requiredMarkers =
        [
            "TEST_START baseline.evidence.selftest",
            "TEST_PASS baseline.evidence.selftest missing_host_log",
            "TEST_PASS baseline.evidence.selftest single_host_pass",
            "TEST_PASS baseline.evidence.selftest duplicate_host_pass",
            "TEST_PASS baseline.evidence.selftest conflicting_host_terminal",
            "TEST_PASS baseline.evidence.selftest summary_mismatch",
            "TEST_PASS baseline.evidence.selftest malformed_summary",
            "TEST_PASS baseline.evidence.selftest"
        ];

        if (result.ExitCode != 0
            || result.Output.Contains(
                "TEST_FAIL baseline.evidence.selftest",
                StringComparison.Ordinal))
        {
            return false;
        }

        string[] outputLines = result.Output.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string marker in requiredMarkers)
        {
            int count = 0;
            foreach (string line in outputLines)
            {
                if (line.Equals(marker, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            if (count != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestBaselineEvidenceScriptRecordsArtifactSummary(string repositoryRoot, string tempRoot)
    {
        string baselineRoot = Path.Combine(tempRoot, "baseline-plan");
        Directory.CreateDirectory(baselineRoot);

        BaselineEvidencePlanResult result = RunBaselineEvidencePlanOnly(
            repositoryRoot,
            baselineRoot,
            "-WorkloadProfile",
            "MultiMinimalApi");

        if (result.ExitCode != 0
            || !result.Output.Contains("TEST_PASS baseline.artifact_summary", StringComparison.Ordinal)
            || !File.Exists(result.SummaryPath))
        {
            PrintBaselinePlanFailure(result);
            return false;
        }

        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(result.SummaryPath));
        JsonElement root = summary.RootElement;

        return root.GetProperty("schema").GetString() == "os.baseline.evidence.summary.v0"
            && root.GetProperty("status").GetString() == "planned"
            && root.GetProperty("mode").GetString() == "plan-only"
            && root.GetProperty("workloadProfile").GetString() == "MultiMinimalApi"
            && AllStepsHaveWorkloadProfile(root, "MultiMinimalApi")
            && HasStep(
                root,
                "baseline.build",
                "dotnet build .\\CLROS.slnx --nologo -m:1")
            && HasStep(
                root,
                "baseline.host.build",
                "dotnet build .\\tests\\OS.HostTests\\OS.HostTests.csproj --nologo")
            && HasStep(
                root,
                "baseline.host.tests",
                "dotnet run --project .\\tests\\OS.HostTests\\OS.HostTests.csproj --no-build")
            && HasStep(
                root,
                "baseline.git.diffcheck",
                "git diff --check")
            && HasStep(
                root,
                "baseline.git.status",
                "git status --short")
            && HasStep(root, "baseline.qemu.diagnostics.dryrun", "-MarkerPreset diagnostics")
            && StepHasSerialAndSummaryArtifact(root, "baseline.qemu.diagnostics.dryrun")
            && StepHasMarkerPreset(root, "baseline.qemu.diagnostics.dryrun", "diagnostics")
            && HasStep(root, "baseline.qemu.qcow2.dryrun", "run-qemu-qcow2-smoke.ps1")
            && StepHasImageAndDiskFormat(root, "baseline.qemu.qcow2.dryrun", "qcow2")
            && StepHasSerialAndSummaryArtifact(root, "baseline.qemu.qcow2.dryrun")
            && root.TryGetProperty("qemuImgDiscovery", out _)
            && Qcow2ConversionIntentMatches(root, "dry-run", "planned", qemuImgRequired: false, qemuImgAvailable: root.GetProperty("qemuImgDiscovery").GetProperty("available").GetBoolean())
            && StepHasQcow2ConversionIntent(root, "baseline.qemu.qcow2.dryrun", "dry-run", "planned", qemuImgRequired: false, qemuImgAvailable: root.GetProperty("qemuImgDiscovery").GetProperty("available").GetBoolean())
            && HasStep(root, "baseline.libvirt.dryrun", "run-libvirt-smoke.ps1")
            && HasStep(root, "baseline.libvirt.missingtool", "-Virsh")
            && StepHasImageAndDiskFormat(root, "baseline.libvirt.dryrun", "qcow2")
            && StepHasImageAndDiskFormat(root, "baseline.libvirt.missingtool", "qcow2")
            && StepHasSerialAndSummaryArtifact(root, "baseline.libvirt.dryrun")
            && StepHasSerialAndSummaryArtifact(root, "baseline.libvirt.missingtool")
            && StepHasDomainXml(root, "baseline.libvirt.dryrun")
            && StepHasDomainXml(root, "baseline.libvirt.missingtool");
    }

    private static bool TestBaselineEvidenceScriptRejectsNonEmptyLogRoot(
        string repositoryRoot,
        string tempRoot)
    {
        string baselineRoot = Path.Combine(tempRoot, "baseline-nonempty");
        Directory.CreateDirectory(baselineRoot);
        File.WriteAllText(Path.Combine(baselineRoot, "stale.log"), "stale");

        BaselineEvidencePlanResult result = RunBaselineEvidencePlanOnly(
            repositoryRoot,
            baselineRoot);

        return result.ExitCode != 0
            && result.Output.Contains(
                "Evidence log directory must be new or empty:",
                StringComparison.Ordinal);
    }

    private static bool TestBaselineEvidencePlanRecordsMissingQemuImg(string repositoryRoot, string tempRoot)
    {
        string baselineRoot = Path.Combine(tempRoot, "baseline-missing-qemu-img");
        Directory.CreateDirectory(baselineRoot);
        string missingQemuImg = Path.Combine(baselineRoot, "missing-qemu-img.exe");

        BaselineEvidencePlanResult result = RunBaselineEvidencePlanOnly(
            repositoryRoot,
            baselineRoot,
            "-RunQcow2RealSmoke",
            "-QemuImg",
            missingQemuImg);

        if (result.ExitCode != 0
            || !result.Output.Contains("TEST_PASS baseline.qemu_img.discovery", StringComparison.Ordinal)
            || !result.Output.Contains("BASELINE_QEMU_IMG_DISCOVERY requested=" + missingQemuImg + " source=configured available=false resolved=not_found", StringComparison.Ordinal)
            || !File.Exists(result.SummaryPath))
        {
            PrintBaselinePlanFailure(result);
            return false;
        }

        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(result.SummaryPath));
        JsonElement root = summary.RootElement;

        return QemuImgDiscoveryMatches(root, expectedAvailable: false, expectedSource: "configured")
            && Qcow2ConversionIntentMatches(root, "real", "blocked_by_missing_qemu_img", qemuImgRequired: true, qemuImgAvailable: false)
            && StepHasQemuImgDiscovery(root, "baseline.qemu.qcow2.real", expectedAvailable: false, expectedSource: "configured")
            && StepHasQcow2ConversionIntent(root, "baseline.qemu.qcow2.real", "real", "blocked_by_missing_qemu_img", qemuImgRequired: true, qemuImgAvailable: false);
    }

    private static bool TestBaselineEvidencePlanRecordsFoundQemuImg(string repositoryRoot, string tempRoot)
    {
        string baselineRoot = Path.Combine(tempRoot, "baseline-found-qemu-img");
        Directory.CreateDirectory(baselineRoot);
        string toolRoot = Path.Combine(tempRoot, "baseline-found-qemu-img-tool");
        Directory.CreateDirectory(toolRoot);
        string fakeQemuImg = Path.Combine(toolRoot, "qemu-img.exe");
        string expectedResolvedPath = Path.GetRelativePath(repositoryRoot, fakeQemuImg);
        File.WriteAllText(fakeQemuImg, string.Empty);

        BaselineEvidencePlanResult result = RunBaselineEvidencePlanOnly(
            repositoryRoot,
            baselineRoot,
            "-RunQcow2RealSmoke",
            "-QemuImg",
            fakeQemuImg);

        if (result.ExitCode != 0
            || !result.Output.Contains("TEST_PASS baseline.qemu_img.discovery", StringComparison.Ordinal)
            || !result.Output.Contains("BASELINE_QEMU_IMG_DISCOVERY requested=" + fakeQemuImg + " source=configured available=true resolved=" + expectedResolvedPath, StringComparison.Ordinal)
            || !File.Exists(result.SummaryPath))
        {
            PrintBaselinePlanFailure(result);
            return false;
        }

        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(result.SummaryPath));
        JsonElement root = summary.RootElement;

        return QemuImgDiscoveryMatches(root, expectedAvailable: true, expectedSource: "configured")
            && Qcow2ConversionIntentMatches(root, "real", "planned", qemuImgRequired: true, qemuImgAvailable: true)
            && StepHasQemuImgDiscovery(root, "baseline.qemu.qcow2.real", expectedAvailable: true, expectedSource: "configured")
            && StepHasQcow2ConversionIntent(root, "baseline.qemu.qcow2.real", "real", "planned", qemuImgRequired: true, qemuImgAvailable: true);
    }

    private static bool HasStep(JsonElement root, string name, string commandFragment)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string command = step.GetProperty("command").GetString() ?? string.Empty;
                string log = step.GetProperty("log").GetString() ?? string.Empty;
                return command.Contains(commandFragment, StringComparison.Ordinal)
                    && log.EndsWith(".log", StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool StepHasSerialAndSummary(JsonElement root, string name)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string serialLog = step.GetProperty("serialLog").GetString() ?? string.Empty;
                string summaryLog = step.GetProperty("summaryLog").GetString() ?? string.Empty;
                string log = step.GetProperty("log").GetString() ?? string.Empty;
                return log.Contains("qemu", StringComparison.Ordinal)
                    && serialLog.EndsWith(".log", StringComparison.Ordinal)
                    && summaryLog.EndsWith(".log", StringComparison.Ordinal)
                    && serialLog.Contains("serial", StringComparison.Ordinal)
                    && summaryLog.Contains("summary", StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool StepHasLog(JsonElement root, string name, string expectedFileName)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string log = step.GetProperty("log").GetString() ?? string.Empty;
                return log.EndsWith(expectedFileName, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool StepHasDomainXml(JsonElement root, string name)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string domainXml = step.GetProperty("domainXml").GetString() ?? string.Empty;
                return domainXml.EndsWith(".xml", StringComparison.Ordinal)
                    && domainXml.Contains("libvirt", StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool StepHasSerialAndSummaryArtifact(JsonElement root, string name)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string serialLog = step.GetProperty("serialLog").GetString() ?? string.Empty;
                string summaryLog = step.GetProperty("summaryLog").GetString() ?? string.Empty;
                return serialLog.EndsWith(".log", StringComparison.Ordinal)
                    && summaryLog.EndsWith(".log", StringComparison.Ordinal)
                    && serialLog.Contains("serial", StringComparison.Ordinal)
                    && summaryLog.Contains("summary", StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool StepHasImageAndDiskFormat(JsonElement root, string name, string diskFormat)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string image = step.GetProperty("image").GetString() ?? string.Empty;
                string actualDiskFormat = step.GetProperty("diskFormat").GetString() ?? string.Empty;
                return actualDiskFormat == diskFormat
                    && image.EndsWith("." + diskFormat, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool QemuImgDiscoveryMatches(JsonElement root, bool expectedAvailable, string expectedSource)
    {
        if (!root.TryGetProperty("qemuImgDiscovery", out JsonElement discovery))
        {
            return false;
        }

        return discovery.GetProperty("available").GetBoolean() == expectedAvailable
            && discovery.GetProperty("source").GetString() == expectedSource
            && !string.IsNullOrWhiteSpace(discovery.GetProperty("requested").GetString())
            && !string.IsNullOrWhiteSpace(discovery.GetProperty("resolved").GetString());
    }

    private static bool Qcow2ConversionIntentMatches(
        JsonElement root,
        string expectedMode,
        string expectedStatus,
        bool qemuImgRequired,
        bool qemuImgAvailable)
    {
        if (!root.TryGetProperty("qcow2ConversionIntent", out JsonElement intent))
        {
            return false;
        }

        return intent.GetProperty("mode").GetString() == expectedMode
            && intent.GetProperty("status").GetString() == expectedStatus
            && intent.GetProperty("qemuImgRequired").GetBoolean() == qemuImgRequired
            && intent.GetProperty("qemuImgAvailable").GetBoolean() == qemuImgAvailable;
    }

    private static bool StepHasQemuImgDiscovery(JsonElement root, string name, bool expectedAvailable, string expectedSource)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                return QemuImgDiscoveryMatches(step, expectedAvailable, expectedSource);
            }
        }

        return false;
    }

    private static bool StepHasQcow2ConversionIntent(
        JsonElement root,
        string name,
        string expectedMode,
        string expectedStatus,
        bool qemuImgRequired,
        bool qemuImgAvailable)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                return Qcow2ConversionIntentMatches(step, expectedMode, expectedStatus, qemuImgRequired, qemuImgAvailable);
            }
        }

        return false;
    }

    private static bool StepHasMarkerPreset(JsonElement root, string name, string expectedPreset)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                if (!step.TryGetProperty("markerPresets", out JsonElement presets))
                {
                    return false;
                }

                foreach (JsonElement preset in presets.EnumerateArray())
                {
                    if (preset.GetString() == expectedPreset)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        return false;
    }

    private static bool StepHasMarkerLane(
        JsonElement root,
        string name,
        string expectedPreset,
        string expectedFilePrefix)
    {
        return HasStep(root, name, "run-qemu-smoke.ps1")
            && HasStep(root, name, "-DryRun")
            && HasStep(root, name, "-MarkerPreset " + expectedPreset)
            && StepHasSerialAndSummary(root, name)
            && StepHasMarkerPreset(root, name, expectedPreset)
            && StepLogContainsFilePrefix(root, name, expectedFilePrefix);
    }

    private static bool StepLogContainsFilePrefix(JsonElement root, string name, string expectedFilePrefix)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string log = step.GetProperty("log").GetString() ?? string.Empty;
                string serialLog = step.GetProperty("serialLog").GetString() ?? string.Empty;
                string summaryLog = step.GetProperty("summaryLog").GetString() ?? string.Empty;
                return log.Contains(expectedFilePrefix, StringComparison.Ordinal)
                    && serialLog.Contains(expectedFilePrefix, StringComparison.Ordinal)
                    && summaryLog.Contains(expectedFilePrefix, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool QemuStepCommandContains(JsonElement root, string name, string commandFragment)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string command = step.GetProperty("command").GetString() ?? string.Empty;
                return command.Contains(commandFragment, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool AllStepsHaveWorkloadProfile(JsonElement root, string workloadProfile)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("workloadProfile").GetString() != workloadProfile)
            {
                return false;
            }
        }

        return true;
    }

    private static bool StepHasExitCode(JsonElement root, string name, int exitCode)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                return step.GetProperty("exitCode").GetInt32() == exitCode;
            }
        }

        return false;
    }

    private static bool StepLogContains(
        string repositoryRoot,
        JsonElement root,
        string name,
        string expected)
    {
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("name").GetString() == name)
            {
                string log = step.GetProperty("log").GetString() ?? string.Empty;
                string logPath = Path.IsPathRooted(log)
                    ? log
                    : Path.Combine(repositoryRoot, log);
                return File.Exists(logPath)
                    && File.ReadAllText(logPath).Contains(expected, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static int CountOccurrences(string value, string needle)
    {
        int count = 0;
        int index = 0;

        while (true)
        {
            index = value.IndexOf(needle, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += needle.Length;
        }
    }

    private static CiSmokeDryRunResult RunCiSmokeDryRun(string repositoryRoot, string tempRoot)
    {
        return RunCiSmoke(repositoryRoot, tempRoot, "-DryRun");
    }

    private static CiSmokeDryRunResult RunCiSmoke(string repositoryRoot, string tempRoot, params string[] extraArguments)
    {
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "run-ci-smoke.ps1");
        string artifactPlanPath = Path.Combine(tempRoot, "ci-smoke.artifacts.json");
        string summaryPath = Path.Combine(tempRoot, "ci-smoke.summary.json");

        ProcessStartInfo start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repositoryRoot
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add("-LogRoot");
        start.ArgumentList.Add(tempRoot);

        foreach (string argument in extraArguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(120000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("run-ci-smoke.ps1 did not exit within 120 seconds.");
        }

        string output = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();
        return new CiSmokeDryRunResult(process.ExitCode, output, artifactPlanPath, summaryPath);
    }

    private static BaselineEvidencePlanResult RunBaselineEvidencePlanOnly(
        string repositoryRoot,
        string tempRoot,
        params string[] extraArguments)
    {
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "run-baseline-evidence.ps1");
        string summaryPath = Path.Combine(tempRoot, "summary.json");

        ProcessStartInfo start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repositoryRoot
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add("-PlanOnly");
        start.ArgumentList.Add("-LogRoot");
        start.ArgumentList.Add(tempRoot);

        foreach (string argument in extraArguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(60000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("run-baseline-evidence.ps1 -PlanOnly did not exit within 60 seconds.");
        }

        string output = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();
        return new BaselineEvidencePlanResult(process.ExitCode, output, summaryPath);
    }

    private static BaselineEvidencePlanResult RunBaselineEvidenceSelfTest(
        string repositoryRoot,
        string tempRoot)
    {
        string scriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "run-baseline-evidence.ps1");
        string summaryPath = Path.Combine(tempRoot, "summary.json");

        ProcessStartInfo start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repositoryRoot
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add("-SelfTest");
        start.ArgumentList.Add("-LogRoot");
        start.ArgumentList.Add(tempRoot);

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(60000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                "run-baseline-evidence.ps1 -SelfTest did not exit within 60 seconds.");
        }

        string output = standardOutput.GetAwaiter().GetResult()
            + standardError.GetAwaiter().GetResult();
        return new BaselineEvidencePlanResult(
            process.ExitCode,
            output,
            summaryPath);
    }

    private static void PrintDryRunFailure(CiSmokeDryRunResult result)
    {
        Console.WriteLine("CI smoke dry-run exit=" + result.ExitCode);
        Console.WriteLine("CI smoke dry-run output=" + result.Output.Replace(Environment.NewLine, "\\n", StringComparison.Ordinal));
        Console.WriteLine("CI smoke artifact plan exists=" + File.Exists(result.ArtifactPlanPath));
        Console.WriteLine("CI smoke summary exists=" + File.Exists(result.SummaryPath));
    }

    private static void PrintBaselinePlanFailure(BaselineEvidencePlanResult result)
    {
        Console.WriteLine("Baseline evidence plan exit=" + result.ExitCode);
        Console.WriteLine("Baseline evidence plan output=" + result.Output.Replace(Environment.NewLine, "\\n", StringComparison.Ordinal));
        Console.WriteLine("Baseline evidence summary exists=" + File.Exists(result.SummaryPath));
    }

    private readonly struct CiSmokeDryRunResult(
        int exitCode,
        string output,
        string artifactPlanPath,
        string summaryPath)
    {
        public int ExitCode { get; } = exitCode;

        public string Output { get; } = output;

        public string ArtifactPlanPath { get; } = artifactPlanPath;

        public string SummaryPath { get; } = summaryPath;
    }

    private readonly struct BaselineEvidencePlanResult(
        int exitCode,
        string output,
        string summaryPath)
    {
        public int ExitCode { get; } = exitCode;

        public string Output { get; } = output;

        public string SummaryPath { get; } = summaryPath;
    }
}
