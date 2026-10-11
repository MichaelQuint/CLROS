namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private static void ValidateDiagnosticTranscript(string root, QemuSmokeOptions options, QemuSmokePlan plan, ProcessResult runner, string[] lines)
    {
        string path = Path.GetFullPath(options.SerialLog, root);
        if(plan.IsEnabled("EnableM04UnhandledExceptionProbe"))
        {
            AssertM04ManagedHeapPageRangeTranscript(path,includeGcStatics:true,includeUnhandled:true);
            AssertExpectedPanicRunner(root,options,plan,runner,lines,false,unhandled:true);
            return;
        }
        if (plan.IsEnabled("EnableM03MemoryMapCaptureProbe"))
        { Need(runner.StandardError.Length == 0, "qemu_runner_stderr_not_empty"); AssertM03MemoryMapCaptureTranscript(path); AssertM03MemoryMapCaptureRunnerResult(lines); return; }
        if (plan.IsEnabled("EnableM04LayoutContractProbe") && !plan.IsEnabled("EnableM04ManagedHeapPageRangeProbe"))
        { AssertM04LayoutContractTranscript(path); return; }
        if (plan.IsEnabled("EnableM04ManagedHeapPageRangeProbe"))
        {
            AssertM04ManagedHeapRunnerResult(lines);
            bool Enabled(string name) => plan.IsEnabled("Enable" + name + "Probe");
            if (Enabled("M04GcStaticRootMaterialization"))
                AssertM04ManagedHeapPageRangeTranscript(path, includeGcStatics: true, includeSeededChurn: Enabled("M04SeededCollectorChurn"),
                    includeClassConstructors: Enabled("M04ClassConstructorExecution"), includeInterfaceDispatch: Enabled("M04InterfaceDispatch"),
                    includeSimpleCatch: Enabled("M04SimpleThrowCatch"), includeSingleFinally: Enabled("M04SingleFinallyCatch"), includeRethrow: Enabled("M04Rethrow"), includeNestedFinally: Enabled("M04NestedFinallyCatch"));
            else if (Enabled("M04CheckedReferenceBarrier")) AssertM04CheckedReferenceBarrierTranscript(path);
            else if (Enabled("M04StringAllocation")) AssertM04StringAllocationTranscript(path);
            else if (Enabled("M04SzArrayAllocation")) AssertM04SzArrayAllocationTranscript(path);
            else if (Enabled("M04RhpNewFast")) AssertM04RhpNewFastTranscript(path);
            else AssertM04ManagedHeapPageRangeTranscript(path);
            return;
        }
        if (plan.IsEnabled("EnableM04TypeManagerModuleRegistrationProbe"))
        {
            Need(runner.StandardError.Length == 0, "qemu_runner_stderr_not_empty");
            AssertM04TypeManagerTranscript(path, plan.IsEnabled("EnableM04X64UnwindMetadataProbe"));
            AssertStandardRunnerResult(root, options, plan, lines);
            return;
        }
        if (plan.IsEnabled("EnableM02UnexpectedVectorPanicProbe"))
        { AssertM02UnexpectedVectorPanicTranscript(path); AssertExpectedPanicRunner(root, options, plan, runner, lines, false); return; }
        if (plan.IsEnabled("EnableM02PageFaultCr2ErrorCodeProbe"))
        { AssertM02PageFaultCr2ErrorCodeTranscript(path); AssertM02PageFaultRunnerResult(lines); }
        if (plan.IsEnabled("EnableM02DoubleFaultIstProbe"))
        { AssertM02DoubleFaultIstTranscript(path); AssertM02DoubleFaultIstRunnerResult(lines); }
        if (plan.IsEnabled("EnableM02Ud2VectorStateProbe")) AssertM02Ud2VectorStateTerminalTranscript(path);
        Need(runner.Succeeded, "qemu_runner_failed");
    }
}
