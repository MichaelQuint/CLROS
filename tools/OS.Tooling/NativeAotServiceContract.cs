namespace OS.Tooling;

/// <summary>Explicit immutable diagnostic package policy. Selecting a value
/// starts no process and grants no original-image or guest authority.</summary>
public enum NativeAotServiceContract
{
    /// <summary>Original finite continuing-service envelope.</summary>
    V1 = 1,
    /// <summary>Configured GC budget and asynchronous request lifecycle.</summary>
    V2 = 2,
    /// <summary>Explicit quota-negative lifecycle fixture with termination on
    /// proven physical backing exhaustion. Requires independent cleanup proof.</summary>
    QuotaFailure = 3
}
