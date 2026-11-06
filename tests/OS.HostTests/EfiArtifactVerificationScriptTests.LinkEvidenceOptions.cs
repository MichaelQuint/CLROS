using System.Diagnostics;
using OS.Tooling;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static partial class EfiArtifactVerificationScriptTests
{
    internal sealed class LinkEvidenceOptions
    {
        public uint MappedEntryRva { get; init; } = EntryPointRva;
        public uint SerializerRva { get; init; } = EfiArtifactVerificationScriptTests.SerializerRva;
        public int SerializerSymbolCopies { get; init; } = 1;
        public string? MapOwner
        {
            get; init;
        }
        public int ForceIncludeCopies { get; init; } = 1;
        public int BreproCopies { get; init; } = 1;
        public string BreproToken { get; init; } = "/BREPRO";
        public int ObjectTokenCopies { get; init; } = 1;
        public int MapOptionCopies { get; init; } = 1;
        public int MapInfoCopies { get; init; } = 1;
        public string? MapPathOverride
        {
            get; init;
        }
        public string? AdditionalResponse
        {
            get; init;
        }
    }
}
