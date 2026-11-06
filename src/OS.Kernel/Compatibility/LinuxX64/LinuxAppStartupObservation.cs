namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Observes the pinned application's first four stderr lines before its first
/// successful socket bind. One externally serialized process owner keeps this
/// value for the process lifetime. It borrows each copied output buffer only
/// during Observe, allocates nothing and authorizes no thread or execution.
/// Malformed input, a changed startup writer or an invalid bind fails permanently.
/// Later stderr belongs to the log observer. Listening, application lifetime,
/// source/native identity and external HTTP evidence remain the caller's duty.
/// A default value observes minimal.api.smoke; ForService explicitly selects
/// minimal.api.service before any observation and never changes mode in use.
/// </summary>
public unsafe struct LinuxAppStartupObservation
{
    private ulong writer, bindWriter;
    private uint stages, position;
    private bool failed, bound;
    private readonly bool service;

    private LinuxAppStartupObservation(bool service)
    {
        this = default;
        this.service = service;
    }

    /// <summary>Create an empty observer for the exact service startup identity.
    /// No application selection, source identity or execution is authorized.</summary>
    public static LinuxAppStartupObservation ForService() => new(true);
    /// <summary>Identity mode fixed at construction, retained after failure/bind.</summary>
    public readonly bool IsService => service;

    /// <summary>Number of complete ordered lines observed, retained on failure.</summary>
    public readonly uint ObservedStages => stages;
    /// <summary>Serialized thread identity which supplied the startup prefix.</summary>
    public readonly ulong WriterIdentity => writer;
    /// <summary>Serialized thread identity of the successful first bind.</summary>
    public readonly ulong BindIdentity => bindWriter;
    /// <summary>All four observations passed; a default or failed value is false.</summary>
    public readonly bool StartupComplete => stages == 4 && !failed;
    /// <summary>The first bind succeeded after startup; does not prove listening.</summary>
    public readonly bool SuccessfulBind => bound && !failed;
    /// <summary>Permanent failure; a later valid fragment cannot restore readiness.</summary>
    public readonly bool Failed => failed;

    /// <summary>
    /// Reads 1..8192 borrowed copied stderr bytes under the caller's syscall
    /// serialization. The pointer must remain readable for this call. Startup
    /// fragments must share a nonzero verified current-thread identity and match
    /// exact LF-terminated ASCII lines. Valid output after the prefix is ignored.
    /// Invalid inputs or a mismatch return false and permanently fail this value.
    /// </summary>
    public bool Observe(ulong currentIdentity, byte* source, uint bytes)
    {
        if (failed)
            return false;
        if (currentIdentity == 0 || source == null || bytes == 0 || bytes > 8192)
        {
            failed = true;
            return false;
        }
        if (stages == 4)
            return true;
        if (writer == 0)
            writer = currentIdentity;
        if (writer != currentIdentity)
        {
            failed = true;
            return false;
        }
        for (uint i = 0; i < bytes; i++)
        {
            string expected = service ? stages switch
            {
                0 => "CLROS_APP_ENTRY minimal.api.service\n",
                1 => "CLROS_APP_CCTOR minimal.api.service\n",
                2 => "CLROS_APP_CTOR minimal.api.service\n",
                _ => "CLROS_APP_BUILT minimal.api.service\n"
            } : stages switch
            {
                0 => "CLROS_APP_ENTRY minimal.api.smoke\n",
                1 => "CLROS_APP_CCTOR minimal.api.smoke\n",
                2 => "CLROS_APP_CTOR minimal.api.smoke\n",
                _ => "CLROS_APP_BUILT minimal.api.smoke\n"
            };
            if (source[i] != expected[(int)position])
            {
                failed = true;
                return false;
            }
            if (++position == expected.Length)
            {
                position = 0;
                if (++stages == 4)
                    return true;
            }
        }
        return true;
    }

    /// <summary>
    /// Records the actual first bind result in the same serialized syscall
    /// order. A nonzero verified current thread may differ from the startup
    /// writer. Early, failed or repeated binds return false and fail permanently.
    /// No syscall is executed and no socket/listener ownership is created here.
    /// </summary>
    public bool RecordBind(ulong currentIdentity, bool succeeded)
    {
        if (failed || currentIdentity == 0 || !StartupComplete || bound || !succeeded)
        {
            failed = true;
            return false;
        }
        bindWriter = currentIdentity;
        bound = true;
        return true;
    }
}
