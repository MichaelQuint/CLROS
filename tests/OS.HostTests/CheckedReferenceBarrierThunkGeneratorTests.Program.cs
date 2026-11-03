#if CHECKED_REFERENCE_THUNK_CHILD
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.X64TextSerializerGenerator;

internal static class Program
{
    public static int Main(string[] args)
    {
        const string group = "m04.010.thunk.offline";
        Console.WriteLine("TEST_START " + group);
        try
        {
            ThunkControls.Need(args.Length == 3, "output and reviewed serializer hash and size arguments");
            object result = ThunkControls.Run(args[0], args[1], int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(args[0], "controls.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
            Console.WriteLine("TEST_PASS " + group);
            Console.WriteLine("TEST_PASS host.tests");
            return 0;
        }
        catch (Exception e) { Console.WriteLine("TEST_FAIL " + group + " " + e.Message); Console.WriteLine("TEST_FAIL host.tests failures=1"); Console.Error.WriteLine(e); return 1; }
    }
}
#else
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
#endif
