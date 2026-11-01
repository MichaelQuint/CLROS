namespace OS.Kernel.Memory;
// Reference boundary:
// - SharpOS Cr3Accessor.cs, local snapshot SHA-256
//   088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47,
//   was used only to cross-check that CR3 restoration must preserve the
//   observed raw register value rather than reconstruct an address.
// - SharpOS BootAsmGenerator.cs, local snapshot SHA-256
//   B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500,
//   influenced deterministic plan-versus-materialized-byte auditing.
// - SharpOS PagingValidation.cs, local snapshot SHA-256
//   B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6,
//   supplied missing-map/alignment/readback negative-test categories only.
// No reference implementation, live CR3 operation, or firmware callback path
// is imported by this host-audited leaf.
/// <summary>
/// Records the only accepted opt-in policy for the reversible CR3 probe.
/// Default-enabled execution is always rejected.
/// </summary>

public readonly struct X64Cr3ProbePolicy
{
    public const ulong StructureSize = 16;
    private readonly ulong _timeoutTicks;
    private readonly byte _isInitialized;
    private readonly byte _enabledByDefault;
    private readonly byte _explicitOneShotOptIn;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    public X64Cr3ProbePolicy(bool enabledByDefault, bool explicitOneShotOptIn, ulong timeoutTicks)
    {
        _timeoutTicks = timeoutTicks;
        _isInitialized = 1;
        _enabledByDefault = enabledByDefault ? (byte)1 : (byte)0;
        _explicitOneShotOptIn = explicitOneShotOptIn ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _enabledByDefault <= 1 && _explicitOneShotOptIn <= 1 && _reserved0 == 0 && _reserved1 == 0;
    public bool EnabledByDefault => _enabledByDefault == 1;
    public bool ExplicitOneShotOptIn => _explicitOneShotOptIn == 1;
    public ulong TimeoutTicks => _timeoutTicks;
}
