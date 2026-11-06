internal static partial class NativeAotLogUploadContractTests
{
    private sealed class FinalPassFaultWriter : StringWriter
    {
        private readonly string finalPassLine;
        public IOException Failure { get; } = new("bounded final pass writer failure");
        public int FinalPassCalls
        {
            get; private set;
        }

        public FinalPassFaultWriter(string marker)
        {
            finalPassLine = "TEST_PASS " + marker;
        }

        public override void WriteLine(string? value)
        {
            if (string.Equals(value, finalPassLine, StringComparison.Ordinal))
            {
                FinalPassCalls++;
                throw Failure;
            }

            base.WriteLine(value);
        }
    }
}
