using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling;
using P = OS.Tooling.NativeAotLogUploadProgram;

internal static partial class NativeAotLogUploadProgramTests
{
    private sealed class SourceCopy : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("clros-log-upload-program-").FullName;

        public SourceCopy(bool complete = true)
        {
            if (!complete)
                return;
            string original = HostToolProcess.FindRepositoryRoot();
            foreach (string relative in P.CurrentSources().Select(source => source.Path))
            {
                string path = Path.Combine(Root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(Path.Combine(original, relative), path);
            }
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
