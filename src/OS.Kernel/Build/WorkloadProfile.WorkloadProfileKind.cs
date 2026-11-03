namespace OS.Kernel.Build;

public enum WorkloadProfileKind
{
    Unknown = 0,
    SingleMinimalApi = 1,
    MultiMinimalApi = 2
}
#if OS_PROFILE_SINGLE_MINIMAL_API && OS_PROFILE_MULTI_MINIMAL_API
#error Exactly one OS workload profile symbol must be defined.
#elif OS_PROFILE_SINGLE_MINIMAL_API

#elif OS_PROFILE_MULTI_MINIMAL_API

#else
#error No OS workload profile symbol was defined.
#endif
