using System.Collections.ObjectModel;
using System.Globalization;

namespace OS.Tooling;
public sealed record QemuSmokePlan(string BootPolicy, IReadOnlyDictionary<string, bool> Flags, IReadOnlyList<string> Required, IReadOnlyList<string> Ordered, IReadOnlyList<string> Unique, IReadOnlyList<string> Forbidden, IReadOnlyList<string> Presets, bool BuildEfi, bool ReplaceDefaultRequiredMarkers)
{
    public bool IsEnabled(string name) => Flags.TryGetValue(name, out bool enabled) && enabled;
    public bool IsNormalBoot => BootPolicy == "VerifiedExit";
}
