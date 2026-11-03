using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static partial class SourceFirewallTests
{
    private sealed class TemporaryRepository : IDisposable
    {
        private TemporaryRepository(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TemporaryRepository Create(bool includeSolution = true)
        {
            string root = Path.Combine(Path.GetTempPath(), "clros-source-firewall-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            TemporaryRepository repository = new TemporaryRepository(root);
            if (includeSolution)
            {
                repository.WriteFile("CLROS.slnx", "<Solution />");
            }

            return repository;
        }

        public void WriteFile(string relativePath, string content)
        {
            string path = Path.Combine(Root, relativePath);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
