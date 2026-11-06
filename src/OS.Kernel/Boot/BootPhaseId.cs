namespace OS.Boot;

public enum BootPhaseId
{
    PhaseA_Audit = 0,
    PhaseA_Diagnostics = 1,
    PhaseB_Critical = 2,
    PhaseC_Memory = 3,
    PhaseD_Runtime = 4,
    PhaseE_Platform = 5,
    PhaseF_Probes = 6,
    PhaseG_Apps = 7
}
