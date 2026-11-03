using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OS.Tooling;

public sealed record EfiSnapshot(IReadOnlyList<EfiInput> Inputs, string AggregateSha256, long TotalBytes)
{
    public int FileCount => Inputs.Count;

    public JsonArray ToJson(bool includeBytes = false) => new(Inputs.Select(i => (JsonNode)new JsonObject { ["path"] = i.Path, ["sha256"] = i.Sha256 }).Select((n, i) =>
    {
        if (includeBytes)
            n["bytes"] = Inputs[i].Bytes;
        return n;
    }).ToArray());
}
