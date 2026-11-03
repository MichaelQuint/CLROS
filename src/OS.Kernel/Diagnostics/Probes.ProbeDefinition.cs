namespace OS.Kernel.Diagnostics;
#if !NOSTDLIB_EFI
public readonly struct ProbeDefinition(string group, string name, bool enabledByDefault, ProbeRisk risk, string description)
{
    public string Group { get; } = group;
    public string Name { get; } = name;
    public bool EnabledByDefault { get; } = enabledByDefault;
    public ProbeRisk Risk { get; } = risk;
    public string Description { get; } = description;
}
#endif
