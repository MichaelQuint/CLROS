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
    private sealed class DependencyVector
    {
        internal const string Directory = "/fixture/artifacts/sqlite-reference";
        internal readonly Dictionary<string, byte[]> Artifacts = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, NativeAotSqliteContract.CompilerInput> Compiler = new(StringComparer.Ordinal);
        internal string[] Imports = ["sqlite3_open_v2", "sqlite3_close_v2", "sqlite3_prepare_v2", "sqlite3_step"];
        internal readonly JsonObject Receipt;
        internal JsonArray Commands;
        internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        internal DependencyVector()
        {
            byte[] archive = ArchiveVector();
            int bytes = int.Parse(Encoding.ASCII.GetString(archive, 56, 10).Trim(), CultureInfo.InvariantCulture);
            byte[] obj = archive.AsSpan(68, bytes).ToArray();
            Artifacts.Add("libe_sqlite3.a", archive);
            Artifacts.Add("libe_sqlite3-repeat.a", archive.ToArray());
            Artifacts.Add("round0/sqlite3.o", obj);
            Artifacts.Add("round1/sqlite3.o", obj.ToArray());
            JsonObject native = [];
            foreach (string role in new[]
            {
                "dotnet",
                "gcc",
                "clang",
                "ld",
                "ar",
                "readelf",
                "nm",
                "strace",
                "setsid",
                "libc.a",
                "libm.a",
                "libz.a",
                "libstdc++.a",
                "libgcc.a",
                "libgcc_eh.a",
                "crt1.o",
                "crti.o",
                "crtn.o"
            }

            )
            {
                string path = "/compiler/" + role, hash = Hash(Encoding.ASCII.GetBytes("independent compiler " + role));
                Compiler.Add(role, new(path, hash));
                native[path] = hash;
            }

            native[Directory + "/upstream/sqlite3.c"] = NativeAotSqliteContract.SourceCsha256;
            native[Directory + "/upstream/sqlite3.h"] = NativeAotSqliteContract.SourceHeaderSha256;
            Commands = [];
            for (int round = 0; round < 2; round++)
            {
                string objectPath = Directory + "/round" + round + "/sqlite3.o", lib = Directory + (round == 0 ? "/libe_sqlite3.a" : "/libe_sqlite3-repeat.a");
                Commands.Add(new JsonObject { ["executable"] = "gcc", ["workingDirectory"] = "/fixture", ["log"] = "compile-r" + round, ["arguments"] = new JsonArray("-c", Directory + "/upstream/sqlite3.c", "-o", objectPath, "-O2", "-fPIC", "-ffunction-sections", "-fdata-sections", "-march=x86-64-v2", "-mtune=generic", "-DSQLITE_THREADSAFE=1") });
                Commands.Add(new JsonObject { ["executable"] = "ar", ["workingDirectory"] = "/fixture", ["log"] = "archive-r" + round, ["arguments"] = new JsonArray("crD", lib, objectPath) });
                native[objectPath] = Hash(obj);
                native[lib] = Hash(archive);
            }

            var inspected = NativeAotSqliteContract.InspectStaticArchiveValues(archive);
            JsonObject outputs = [];
            foreach (var artifact in Artifacts)
                outputs[artifact.Key] = Hash(artifact.Value);
            outputs["upstream/sqlite3.c"] = NativeAotSqliteContract.SourceCsha256;
            outputs["upstream/sqlite3.h"] = NativeAotSqliteContract.SourceHeaderSha256;
            outputs["upstream/sqlite-amalgamation-3530400.zip"] = NativeAotSqliteContract.SourceArchiveSha256;
            Receipt = new JsonObject
            {
                ["Schema"] = "clros-sqlite-linux-reference-v1",
                ["Succeeded"] = true,
                ["Failure"] = "",
                ["SourceArchiveSha256"] = NativeAotSqliteContract.SourceArchiveSha256,
                ["SourceArchiveSha3"] = NativeAotSqliteContract.SourceArchiveSha3,
                ["SourceId"] = NativeAotSqliteContract.SourceId,
                ["Imports"] = new JsonArray(Imports.Select(p => (JsonNode?)JsonValue.Create(p)).ToArray()),
                ["Archive"] = JsonSerializer.SerializeToNode(inspected),
                ["Native"] = native,
                ["Outputs"] = outputs
            };
            RehashCommands();
        }

        internal void RehashCommands()
        {
            Artifacts["commands.json"] = Encoding.UTF8.GetBytes(Commands.ToJsonString());
            Rehash("commands.json");
        }

        internal void Rehash(string name)
        {
            Receipt["Outputs"]![name] = Hash(Artifacts[name]);
        }

        internal NativeAotSqliteContract.ArchiveDependency Inspect()
        {
            byte[] bytes = Json(Receipt);
            return NativeAotSqliteContract.InspectArchiveDependencyValues(bytes, Hash(bytes), Artifacts, Imports, Compiler);
        }
    }
}
