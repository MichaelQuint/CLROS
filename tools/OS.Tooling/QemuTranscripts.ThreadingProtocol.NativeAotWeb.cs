using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        internal WebReplayReport ValidateWeb(WebObservationReport observations)
        {
            Need(web, "web_replay_profile");
            try
            {
                _ = Validate();
                throw new ToolFailure("web_replay_unexpected_finite_return");
            }
            catch (WebBoundary boundary)
            {
                Need(position == rows.Length && active > 0 && !stopping && calls == observations.Calls && writes == writeLengths.Count && irqs > 0 && clones > 0 && entropyBytes > 0 && entropyBytes <= entropyLimit && barrierRegistered && signalPipeCreated && stackProbe && new ulong[] { 2, 3, 8, 11, 13, 18, 35 }.All(actions.Contains), "web_replay_incomplete_startup");
                Need(history.Count(t => !t.Reaped) == active && ready.All(t => !t.Reaped && (t != socketSelected || webReturningToSupervisor)) && socketDescriptors.Values.Any(d => !d.Port && d.Phase == 2) && socketAccepts >= 2 && socketSent > 0 && socketReceived > 0, "web_replay_live_owner");
                ulong current = socketSelected?.Identity ?? 0;
                if (webControls is not null)
                {
                    Need(webControlEvents == 2 && webPrivateRetired, "web_replay_controls_incomplete");
                    WebControlIo(before: false, requirePrivate: true, allowLaterAccepts: webRaceTransport);
                    Need(!webRaceTransport || socketAccepts == webLifetimes!.Count, "web_replay_race_census");
                }

                WebReplayFragment(observations.TrailingFragment, boundary.Expected);
                int completedTransfers = rows.Count(r => r.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal)) * 2;
                return new(observations, new(calls, clones, reaped, irqs, drains, ticks, completedTransfers, signalReturns), active, ready.Count, current, boundary.Expected, webReturningToSupervisor, checked(entropyLimit - (uint)entropyBytes), socketAccepts, socketSent, socketReceived, webControls is not null);
            }
        }

        private void WebReplayFragment(string fragment, string expected)
        {
            if (fragment.Length == 0)
                return;
            string[] tokens = fragment.Split(' ');
            string kind = tokens[0];
            bool network = kind is "M16_012_POLL" or "M16_012_WAKE";
            bool allowed = expected switch
            {
                "M16_008_SUPERVISOR" => kind == expected && webReturningToSupervisor,
                "M16_009_IRQ" => network || kind is "M16_008_CALL" or "M16_009_IRQ" or "M16_008_SYSCALL_TIME",
                "M16_009_DRAIN" => network || kind is "M16_009_DRAIN" or "M16_008_SYSCALL_TIME",
                "dispatch_or_idle" => network || ready.Count > 0 && kind is "M16_008_CALL" or "M16_009_IRQ" or "M16_008_RESUME",
                _ => kind == expected
            };
            Need(allowed, "web_replay_fragment_stage");
            // The final numeric token may itself be incomplete. Only a field
            // followed by a delimiter supplies a complete identity observation.
            if (tokens.Length < 3)
                return;
            ulong identity = ulong.Parse(tokens[1][9..], CultureInfo.InvariantCulture);
            if (network)
                Need(kind == "M16_012_POLL" ? identity == (socketSelected?.Identity ?? 0) : history.Any(t => !t.Reaped && t.Identity == identity && t.WaitSource == 3), "web_replay_fragment_owner");
            else
            {
                ulong owner = socketSelected?.Identity ?? (ready.TryPeek(out var next) ? next.Identity : 0);
                Need(owner != 0 && identity == owner, "web_replay_fragment_owner");
            }
        }
    }
}
