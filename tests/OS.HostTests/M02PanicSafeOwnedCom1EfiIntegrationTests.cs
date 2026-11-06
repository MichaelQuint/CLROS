using System.Text;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static class M02PanicSafeOwnedCom1EfiIntegrationTests
{
    private const string TestName = "m02.006.efi.integration";
    private const string ReadyMarker =
        OS.Tooling.QemuContracts.Record115;
    private const string EvidenceMarker =
        OS.Tooling.QemuContracts.Record116;
    private const string FailureMarker = OS.Tooling.QemuContracts.Record109;
    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.006 EFI closure links one default-off panic reporter",
            TestCompileClosureAndSwitch,
            ref failures);
        RunCase(
            "m02.006 EFI calls the panic reporter after m02.005",
            TestEntryCallOrderAndResultGate,
            ref failures);
        RunCase(
            "m02.006 direct and UEFI marker transports remain separate",
            TestMarkerTransportSeparation,
            ref failures);
        RunCase(
            "m02.006 QEMU profile enforces exact positive and negative evidence",
            TestQemuProfileSource,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-006 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestCompileClosureAndSwitch()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(root, "src", "OS", "OS.csproj"));
        var catalog = Source();
        foreach (string owner in new[]
        {
        "OS.Kernel.Diagnostics.OwnedCom1ReadyExclusivelyOwnedWriter",
        "OS.Kernel.Diagnostics.PanicSafeFormattingSerialReportingOwnedCom1",
        "OS.Kernel.Diagnostics.OwnedX64GdtContainingNullN64BitCode",
        "OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI",
        "OS.Kernel.Diagnostics.OwnedGDTTSSGeneratedAuditedCSharpStubBytes",
        "OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame",
        "OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa"
    })
            _ = catalog.Type(owner);
        XElement[] defaults = project.Descendants("EnableM02PanicSafeOwnedCom1Probe").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02PanicSafeOwnedCom1Probe)' == ''").ToArray();
        XElement[] ud2ProfileOverrides = project.Descendants("PropertyGroup").Where(group => group.Attribute("Condition")?.Value == "'$(EnableM02Ud2VectorStateProbe)' == 'true'").SelectMany(group => group.Elements("EnableM02PanicSafeOwnedCom1Probe")).ToArray();
        XElement[] symbolGroups = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'").ToArray();
        string[] dependencyConditions = project.Descendants("Error").Select(element => element.Attribute("Condition")?.Value ?? string.Empty).Where(value => value.StartsWith("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'", StringComparison.Ordinal)).ToArray();
        return (defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(EnableM02PanicSafeOwnedCom1Probe)' == ''" && ud2ProfileOverrides.Length == 1 && ud2ProfileOverrides[0].Value == "true" && symbolGroups.Length == 1 && symbolGroups[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M02_PANIC_SAFE_OWNED_COM1_PROBE" && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM02OwnedCom1ReadyProbe)' != 'true'", StringComparer.Ordinal)) && OS.Tooling.ContractAudit.Inspect(root).Count == 0;
    }

    private static bool TestEntryCallOrderAndResultGate()
    {
        var catalog = Source();
        string source = HostSourceText.Active(catalog.Method(Adapter, "WriteSuccessLine"));
        int priorSuccess = HostSourceText.IndexOf(source, "WriteM02OwnedCom1ReadySuccessLine(");
        int reporterCall = IndexOfAfter(
            source,
            ".EmitReadinessMarker(ownedCom1Backend);",
            priorSuccess);
        int resultGuard = IndexOfAfter(
            source,
            "if (!panicSafeOwnedCom1.Succeeded ||",
            reporterCall);
        int failureLine = IndexOfAfter(
            source,
            "WriteM02PanicSafeOwnedCom1FailureLine(",
            resultGuard);
        int evidenceLine = IndexOfAfter(
            source,
            "WriteM02PanicSafeOwnedCom1EvidenceLine(",
            failureLine);
        int stageThreeLineLoop = IndexOfAfter(
            source,
            "GetM01StageThreeSuccessLineLength(",
            evidenceLine);

        return priorSuccess >= 0
            && reporterCall > priorSuccess
            && resultGuard > reporterCall
            && failureLine > resultGuard
            && evidenceLine > failureLine
            && stageThreeLineLoop > evidenceLine
            && HostSourceText.Contains(source, "panicSafeOwnedCom1.Format.RequiredBytes !=")
            && HostSourceText.Contains(source, ".ReadinessSerialByteCount")
            && HostSourceText.Contains(source, "panicSafeOwnedCom1.Configuration.WritesCompleted !=")
            && HostSourceText.Contains(source, ".RequiredConfigurationWriteCount")
            && HostSourceText.Contains(source, "panicSafeOwnedCom1.LineStatusPolls !=")
            && HostSourceText.Contains(source, "panicSafeOwnedCom1.FailedByteIndex != -1")
            && HostSourceText.Contains(source, "!panicSafeOwnedCom1.OwnedBackendUsed")
            && HostSourceText.Contains(source, "panicSafeOwnedCom1.FirmwareFallbackUsed")
            && HostSourceText.Contains(source, ".DataRegisterPort")
            && HostSourceText.Contains(source, ".LineFeed")
            && HostSourceText.Count(
                source,
                ".EmitReadinessMarker(ownedCom1Backend);") == 1
            && HostSourceOperations.CallsTo(catalog,
                "OS.Kernel.Diagnostics.PanicSafeFormattingSerialReportingOwnedCom1", "EmitReadinessMarker").Length == 1;
    }

    private static bool TestMarkerTransportSeparation()
    {
        var catalog = Source();
        string source = HostSourceText.Documents(catalog, "OS.Boot.EfiEntry");
        string reconstructedEvidence = HostSourceText.ByteCharacters(catalog, Adapter, "WriteM02PanicSafeOwnedCom1EvidenceLine");
        string reconstructedFailure = HostSourceText.ByteCharacters(catalog, Adapter, "WriteM02PanicSafeOwnedCom1FailureLine");
        var productionReady = new StringBuilder(
            PanicSafeFormattingSerialReportingOwnedCom1.ReadinessMarkerByteCount);
        for (int index = 0;
            index < PanicSafeFormattingSerialReportingOwnedCom1.ReadinessMarkerByteCount;
            index++)
        {
            if (!PanicSafeFormattingSerialReportingOwnedCom1
                    .TryGetReadinessMarkerByte(index, out byte value))
            {
                return false;
            }

            productionReady.Append((char)value);
        }

        bool directReadyFormatterExists = HostSourceText.HasByteFormatter(catalog, "OS.Boot.EfiEntry", ReadyMarker);
        return productionReady.ToString() == ReadyMarker
            && productionReady.Length == 125
            && PanicSafeFormattingSerialReportingOwnedCom1.ReadinessSerialByteCount ==
                productionReady.Length + 2
            && reconstructedEvidence == EvidenceMarker
            && reconstructedFailure == FailureMarker
            && !directReadyFormatterExists
            && !source.Contains("\"" + ReadyMarker + "\"", StringComparison.Ordinal)
            && HostSourceOperations.CallsTo(catalog, Adapter, "WriteM02PanicSafeOwnedCom1EvidenceLine").Length == 1
            && HostSourceOperations.CallsTo(catalog, Adapter, "WriteM02PanicSafeOwnedCom1FailureLine").Length == 1;
    }

    private static bool TestQemuProfileSource() => EfiProfileTestControls.Returning("M02PanicSafeOwnedCom1", OS.Tooling.QemuContracts.M02PanicSafeOwnedCom1RequiredMarkers, OS.Tooling.QemuContracts.M02PanicSafeOwnedCom1ForbiddenMarkers, ReadyMarker, FailureMarker);

    private const string Adapter = "OS.Boot.EfiEntry+M01StageThreeAdapter";

    private static OS.Tooling.CSharpSourceCatalog Source() => HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(
        EfiProfileTestControls.ReturningArguments("M02PanicSafeOwnedCom1"), true).Profile());

    private static int IndexOfAfter(string source, string value, int start) => HostSourceText.IndexOf(source, value, start);
}
