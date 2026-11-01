namespace OS.Boot;
public readonly struct BootPhaseInfo(BootPhaseId id, string name, string precondition, string postcondition, string markerPrefix)
{
    public BootPhaseId Id { get; } = id;
    public string Name { get; } = name;
    public string Precondition { get; } = precondition;
    public string Postcondition { get; } = postcondition;
    public string MarkerPrefix { get; } = markerPrefix;
    public string StartMarker => MarkerPrefix + " " + Name + " START";
    public string PassMarker => MarkerPrefix + " " + Name + " PASS";
}
