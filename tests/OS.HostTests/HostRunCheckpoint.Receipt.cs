using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Checkpoints retain historical evidence. They never replay TEST_PASS output.
internal sealed partial class HostRunCheckpoint
{
    internal sealed class Receipt
    {
        public int Schema { get; set; } = 1;
        public string Root { get; set; } = "";
        public string Machine { get; set; } = "";
        public Selection Selection { get; set; } = new("fast", null, null);
        public Dictionary<string, Entry> Entries { get; set; } = new(StringComparer.Ordinal);
    }
}
