using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // Only the explicit QemuSmoke quota route supplies the actual original
    // pre-run input records and privately owned observed Completion. Its source,
    // native and image locks remain held through complete inspection/readback.
    internal static ServiceQuotaOwnedEvidence HoldServiceQuotaEvidence(string root, QemuSmokeOptions options, NativeAotServiceProgram program, NativeAotServiceQuotaPeer.Completion completion, ProcessResult runner, ServiceRunnerContext context, AuthorityFiles inputLocks, AuthorityFile packageInput, AuthorityFile dumpInput, AuthorityFile imageInput, AuthorityFile efiInput) => ServiceQuotaOwnedEvidence.Create(root, options, program, completion, runner, context, inputLocks, packageInput, dumpInput, imageInput, efiInput);
}
