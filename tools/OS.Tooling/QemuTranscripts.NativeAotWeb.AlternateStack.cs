namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        // Literal Smoke queries only. Scalar rows prove complete output access
        // and errno, not copied stack contents, registration or handler ownership.
        // Service and other profiles retain their existing syscall admission.
        private bool WebAlternateStackCall(Dictionary<string, ulong> r)
        {
            if (r["nr"] != 131)
                return false;
            Need(web && !service && r["a0"] == 0, "web_altstack_query");
            ulong output = r["a1"];
            long expected = output == 0 || memory.Covered(output, 24, 2) ? 0 : -14;
            Need(r["valueBits"] == Bits(expected), "web_altstack_query_result");
            return true;
        }
    }
}
