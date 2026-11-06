using System.Globalization;

namespace OS.Tools.QemuRunner;

public static partial class EvidenceManifestCommand
{
    private sealed class EvidenceManifestCliOptions
    {
        private readonly Dictionary<string, string> tools = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<EvidenceArtifactInput> artifacts = new List<EvidenceArtifactInput>();
        private readonly List<string> requiredMarkers = new List<string>();
        private readonly List<string> forbiddenMarkers = new List<string>();
        private readonly List<string> observedMarkers = new List<string>();
        public string OutputPath { get; private set; } = string.Empty;
        public string RepositoryPath { get; private set; } = string.Empty;
        public string ExpectedSourceCommit { get; private set; } = string.Empty;
        public bool? ExpectedSourceDirty
        {
            get; private set;
        }
        public DateTimeOffset StartedUtc
        {
            get; private set;
        }
        public DateTimeOffset CompletedUtc
        {
            get; private set;
        }
        public string CommandFile { get; private set; } = string.Empty;
        public int ExitCode
        {
            get; private set;
        }
        public EvidenceRunClassification Classification
        {
            get; private set;
        }

        public static EvidenceManifestCliOptions Parse(string[] args)
        {
            EvidenceManifestCliOptions options = new EvidenceManifestCliOptions();
            for (int index = 1; index < args.Length; index++)
            {
                string argument = args[index];
                switch (argument)
                {
                    case "--output":
                        options.OutputPath = ReadValue(args, ref index, argument);
                        break;
                    case "--repository":
                        options.RepositoryPath = ReadValue(args, ref index, argument);
                        break;
                    case "--expected-source-commit":
                        options.ExpectedSourceCommit = ReadValue(args, ref index, argument);
                        break;
                    case "--expected-source-dirty":
                        options.ExpectedSourceDirty = ReadBoolean(args, ref index, argument);
                        break;
                    case "--started-utc":
                        options.StartedUtc = ReadTimestamp(args, ref index, argument);
                        break;
                    case "--completed-utc":
                        options.CompletedUtc = ReadTimestamp(args, ref index, argument);
                        break;
                    case "--command-file":
                        options.CommandFile = ReadValue(args, ref index, argument);
                        break;
                    case "--exit-code":
                        options.ExitCode = ReadInt32(args, ref index, argument);
                        break;
                    case "--classification":
                        options.Classification = ReadClassification(args, ref index, argument);
                        break;
                    case "--tool":
                        options.AddTool(ReadValue(args, ref index, argument));
                        break;
                    case "--artifact":
                        options.AddArtifact(ReadValue(args, ref index, argument));
                        break;
                    case "--require-marker":
                        AddDistinct(options.requiredMarkers, ReadValue(args, ref index, argument), argument);
                        break;
                    case "--forbid-marker":
                        AddDistinct(options.forbiddenMarkers, ReadValue(args, ref index, argument), argument);
                        break;
                    case "--observed-marker":
                        AddDistinct(options.observedMarkers, ReadValue(args, ref index, argument), argument);
                        break;
                    default:
                        throw new ArgumentException("Unknown argument: " + argument);
                }
            }

            options.Validate();
            return options;
        }

        public EvidenceManifestRequest ToRequest()
        {
            string command;
            try
            {
                command = File.ReadAllText(CommandFile).Trim();
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Unable to read --command-file: " + ex.GetType().Name);
            }

            return new EvidenceManifestRequest
            {
                RepositoryPath = RepositoryPath,
                ExpectedSourceCommit = ExpectedSourceCommit,
                ExpectedSourceDirty = ExpectedSourceDirty!.Value,
                StartedUtc = StartedUtc,
                CompletedUtc = CompletedUtc,
                Command = command,
                ExitCode = ExitCode,
                Classification = Classification,
                ToolVersions = tools,
                Artifacts = artifacts,
                RequiredMarkers = requiredMarkers,
                ForbiddenMarkers = forbiddenMarkers,
                ObservedMarkers = observedMarkers
            };
        }

        private void AddTool(string value)
        {
            int separator = value.IndexOf('=');
            if (separator <= 0 || separator == value.Length - 1)
            {
                throw new ArgumentException("--tool requires name=version.");
            }

            string name = value[..separator].Trim();
            string version = value[(separator + 1)..].Trim();
            if (name.Length == 0 || version.Length == 0 || !tools.TryAdd(name, version))
            {
                throw new ArgumentException("--tool contains an empty or duplicate name.");
            }
        }

        private void AddArtifact(string value)
        {
            string[] fields = value.Split('|');
            if (fields.Length != 4 || fields.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("--artifact requires name|role|path|sha256.");
            }

            string name = fields[0].Trim();
            if (artifacts.Any(artifact => string.Equals(artifact.Name, name, StringComparison.Ordinal)))
            {
                throw new ArgumentException("--artifact names must be unique.");
            }

            artifacts.Add(new EvidenceArtifactInput(name, fields[1].Trim(), fields[2].Trim(), fields[3].Trim()));
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(OutputPath) || string.IsNullOrWhiteSpace(RepositoryPath) || string.IsNullOrWhiteSpace(ExpectedSourceCommit) || !ExpectedSourceDirty.HasValue || StartedUtc == default || CompletedUtc == default || string.IsNullOrWhiteSpace(CommandFile) || !File.Exists(CommandFile) || !Enum.IsDefined(Classification) || tools.Count == 0 || artifacts.Count == 0)
            {
                throw new ArgumentException("Required evidence-manifest arguments are missing.");
            }
        }

        private static string ReadValue(string[] args, ref int index, string argument)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for " + argument + ".");
            }

            string value = args[++index];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Empty value for " + argument + ".");
            }

            return value;
        }

        private static bool ReadBoolean(string[] args, ref int index, string argument)
        {
            string value = ReadValue(args, ref index, argument);
            if (!bool.TryParse(value, out bool parsed))
            {
                throw new ArgumentException("Value for " + argument + " must be true or false.");
            }

            return parsed;
        }

        private static int ReadInt32(string[] args, ref int index, string argument)
        {
            string value = ReadValue(args, ref index, argument);
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new ArgumentException("Value for " + argument + " must be a 32-bit integer.");
            }

            return parsed;
        }

        private static DateTimeOffset ReadTimestamp(string[] args, ref int index, string argument)
        {
            string value = ReadValue(args, ref index, argument);
            if (!DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
            {
                throw new ArgumentException("Value for " + argument + " must use round-trip ISO 8601.");
            }

            return parsed;
        }

        private static EvidenceRunClassification ReadClassification(string[] args, ref int index, string argument)
        {
            string value = ReadValue(args, ref index, argument);
            return value.ToLowerInvariant() switch
            {
                "pass" => EvidenceRunClassification.Passed,
                "fail" => EvidenceRunClassification.Failed,
                "timeout" => EvidenceRunClassification.TimedOut,
                _ => throw new ArgumentException("Value for --classification must be pass, fail, or timeout.")
            };
        }

        private static void AddDistinct(List<string> destination, string value, string argument)
        {
            string normalized = value.Trim();
            if (destination.Contains(normalized, StringComparer.Ordinal))
            {
                throw new ArgumentException(argument + " values must be unique.");
            }

            destination.Add(normalized);
        }
    }
}
