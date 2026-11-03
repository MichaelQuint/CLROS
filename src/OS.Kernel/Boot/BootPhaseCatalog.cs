namespace OS.Boot;

public static class BootPhaseCatalog
{
    public const string PhaseMarkerPrefix = "BOOT_PHASE";
    public const int ImplementedPhaseCount = 2;
    public const int KnownPhaseCount = 8;
    public static BootPhaseInfo GetImplementedPhase(int index)
    {
        return index switch
        {
            0 => Get(BootPhaseId.PhaseA_Audit),
            1 => Get(BootPhaseId.PhaseA_Diagnostics),
            _ => default
        };
    }

    public static BootPhaseInfo Get(BootPhaseId id)
    {
        return id switch
        {
            BootPhaseId.PhaseA_Audit => new BootPhaseInfo(id, "PhaseA.Audit", "Repository baseline, marker contract, and first-party source boundaries are available.", "Boot audit marker emitted and early ordering evidence can be recorded.", PhaseMarkerPrefix),
            BootPhaseId.PhaseA_Diagnostics => new BootPhaseInfo(id, "PhaseA.Diagnostics", "A serial sink is available and boot audit markers have been emitted.", "Basic diagnostics marker emitted and smoke status can be reported.", PhaseMarkerPrefix),
            BootPhaseId.PhaseB_Critical => new BootPhaseInfo(id, "PhaseB.Critical", "Firmware handoff metadata and early diagnostics are available.", "Critical CPU tables and fault diagnostics are initialized.", PhaseMarkerPrefix),
            BootPhaseId.PhaseC_Memory => new BootPhaseInfo(id, "PhaseC.Memory", "UEFI memory-map metadata has been captured and copied into OS-owned storage.", "Page ownership and allocator prerequisites are established.", PhaseMarkerPrefix),
            BootPhaseId.PhaseD_Runtime => new BootPhaseInfo(id, "PhaseD.Runtime", "Memory ownership and runtime helper prerequisites are available.", "NativeAOT runtime services needed by later phases are initialized.", PhaseMarkerPrefix),
            BootPhaseId.PhaseE_Platform => new BootPhaseInfo(id, "PhaseE.Platform", "Runtime and diagnostics can operate without firmware services.", "Platform tables, interrupts, timers, PCI, and owned diagnostics are initialized.", PhaseMarkerPrefix),
            BootPhaseId.PhaseF_Probes => new BootPhaseInfo(id, "PhaseF.Probes", "Platform drivers and diagnostics have reached a probe-safe boundary.", "Enabled safe probes have emitted deterministic pass/fail markers.", PhaseMarkerPrefix),
            BootPhaseId.PhaseG_Apps => new BootPhaseInfo(id, "PhaseG.Apps", "Storage, manifest loading, runtime services, and app ABI are available.", "Configured single workload has started or produced controlled failure diagnostics.", PhaseMarkerPrefix),
            _ => default
        };
    }
}
