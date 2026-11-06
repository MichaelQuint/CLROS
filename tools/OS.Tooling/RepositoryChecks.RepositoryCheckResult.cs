namespace OS.Tooling;

/// <summary>Host repository policy results. Every reported failure prevents
/// success; output preserves the stable naming and source-firewall markers.</summary>
public sealed record RepositoryCheckResult(IReadOnlyList<string> NamingFailures, IReadOnlyList<string> SourceFailures)
{
    public IReadOnlyList<string> StyleFailures { get; init; } = [];

    public bool Succeeded => NamingFailures.Count == 0 && SourceFailures.Count == 0 && StyleFailures.Count == 0;

    public void WriteTo(TextWriter output)
    {
        Write("naming.guard", NamingFailures);
        Write("source.firewall", SourceFailures);
        Write("source.style", StyleFailures);

        void Write(string group, IReadOnlyList<string> failures)
        {
            output.WriteLine("TEST_START " + group);
            if (failures.Count == 0)
                output.WriteLine("TEST_PASS " + group);
            else
                foreach (string failure in failures)
                    output.WriteLine("TEST_FAIL " + group + " " + failure);
        }
    }
}
