using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using OS.Manifest;
using OS.Tooling;

internal static partial class NativeAotWebProgramTests
{
    private sealed class SourceFixture : IDisposable
    {
        private readonly string parent = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()));
        public string Root
        {
            get;
        }

        private static string[] Sources => NativeAotWebProgram.CurrentSources().Select(source => source.Path).ToArray();
        internal SourceFixture()
        {
            Root = System.IO.Path.Combine(parent, "clros-web-sources-" + Guid.NewGuid().ToString("N"));
            if (System.IO.Path.Exists(Root))
                throw new IOException("fresh source fixture required");
            Directory.CreateDirectory(Root);
            try
            {
                string repo = HostToolProcess.FindRepositoryRoot();
                foreach (string relative in Sources)
                {
                    string destination = PathFor(relative);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                    File.Copy(System.IO.Path.Combine(repo, relative), destination, false);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal string PathFor(string relative) => System.IO.Path.Combine(Root, relative);
        internal void Write(string relative, string text)
        {
            string path = PathFor(relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        public void Dispose()
        {
            if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Root)) != parent || !System.IO.Path.GetFileName(Root).StartsWith("clros-web-sources-", StringComparison.Ordinal))
                throw new InvalidOperationException("source fixture cleanup boundary");
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}
