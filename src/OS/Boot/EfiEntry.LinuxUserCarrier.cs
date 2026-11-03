#if M16_027_PROBE
namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {










        private static LinuxUserCarrier s_linuxUserCarrier;

        // Prepare once before startup admission can invoke its phase-three
        // clock callback. The pre-exit cooperative reservation is already
        // recorded in the original retained mapping references by this point.
        // A repeated call may validate the same stamp, never replace it.
        private static bool PrepareLinuxUserCarrier(Storage* state)
        {
            fixed (Storage* original = &s_storage)
            fixed (LinuxUserCarrier* carrier = &s_linuxUserCarrier)
            {
                if (state != original || !LinuxUserCarrierPool(state)) return false;
                if (carrier->Self != 0 || carrier->Boot != null || carrier->OriginalCooperativePool != 0)
                    return LinuxUserCarrierMatches(carrier, state);
                carrier->Boot = state;
                carrier->OriginalCooperativePool = state->CooperativePool;
                carrier->Self = (ulong)carrier;
                return true;
            }
        }

        // Canonical copied Context for every selected user-access admission.
        // The caller must prove this entire carrier is mapped supervisor RW/NX
        // and disjoint from all retained trees before publishing those owners.
        private static void* LinuxUserCallbackContext(Storage* state)
        {
            fixed (Storage* original = &s_storage)
            fixed (LinuxUserCarrier* carrier = &s_linuxUserCarrier)
                return state == original && LinuxUserCarrierMatches(carrier, state) ? carrier : null;
        }

        // Independently check each copied selected admission against the actual
        // canonical carrier and its whole size, not another copied admission.
        // Exact context/size still grants no pointee, mapping or code authority.
        private static bool LinuxUserCarrierCurrent(Storage* state, void* context, ulong bytes)
        {
            fixed (Storage* original = &s_storage)
            fixed (LinuxUserCarrier* carrier = &s_linuxUserCarrier)
                return state == original && context == carrier && bytes == (ulong)sizeof(LinuxUserCarrier) &&
                    LinuxUserCarrierMatches(carrier, state);
        }

        // Identify only either original static address before reading carrier
        // bytes. Exact Storage* is retained for internal native/temporary access
        // callers; recognizing its address cannot establish actual call origin.
        // A malformed carrier never falls back to its Boot pointer.
        // This pure decoder must not require a published process: startup uses
        // the carrier before LinuxNativeControl or any worker is initialized.
        private static Storage* ResolveLinuxUserCallbackStorage(void* context)
        {
            fixed (Storage* original = &s_storage)
            fixed (LinuxUserCarrier* carrier = &s_linuxUserCarrier)
            {
                if (context == original) return original;
                return context == carrier && LinuxUserCarrierMatches(carrier, original) ? original : null;
            }
        }

        private static bool LinuxUserCarrierMatches(LinuxUserCarrier* carrier, Storage* state) =>
            carrier->Self == (ulong)carrier && carrier->Boot == state &&
            carrier->OriginalCooperativePool == state->CooperativePool && LinuxUserCarrierPool(state);

        private static bool LinuxUserCarrierPool(Storage* state) =>
            state->CooperativePool >= Page && (state->CooperativePool & (Page - 1)) == 0 &&
            state->CooperativePool <= 0x100000000UL - CooperativePoolPages * Page &&
            state->Memory.References.CooperativePoolStart == state->CooperativePool &&
            state->Memory.References.CooperativePoolPages == CooperativePoolPages;
    }
}
#endif
