/// <summary>Immutable selected file profile and confined guest mount exposed by the diagnostic endpoint.</summary>
public sealed record StaticFileConfigurationResponse(string AppId, string Profile, string WebRoot);
