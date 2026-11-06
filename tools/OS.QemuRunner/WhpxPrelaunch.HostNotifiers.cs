using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OS.Tools.QemuRunner;

internal sealed partial class WhpxPrelaunch
{
    /// <summary>Configure the unstarted, exclusively owned invocation after the
    /// executable hash and Windows/WHPX recipe have been authenticated. Disable
    /// the affected binary's optional virtio host event-notifier acceleration;
    /// preserve every other argument, resource bound and native stderr check.
    /// Conflicting or duplicate overrides throw before any argument is changed.
    /// Other binaries and accelerators never call this compatibility policy.</summary>
    internal static void ConfigureVirtioNotifiers(IList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        List<int> devices = [];
        for (int i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] != "-device")
                continue;
            if (++i == arguments.Count)
                throw new IOException("whpx_notifier_device_missing");
            string[] parts = arguments[i].Split(',');
            if (parts[0] is not ("virtio-net-pci" or "virtio-blk-pci" or "virtio-rng-pci"))
                continue;
            string[] configured = parts.Skip(1).Where(p => p.StartsWith("ioeventfd=", StringComparison.Ordinal)).ToArray();
            if (configured.Length > 1 || configured.Length == 1 && configured[0] != "ioeventfd=off")
                throw new IOException("whpx_notifier_override");
            if (configured.Length == 0)
                devices.Add(i);
        }

        foreach (int device in devices)
            arguments[device] += ",ioeventfd=off";
    }
}
