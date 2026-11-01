namespace OS.Kernel.Build;
public static class WorkloadProfile
{
    public const string SingleMinimalApiName = "SingleMinimalApi";
    public const string MultiMinimalApiName = "MultiMinimalApi";
#if OS_PROFILE_SINGLE_MINIMAL_API && OS_PROFILE_MULTI_MINIMAL_API
#error Exactly one OS workload profile symbol must be defined.
#elif OS_PROFILE_SINGLE_MINIMAL_API
    public const WorkloadProfileKind SelectedKind = WorkloadProfileKind.SingleMinimalApi;
    public const string SelectedName = SingleMinimalApiName;
    public const bool IsSingleMinimalApi = true;
    public const bool IsMultiMinimalApi = false;
    public const bool RequiresProcessTable = false;
    public const bool RequiresMultiAppManifest = false;
    public const bool SupportsMultipleMinimalApiApps = false;
#elif OS_PROFILE_MULTI_MINIMAL_API
    public const WorkloadProfileKind SelectedKind = WorkloadProfileKind.MultiMinimalApi;
    public const string SelectedName = MultiMinimalApiName;
    public const bool IsSingleMinimalApi = false;
    public const bool IsMultiMinimalApi = true;
    public const bool RequiresProcessTable = true;
    public const bool RequiresMultiAppManifest = true;
    public const bool SupportsMultipleMinimalApiApps = true;
#else
#error No OS workload profile symbol was defined.
#endif
    public const bool ExactlyOneProfileSelected = IsSingleMinimalApi != IsMultiMinimalApi;
}
