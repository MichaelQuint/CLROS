using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static OS.Tooling.BacklogManifest;

namespace OS.Tooling;
public static partial class BacklogSync
{
    private sealed class Actions(TextWriter output)
    {
        private static readonly string[] Names = ["created", "updated", "unchanged", "skipped", "failed", "wouldCreate", "wouldUpdate"];
        private readonly Dictionary<string, List<string>> values = Names.ToDictionary(x => x, _ => new List<string>(), StringComparer.Ordinal);
        public void Add(string action, string key) => values[action].Add(key);
        public int Count(string action) => values[action].Count;
        public void Write()
        {
            foreach (string action in Names)
            {
                string[] keys = values[action].Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (keys.Length == 0)
                    output.WriteLine("SYNC_KEYS action=" + action + " count=0 keys=none");
                for (int offset = 0; offset < keys.Length; offset += 50)
                    output.WriteLine($"SYNC_KEYS action={action} count={keys.Length} chunk={offset / 50 + 1}/{(keys.Length + 49) / 50} keys={string.Join(',', keys.Skip(offset).Take(50))}");
            }

            output.WriteLine("SYNC_SUMMARY " + string.Join(' ', Names.Select(x => x + "=" + values[x].Count)));
        }
    }
}
