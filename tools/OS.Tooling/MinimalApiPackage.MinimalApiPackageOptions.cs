using System.Text.Json;

namespace OS.Tooling;
public sealed record MinimalApiPackageOptions
{
    public string OutputRoot { get; init; } = "artifacts/kestrel-smoke";
    public string Configuration { get; init; } = "Release";
    public string WorkloadProfile { get; init; } = "SingleMinimalApi";
    public bool SkipValidation { get; init; }
    public bool SkipNegativeValidation { get; init; }

    public static MinimalApiPackageOptions Parse(string[] arguments)
    {
        var options = new MinimalApiPackageOptions();
        for (int i = 0; i < arguments.Length; i++)
        {
            string Value() => ++i < arguments.Length ? arguments[i] : throw new ArgumentException("missing_value " + arguments[i - 1]);
            options = arguments[i] switch
            {
                "--output-root" => options with
                {
                    OutputRoot = Value()
                },
                "--configuration" => options with
                {
                    Configuration = Value()
                },
                "--workload-profile" => options with
                {
                    WorkloadProfile = Value()
                },
                "--skip-validation" => options with
                {
                    SkipValidation = true
                },
                "--skip-negative-validation" => options with
                {
                    SkipNegativeValidation = true
                },
                _ => throw new ArgumentException("unknown_option " + arguments[i])};
        }

        return options;
    }
}
