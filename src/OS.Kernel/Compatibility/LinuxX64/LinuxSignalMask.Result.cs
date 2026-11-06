namespace OS.Kernel.Compatibility.LinuxX64;

public readonly partial struct LinuxSignalMask
{
    /// <summary>Default result is not success. Failure preserves Before/After;
    /// success proposes an independent value without mutating the caller.</summary>

    public readonly struct Result
    {
        public readonly long Error;
        public readonly LinuxSignalMask Before, After;
        private readonly bool _returned;
        public bool Succeeded => _returned && Error == 0;

        internal Result(long error, LinuxSignalMask before, LinuxSignalMask after)
        {
            Error = error;
            Before = before;
            After = after;
            _returned = true;
        }
    }
}
