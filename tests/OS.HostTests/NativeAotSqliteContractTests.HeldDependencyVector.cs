using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotSqliteContractTests
{
    private sealed class HeldDependencyVector : IDisposable
    {
        internal static readonly string[] Names = ["receipt.json", "commands.json", "round0/sqlite3.o", "round1/sqlite3.o", "libe_sqlite3.a", "libe_sqlite3-repeat.a", "upstream/sqlite3.c", "upstream/sqlite3.h", "upstream/sqlite-amalgamation-3530400.zip"];
        internal readonly string Root = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/sqlite-archive-held-tests", Guid.NewGuid().ToString("N"));
        internal readonly DependencyVector Values = new();
        internal string ReceiptHash => DependencyVector.Hash(File.ReadAllBytes(PathOf("receipt.json")));

        internal string PathOf(string relative) => Path.Combine(Root, relative);
        internal HeldDependencyVector()
        {
            Directory.CreateDirectory(Root);
            foreach (var pair in Values.Artifacts)
                Write(pair.Key, pair.Value);
            // Deliberately not pinned SQLite source: this helper grants only
            // local held-file identity. Full recipe/source admission must fail.
            foreach (string name in Names.Where(p => p.StartsWith("upstream/", StringComparison.Ordinal)))
            {
                byte[] bytes = Encoding.UTF8.GetBytes("independent held source " + name);
                Write(name, bytes);
                Values.Receipt["Outputs"]![name] = DependencyVector.Hash(bytes);
            }

            WriteReceipt();
        }

        internal void Write(string relative, byte[] bytes)
        {
            string path = PathOf(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        internal void WriteReceipt() => Write("receipt.json", Json(Values.Receipt));
        internal IReadOnlyDictionary<string, AuthorityFile> Hold(AuthorityFiles files) => NativeAotSqliteContract.HoldArchiveDependencyInputs(files, Root, ReceiptHash);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
