using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace OS.CompilerReceipts
{
    /// <summary>
    /// Captures declared host compiler inputs before compilation and publishes a
    /// receipt only from the SDK's successful-compilation hook. Callers must keep
    /// the trusted producer's files unchanged and serialize output ownership.
    /// Import paths are reevaluated with the current global properties; this does
    /// not intercept the original MSBuild evaluation. No generators are executed.
    /// </summary>
    public sealed class HostCompilationReceiptTask : Task
    {
        [Required] public string Mode { get; set; }
        [Required] public string Root { get; set; }
        [Required] public string Project { get; set; }
        [Required] public string ReceiptPath { get; set; }
        [Required] public string CompilerDirectory { get; set; }
        public ITaskItem[] Inputs { get; set; } = new ITaskItem[0];
        public ITaskItem[] Outputs { get; set; } = new ITaskItem[0];
        public ITaskItem[] Arguments { get; set; } = new ITaskItem[0];
        public string DesignTimeBuild { get; set; }
        public string SkipCompilerExecution { get; set; }
        public string DisableImplicitFrameworkReferences { get; set; }
        [Output] public string Token { get; set; }
        [Output] public string SnapshotXml { get; set; }
        private static StringComparer PathComparer => Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private static StringComparison PathComparison => Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        public override bool Execute()
        {
            try
            {
                Root = Path.GetFullPath(Root);
                Project = Path.GetFullPath(Project);
                ReceiptPath = Path.GetFullPath(ReceiptPath);
                RequireUnderRoot(Project);
                RequireUnderRoot(ReceiptPath);
                string relative = Project.Substring(Root.TrimEnd(Path.DirectorySeparatorChar).Length)
                    .TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/');
                if (relative.StartsWith("src/OS/", PathComparison)
                    || Enabled(DisableImplicitFrameworkReferences) || Enabled(DesignTimeBuild)
                    || (Mode != "Evaluate" && Enabled(SkipCompilerExecution)))
                    throw new InvalidOperationException("host_receipt_unsupported_producer");

                if (Mode == "Evaluate")
                {
                    if (!Enabled(SkipCompilerExecution) || Arguments.Length == 0)
                        throw new InvalidOperationException("host_receipt_evaluation_requires_skipped_compiler_arguments");
                    var snapshot = Capture();
                    ValidateArguments(snapshot);
                    SnapshotXml = snapshot.OuterXml;
                    return true;
                }

                var engine = BuildEngine as IBuildEngine4;
                if (engine == null)
                    throw new InvalidOperationException("host_receipt_build_engine");
                if (Mode == "Begin")
                {
                    Token = Guid.NewGuid().ToString("N");
                    engine.RegisterTaskObject(StateKey(), Capture().OuterXml,
                        RegisteredTaskObjectLifetime.Build, false);
                    return true;
                }
                if (Mode == "Mark")
                {
                    if (string.IsNullOrEmpty(Token)
                        || engine.GetRegisteredTaskObject(StateKey(), RegisteredTaskObjectLifetime.Build) == null)
                        throw new InvalidOperationException("host_receipt_no_same_build_snapshot");
                    engine.RegisterTaskObject(StateKey() + ":compiled", true, RegisteredTaskObjectLifetime.Build, false);
                    return true;
                }
                if (Mode != "Complete" || string.IsNullOrEmpty(Token))
                    throw new InvalidOperationException("host_receipt_mode_or_token");
                if (engine.UnregisterTaskObject(StateKey() + ":compiled", RegisteredTaskObjectLifetime.Build) == null)
                {
                    // CoreCompile was skipped. A prior receipt is never refreshed.
                    engine.UnregisterTaskObject(StateKey(), RegisteredTaskObjectLifetime.Build);
                    return true;
                }
                string before = engine.UnregisterTaskObject(StateKey(), RegisteredTaskObjectLifetime.Build) as string;
                if (before == null)
                    throw new InvalidOperationException("host_receipt_no_same_build_snapshot");
                XmlDocument current = Capture();
                if (!StringComparer.Ordinal.Equals(before, current.OuterXml))
                    throw new InvalidOperationException("host_receipt_inputs_changed");
                if (Arguments.Length == 0)
                    throw new InvalidOperationException("host_receipt_compiler_arguments_missing");
                ValidateArguments(current);
                var receipt = NewDocument("HostCompilationReceipt");
                receipt.DocumentElement.SetAttribute("Version", "1");
                receipt.DocumentElement.SetAttribute("Producer", "SdkTargetsTriggeredByCompilation");
                receipt.DocumentElement.SetAttribute("InputSha256", Hash(Encoding.UTF8.GetBytes(before)));
                receipt.DocumentElement.AppendChild(receipt.ImportNode(current.DocumentElement, true));
                var arguments = Add(receipt.DocumentElement, "Arguments");
                foreach (var argument in Arguments)
                    Add(arguments, "Argument").InnerText = argument.ItemSpec;
                var outputs = Add(receipt.DocumentElement, "Outputs");
                if (Outputs.Length != 2)
                    throw new InvalidOperationException("host_receipt_requires_pe_and_pdb");
                foreach (var output in Outputs)
                {
                    string path = FullPath(output.ItemSpec);
                    RequireUnderRoot(path);
                    AddFile(outputs, path, output.GetMetadata("ReceiptKind"));
                }
                SaveAtomic(receipt);
                Log.LogMessage(MessageImportance.Low, "HOST_COMPILATION_RECEIPT {0}", ReceiptPath);
                return true;
            }
            catch (Exception exception)
            {
                Log.LogError("host_compilation_receipt_failed: {0}", exception.Message);
                return false;
            }
        }

        private string StateKey() => "OS.HostCompilationReceipt:" + ReceiptPath + ":" + Token;
        private static bool Enabled(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        private string FullPath(string value) => Path.GetFullPath(Path.IsPathRooted(value)
            ? value : Path.Combine(Path.GetDirectoryName(Project), value));

        private XmlDocument Capture()
        {
            var document = NewDocument("Inputs");
            var root = document.DocumentElement;
            root.SetAttribute("Project", Project);
            root.SetAttribute("CompilerDirectory", Path.GetFullPath(CompilerDirectory));
            var declared = Add(root, "Declared");
            foreach (var input in Inputs)
            {
                string kind = input.GetMetadata("ReceiptKind");
                if (string.IsNullOrEmpty(kind))
                    throw new InvalidOperationException("host_receipt_input_kind");
                var file = AddFile(declared, FullPath(input.ItemSpec), kind);
                foreach (string metadata in new[] { "Aliases", "EmbedInteropTypes", "LogicalName", "Access", "Link" })
                    file.SetAttribute(metadata, input.GetMetadata(metadata));
            }
            var globals = ((IBuildEngine6)BuildEngine).GetGlobalProperties();
            // Global properties drive this evaluation. Persist their effects in
            // actual compiler arguments, editorconfig bytes and input inventories,
            // not arbitrary values that may contain secrets or equivalent defaults.
            var imports = Add(root, "EvaluatedImports");
            foreach (string path in EvaluatedImports(globals))
                AddFile(imports, path, "Import");
            var tools = Add(root, "CompilerFiles");
            foreach (string path in DirectoryFiles(CompilerDirectory))
                AddFile(tools, path, "Compiler");
            var dependencies = Add(root, "AnalyzerDirectories");
            var analyzerDirectories = Inputs.Where(input => input.GetMetadata("ReceiptKind") == "Analyzer")
                .Select(input => Path.GetDirectoryName(FullPath(input.ItemSpec)))
                .Distinct(PathComparer).OrderBy(path => path, PathComparer);
            foreach (string directory in analyzerDirectories)
                foreach (string path in DirectoryFiles(directory))
                    AddFile(dependencies, path, "AnalyzerDependency");
            return document;
        }

        private IEnumerable<string> EvaluatedImports(IReadOnlyDictionary<string, string> globals)
        {
            // Public MSBuild APIs are reflected solely to keep the inline task free
            // of a second Microsoft.Build assembly reference/bootstrap project.
            var collectionType = Type.GetType("Microsoft.Build.Evaluation.ProjectCollection, Microsoft.Build", true);
            var instanceType = Type.GetType("Microsoft.Build.Execution.ProjectInstance, Microsoft.Build", true);
            var collection = (IDisposable)Activator.CreateInstance(collectionType);
            try
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in globals)
                    values.Add(pair.Key, pair.Value);
                var constructor = instanceType.GetConstructor(new[]
                    { typeof(string), typeof(IDictionary<string, string>), typeof(string), collectionType });
                if (constructor == null)
                    throw new InvalidOperationException("host_receipt_import_api");
                object instance = constructor.Invoke(new object[] { Project, values, null, collection });
                var paths = ((IEnumerable)instanceType.GetProperty("ImportPaths").GetValue(instance))
                    .Cast<string>().Prepend(Project).ToArray();
                return paths;
            }
            finally
            {
                collection.Dispose();
            }
        }

        private void ValidateArguments(XmlDocument snapshot)
        {
            var compiler = Assembly.LoadFrom(Path.Combine(CompilerDirectory, "Microsoft.CodeAnalysis.CSharp.dll"));
            var parserType = compiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCommandLineParser", true);
            var parser = parserType.GetProperty("Default").GetValue(null);
            var parse = parserType.GetMethod("Parse", new[]
                { typeof(IEnumerable<string>), typeof(string), typeof(string), typeof(string) });
            var parsed = parse.Invoke(parser, new object[]
                { Arguments.Select(argument => argument.ItemSpec).ToArray(), Path.GetDirectoryName(Project), null, null });
            var errors = Values(parsed, "Errors");
            foreach (var error in errors)
                if (Get(error, "Severity").ToString() == "Error")
                    throw new InvalidOperationException("host_receipt_argument_parse " + error);
            MatchPaths(snapshot, "Source", Values(parsed, "SourceFiles").Select(item => Get(item, "Path").ToString()));
            MatchPaths(snapshot, "Analyzer", Values(parsed, "AnalyzerReferences").Select(item => Get(item, "FilePath").ToString()));
            MatchPaths(snapshot, "Additional", Values(parsed, "AdditionalFiles").Select(item => Get(item, "Path").ToString()));
            MatchPaths(snapshot, "AnalyzerConfig", Values(parsed, "AnalyzerConfigPaths").Cast<string>());
            MatchPaths(snapshot, "SourceLink", Optional(Get(parsed, "SourceLink") as string));
            MatchPaths(snapshot, "RuleSet", Optional(Get(parsed, "RuleSetPath") as string));
            MatchPaths(snapshot, "Win32Resource", Optional(Get(parsed, "Win32ResourceFile") as string));
            MatchPaths(snapshot, "Win32Icon", Optional(Get(parsed, "Win32Icon") as string));
            MatchPaths(snapshot, "Win32Manifest", Optional(Get(parsed, "Win32Manifest") as string));
            MatchPaths(snapshot, "AppConfig", Optional(Get(parsed, "AppConfigPath") as string));
            var embedded = snapshot.SelectNodes("/Inputs/Declared/File").Cast<XmlElement>()
                .Where(file => file.GetAttribute("Kind") == "Embedded"
                    || (Arguments.Any(argument => argument.ItemSpec.Equals("/embed", StringComparison.OrdinalIgnoreCase))
                        && file.GetAttribute("Kind") == "Source"))
                .Select(file => file.GetAttribute("Path")).Distinct(PathComparer)
                .OrderBy(path => path, PathComparer);
            var parsedEmbedded = Values(parsed, "EmbeddedFiles").Select(item => FullPath(Get(item, "Path").ToString()))
                .Distinct(PathComparer).OrderBy(path => path, PathComparer);
            if (!embedded.SequenceEqual(parsedEmbedded, PathComparer))
                throw new InvalidOperationException("host_receipt_argument_inventory Embedded");
            var resources = Values(parsed, "ManifestResourceArguments").ToArray();
            foreach (string kind in new[] { "Resource", "LinkResource" })
            {
                var actualResources = resources.Where(resource => (bool)Get(resource, "IsEmbedded") == (kind == "Resource")).ToArray();
                MatchPaths(snapshot, kind, actualResources.Select(resource => Get(resource, "FullPath").ToString()));
                var expectedResources = snapshot.SelectNodes("/Inputs/Declared/File").Cast<XmlElement>()
                    .Where(file => file.GetAttribute("Kind") == kind).ToArray();
                for (int index = 0; index < expectedResources.Length; index++)
                {
                    string name = expectedResources[index].GetAttribute("LogicalName");
                    if (name.Length == 0)
                        name = Path.GetFileName(expectedResources[index].GetAttribute("Path"));
                    bool isPublic = !expectedResources[index].GetAttribute("Access").Equals("private", StringComparison.OrdinalIgnoreCase);
                    if (name != Get(actualResources[index], "ResourceName").ToString() || isPublic != (bool)Get(actualResources[index], "IsPublic"))
                        throw new InvalidOperationException("host_receipt_resource_properties");
                }
            }
            var references = snapshot.SelectNodes("/Inputs/Declared/File").Cast<XmlElement>()
                .Where(file => file.GetAttribute("Kind") == "Reference")
                .SelectMany(file => (file.GetAttribute("Aliases").Length == 0 ? new[] { "" }
                    : file.GetAttribute("Aliases").Split(',').Where(alias => alias.Length != 0).Select(alias => alias.Trim()))
                    .Select(alias => new KeyValuePair<XmlElement, string>(file, alias == "global" ? "" : alias))).ToArray();
            var parsedReferences = Values(parsed, "MetadataReferences").ToArray();
            if (references.Length != parsedReferences.Length)
                throw new InvalidOperationException("host_receipt_argument_inventory Reference");
            for (int index = 0; index < references.Length; index++)
            {
                var properties = Get(parsedReferences[index], "Properties");
                var aliases = references[index].Value.Length == 0 ? new string[0] : new[] { references[index].Value };
                if (!PathComparer.Equals(references[index].Key.GetAttribute("Path"), FullPath(Get(parsedReferences[index], "Reference").ToString()))
                    || !aliases.SequenceEqual(Values(properties, "Aliases").Cast<string>(), StringComparer.Ordinal)
                    || Enabled(references[index].Key.GetAttribute("EmbedInteropTypes")) != (bool)Get(properties, "EmbedInteropTypes"))
                    throw new InvalidOperationException("host_receipt_reference_properties");
            }
            // Input-bearing switches outside this bounded adapter must be added
            // explicitly before any corresponding receipt can be accepted.
            foreach (var argument in Arguments)
            {
                string value = argument.ItemSpec;
                if (value.StartsWith("@", StringComparison.Ordinal)
                    || new[] { "/addmodule:", "/keyfile:", "/keycontainer:" }
                        .Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("host_receipt_unsupported_file_argument " + value.Split(':')[0]);
            }
            string assembly = FullPath(Path.Combine(Get(parsed, "OutputDirectory").ToString(), Get(parsed, "OutputFileName").ToString()));
            string symbols = Get(parsed, "PdbPath") as string;
            symbols = string.IsNullOrEmpty(symbols) ? Path.ChangeExtension(assembly, ".pdb") : FullPath(symbols);
            if (Mode != "Evaluate"
                && (!Outputs.Where(output => output.GetMetadata("ReceiptKind") == "PE").Select(output => FullPath(output.ItemSpec))
                    .SequenceEqual(new[] { assembly }, PathComparer)
                    || !Outputs.Where(output => output.GetMetadata("ReceiptKind") == "PDB").Select(output => FullPath(output.ItemSpec))
                        .SequenceEqual(new[] { symbols }, PathComparer)))
                throw new InvalidOperationException("host_receipt_output_argument_inventory");
        }

        private static IEnumerable<string> Optional(string path) => string.IsNullOrEmpty(path) ? new string[0] : new[] { path };

        private void MatchPaths(XmlDocument snapshot, string kind, IEnumerable<string> actual)
        {
            var expected = snapshot.SelectNodes("/Inputs/Declared/File").Cast<XmlElement>()
                .Where(file => file.GetAttribute("Kind") == kind).Select(file => file.GetAttribute("Path")).ToArray();
            if (!expected.SequenceEqual(actual.Select(FullPath), PathComparer))
                throw new InvalidOperationException("host_receipt_argument_inventory " + kind);
        }

        private static object Get(object instance, string name)
        {
            var property = instance.GetType().GetProperty(name);
            if (property != null)
                return property.GetValue(instance);
            var field = instance.GetType().GetField(name);
            if (field != null)
                return field.GetValue(instance);
            throw new InvalidOperationException("host_receipt_compiler_api " + name);
        }

        private static IEnumerable<object> Values(object instance, string name) => ((IEnumerable)Get(instance, name)).Cast<object>();

        private static IEnumerable<string> DirectoryFiles(string directory)
        {
            RequirePlain(directory);
            var result = new List<string>();
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                RequirePlain(path);
                if (Directory.Exists(path))
                    result.AddRange(DirectoryFiles(path));
                else
                    result.Add(Path.GetFullPath(path));
            }
            return result.OrderBy(path => path, PathComparer);
        }

        private static XmlDocument NewDocument(string name)
        {
            var document = new XmlDocument { XmlResolver = null };
            document.AppendChild(document.CreateElement(name));
            return document;
        }

        private static XmlElement Add(XmlElement parent, string name)
        {
            var child = parent.OwnerDocument.CreateElement(name);
            parent.AppendChild(child);
            return child;
        }

        private static XmlElement AddFile(XmlElement parent, string path, string kind)
        {
            RequirePlain(path);
            var file = Add(parent, "File");
            file.SetAttribute("Path", Path.GetFullPath(path));
            file.SetAttribute("Kind", kind);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var hash = SHA256.Create())
            {
                file.SetAttribute("Length", stream.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                file.SetAttribute("Sha256", BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""));
            }
            return file;
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }

        private void RequireUnderRoot(string path)
        {
            if (!path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                PathComparison))
                throw new InvalidOperationException("host_receipt_path_outside_root");
            RequirePlain(path);
        }

        private static void RequirePlain(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current))
                    && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("host_receipt_link_path");
        }

        private void SaveAtomic(XmlDocument receipt)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReceiptPath));
            string temporary = ReceiptPath + "." + Token + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
                    receipt.Save(writer);
                if (File.Exists(ReceiptPath))
                    File.Replace(temporary, ReceiptPath, null);
                else
                    File.Move(temporary, ReceiptPath);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
