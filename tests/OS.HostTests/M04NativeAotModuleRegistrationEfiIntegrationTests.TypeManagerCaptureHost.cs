using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Tooling;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;
using Probe = OS.Runtime.NativeAotModuleRegistrationProbe;
using Status = OS.Kernel.Runtime.TypeManagerModuleStatus;

internal static unsafe partial class TypeManagerCaptureHost
{
    private static void Check(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START m04.typemanager.host " + name);
        test();
        Console.WriteLine("TEST_PASS m04.typemanager.host " + name);
    }

    internal static int ExecuteCases()
    {
        Console.WriteLine("TEST_START m04.typemanager.host.tests");
        Run("actual adapter register slot-derived consumer and tail canaries", () =>
        {
            using Fixture f = new();
            Check(sizeof(NativeAotTypeManagerAbi) == 48 && Core.StateBytes == 272 && sizeof(Probe.Buffer) == 4096, "actual geometry");
            Check(Probe.Execute(f.Capture, f.Mapping, f.Storage, 4096, out var o), "genuine adapter execution " + o.Failure);
            Check(o.Image == (ulong)f.Image && o.Storage == (ulong)f.Storage && o.Handle == (ulong)f.Storage + 272 && o.Slot == f.Address(0x2000), "actual pointer lineage");
            Check(o.Short == Status.StorageRejected && o.Repeat == Status.AlreadyRegistered && o.Foreign == Status.RegistrationChanged, "actual rejection statuses");
            Check(Probe.ReadWord(o.Slot) == o.Handle && Probe.ReadWord(o.Slot + 8) == 0 && o.Abi.OsModule == o.Image && o.Abi.ReadyToRunHeader == f.Address(0x1000), "slot ABI readback");
            Check(o.Thread.Section == f.Address(0x2100) && o.Thread.Length == 8 && o.Indirection.Section == o.Slot && o.Indirection.Length == 16 && o.Empty.Succeeded && o.Empty.Length == 0 && o.Absent.Status == Status.SectionAbsent, "actual consumer results");
            byte[] before = Bytes(f.Image, Fixture.ImageSize);
            Check(!Probe.Execute(f.Capture, f.Mapping, f.Storage, 4096, out _) && Bytes(f.Image, Fixture.ImageSize).SequenceEqual(before), "second adapter cannot reset retained bytes");
            Console.WriteLine("TYPE_MANAGER_HOST_LAYOUT abi=48 state=" + Core.StateBytes + " result=" + sizeof(TypeManagerModuleResult) + " observation=" + sizeof(Probe.Observation) + " capacity=4096 guardStart=4064");
        });
        Run("known mapped short buffer rejects without image or slot writes", () =>
        {
            using Fixture f = new();
            var valid = Core.Validate(f.Capture, f.Mapping);
            Check(valid.Succeeded && valid.RequiredStorage <= 4064, "actual required capacity");
            byte[] before = Bytes(f.Image, Fixture.ImageSize);
            var shortResult = Core.RegisterOnce(f.Capture, f.Mapping, f.Storage, valid.RequiredStorage - 1);
            Check(shortResult.Status == Status.StorageRejected && Bytes(f.Image, Fixture.ImageSize).SequenceEqual(before), "controlled full image includes full slot and buffer");
            foreach (int selected in new[]
            {
                0,
                1,
                2,
                3,
                4
            }

            )
            {
                byte* storage = selected == 0 ? null : selected == 1 ? f.Storage + 1 : selected == 2 ? (byte*)(ulong.MaxValue - 7) : f.Storage;
                ulong capacity = selected == 3 ? 4095UL : 4096UL;
                var capture = selected == 4 ? default : f.Capture;
                Check(!Probe.Execute(capture, f.Mapping, storage, capacity, out _) && Bytes(f.Image, Fixture.ImageSize).SequenceEqual(before), "numeric rejection unchanged " + selected);
            }

            f.Storage[4095] = 1;
            before = Bytes(f.Image, Fixture.ImageSize);
            Check(!Probe.Execute(f.Capture, f.Mapping, f.Storage, 4096, out _) && Bytes(f.Image, Fixture.ImageSize).SequenceEqual(before), "nonzero known tail rejects before registration");
        });
        Run("both internal tail strips and retained padding are read back", () =>
        {
            using Fixture f = new();
            Check(Probe.Execute(f.Capture, f.Mapping, f.Storage, 4096, out var o), "register");
            foreach (int index in new[]
            {
                (int)o.Required,
                4063,
                4064,
                4079,
                4080,
                4095
            }

            )
            {
                byte saved = f.Storage[index];
                f.Storage[index] ^= 1;
                Check(!Probe.Preserved(f.Storage, o.Required), "changed tail detected " + index);
                f.Storage[index] = saved;
                Check(Probe.Preserved(f.Storage, o.Required), "unchanged restored tail");
            }

            byte[] before = Bytes(f.Image, Fixture.ImageSize);
            Check(Core.RegisterOnce(f.Capture, f.Mapping, f.Storage, 4096).Status == Status.AlreadyRegistered && Bytes(f.Image, Fixture.ImageSize).SequenceEqual(before), "repeat all bytes unchanged");
            Check(Core.TryGetSection(f.Capture, f.Mapping, f.Storage, 4096, o.Handle + 8, 204).Status == Status.RegistrationChanged && Bytes(f.Image, Fixture.ImageSize).SequenceEqual(before), "foreign all bytes unchanged");
        });
        Run("real metadata and published slot tampering cannot yield a section", () =>
        {
            for (int selected = 0; selected < 4; selected++)
            {
                using Fixture f = new();
                Check(Probe.Execute(f.Capture, f.Mapping, f.Storage, 4096, out var o), "register");
                if (selected == 0)
                    W64(f.Row(0) + 8, f.Address(0x2110));
                if (selected == 1)
                    W64(f.Image + 0x2000, o.Handle + 8);
                if (selected == 2)
                    f.Storage[272] ^= 1;
                if (selected == 3)
                    f.Image[0x200C] = 1;
                byte[] before = Bytes(f.Image, Fixture.ImageSize);
                Check(!Core.TryGetSection(f.Capture, f.Mapping, f.Storage, 4096, o.Handle, 204).Succeeded && Bytes(f.Image, Fixture.ImageSize).SequenceEqual(before), "tamper no writes " + selected);
            }
        });
        Console.WriteLine("TEST_PASS m04.typemanager.host.tests");
        return 0;
    }

    private static EfiLoadedImageRangeCapturePlan Capture(ulong address, ulong bytes, ulong handle = 0x1000)
    {
        var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, handle, 0x2000, address, bytes);
        return EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
    }

    private static byte[] Bytes(byte* p, int length) => new ReadOnlySpan<byte>(p, length).ToArray();
    private static void W16(byte* p, ushort v)
    {
        p[0] = (byte)v;
        p[1] = (byte)(v >> 8);
    }

    private static void W32(byte* p, uint v)
    {
        W16(p, (ushort)v);
        W16(p + 2, (ushort)(v >> 16));
    }

    private static void W64(byte* p, ulong v)
    {
        W32(p, (uint)v);
        W32(p + 4, (uint)(v >> 32));
    }
}
