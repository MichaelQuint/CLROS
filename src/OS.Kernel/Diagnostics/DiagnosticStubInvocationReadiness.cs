using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public static class DiagnosticStubInvocationReadiness
{
    public static DiagnosticStubInvocationReadinessResult RequireReady(ExecutableStubPageTransitionPlanResult pageTransition, Com1PortIoBackendContractResult backendContract)
    {
        if (!pageTransition.Succeeded || pageTransition.BytesMaterialized == 0)
        {
            return DiagnosticStubInvocationReadinessResult.Fail(DiagnosticStubInvocationReadinessStatus.PageTransitionNotReady, pageTransition, backendContract);
        }

        if (!backendContract.Succeeded)
        {
            return DiagnosticStubInvocationReadinessResult.Fail(DiagnosticStubInvocationReadinessStatus.BackendContractNotReady, pageTransition, backendContract);
        }

        if (!DiagnosticStubInvocationReadinessResult.IsInsidePayload(backendContract.Out8StubAddress, pageTransition.PayloadBaseAddress, pageTransition.PayloadEndAddress))
        {
            return DiagnosticStubInvocationReadinessResult.Fail(DiagnosticStubInvocationReadinessStatus.Out8AddressOutsideRegion, pageTransition, backendContract);
        }

        if (!DiagnosticStubInvocationReadinessResult.IsInsidePayload(backendContract.In8StubAddress, pageTransition.PayloadBaseAddress, pageTransition.PayloadEndAddress))
        {
            return DiagnosticStubInvocationReadinessResult.Fail(DiagnosticStubInvocationReadinessStatus.In8AddressOutsideRegion, pageTransition, backendContract);
        }

        return DiagnosticStubInvocationReadinessResult.Pass(pageTransition, backendContract);
    }
}
