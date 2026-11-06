using System.Reflection;
using OS.Tooling;

internal static partial class WhpxPrelaunchTests
{
    private static Action<IList<string>> ConfigureHostNotifiers => Target
        .GetMethod("ConfigureVirtioNotifiers", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Action<IList<string>>>();

    [HostTest("WHPX compatibility disables only the supported virtio notifier acceleration")]
    private static void HostNotifierDevices()
    {
        string[] arguments = ["-machine", Machine, "-m", "256M", "-device", "virtio-net-pci,netdev=n0,addr=06.0",
            "-device", "virtio-blk-pci,drive=webroot,addr=07.0", "-device", "virtio-rng-pci,rng=clrosrng",
            "-device", "e1000,netdev=other", "-netdev", "user,id=n0", "-cpu", "Nehalem"];
        string[] expected = ["-machine", Machine, "-m", "256M", "-device", "virtio-net-pci,netdev=n0,addr=06.0,ioeventfd=off",
            "-device", "virtio-blk-pci,drive=webroot,addr=07.0,ioeventfd=off", "-device", "virtio-rng-pci,rng=clrosrng,ioeventfd=off",
            "-device", "e1000,netdev=other", "-netdev", "user,id=n0", "-cpu", "Nehalem"];
        ConfigureHostNotifiers(arguments);
        HostCheck.True(arguments.SequenceEqual(expected));
        ConfigureHostNotifiers(arguments);
        HostCheck.True(arguments.SequenceEqual(expected));
    }

    [HostTest("WHPX notifier policy leaves invocations without those devices unchanged")]
    private static void HostNotifierUnchanged()
    {
        string[] arguments = ["-machine", Machine, "-device", "ich9-ahci,id=ahci", "-drive", "if=none,id=esp,file=boot.raw"];
        string[] original = arguments.ToArray();
        ConfigureHostNotifiers(arguments);
        HostCheck.True(arguments.SequenceEqual(original));
        ConfigureHostNotifiers(Array.Empty<string>());
    }

    [HostTest("WHPX notifier conflicts and malformed devices fail without partial mutation")]
    private static void HostNotifierRejected()
    {
        foreach (string invalid in new[] { "virtio-net-pci,ioeventfd=on", "virtio-blk-pci,ioeventfd=off,ioeventfd=off",
            "virtio-rng-pci,ioeventfd=off,ioeventfd=on", "virtio-rng-pci,ioeventfd=" })
        {
            string[] arguments = ["-device", "virtio-net-pci,netdev=n0", "-device", invalid];
            string[] original = arguments.ToArray();
            bool rejected = false;
            try
            {
                ConfigureHostNotifiers(arguments);
            }
            catch (IOException error)
            {
                rejected = error.Message == "whpx_notifier_override";
            }
            HostCheck.True(rejected && arguments.SequenceEqual(original));
        }

        string[] missing = ["-device", "virtio-rng-pci", "-device"];
        bool missingRejected = false;
        try
        {
            ConfigureHostNotifiers(missing);
        }
        catch (IOException error)
        {
            missingRejected = error.Message == "whpx_notifier_device_missing";
        }
        HostCheck.True(missingRejected && missing[1] == "virtio-rng-pci");
    }
}
