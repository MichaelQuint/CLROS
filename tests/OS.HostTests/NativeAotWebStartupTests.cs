using System.Reflection;
using System.Runtime.Loader;
using System.Text;

internal static partial class NativeAotWebStartupTests
{
    [HostTest("m10.006 real managed app constructor failures emit failure before build or bind")]
    private static void ConstructorFailures()
    {
        foreach (string stage in new[]
        {
            "CCTOR",
            "CTOR"
        }

        )
        {
            // Load the actual app into a fresh context so a failed type initializer
            // cannot contaminate the other case. Failure occurs before Kestrel starts.
            var context = new AssemblyLoadContext("startup-failure-" + stage, isCollectible: true);
            var writer = new ConstructorFailureWriter("CLROS_APP_" + stage + " minimal.api.smoke");
            TextWriter original = Console.Error;
            Exception? failure = null;
            try
            {
                Assembly app = context.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "MinimalApiSmoke.dll"));
                MethodInfo entry = app.EntryPoint ?? throw new InvalidDataException("app entry missing");
                Console.SetError(writer);
                try
                {
                    entry.Invoke(null, [Array.Empty<string>()]);
                }
                catch (TargetInvocationException exception)
                {
                    failure = exception.InnerException;
                }
            }
            finally
            {
                Console.SetError(original);
                context.Unload();
            }

            HostCheck.Equal(1, writer.Failures);
            HostCheck.True(stage == "CCTOR" ? failure is TypeInitializationException : failure is IOException, stage);
            string observed = writer.Output.ToString();
            HostCheck.True(observed.StartsWith("CLROS_APP_ENTRY minimal.api.smoke\n", StringComparison.Ordinal));
            HostCheck.True(observed.EndsWith("CLROS_APP_FAIL minimal.api.smoke\n", StringComparison.Ordinal), stage);
            HostCheck.Equal(false, observed.Contains("CLROS_APP_BUILT", StringComparison.Ordinal));
            HostCheck.Equal(false, observed.Contains("Now listening", StringComparison.Ordinal));
        }
    }
}
