using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

internal static class HostCheck
{
    public static int Failures
    {
        get; private set;
    }
    public static int Executed
    {
        get; private set;
    }
    public static string? Filter
    {
        get; set;
    }

    public static void Registered(Type fixture)
    {
        True(fixture.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Any(method => method.IsDefined(typeof(HostFixtureAttribute)) || method.IsDefined(typeof(HostTestAttribute))), "behavioral fixture must remain registered: " + fixture.Name);
    }

    public static void SingleFixture(Type owner, string expectedMethod, params Type[] coordinatorOnly)
    {
        var discovery = HostTestRunner.Discover();
        var entries = discovery.Where(item => item.Method.DeclaringType == owner).ToArray();
        True(entries.Length == 1, "explicit fixture registration must be unique: " + owner.Name);
        True(entries[0].Method.Name == expectedMethod && entries[0].Name is null, "explicit fixture entry must be the aggregate method: " + owner.Name + "." + expectedMethod);
        foreach (Type type in new[]
        {
            owner
        }.Concat(coordinatorOnly))
            True(!type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Any(method => method.IsDefined(typeof(ModuleInitializerAttribute))), "fixture must not run through a module initializer: " + type.Name);
        foreach (Type type in coordinatorOnly)
            True(!discovery.Any(item => item.Method.DeclaringType == type), "coordinator-only fixture must not be discovered independently: " + type.Name);
    }

    public static void Run(string name, Func<bool> test, ref int failures)
    {
        if (!Selected(name))
            return;
        Executed++;
        Console.WriteLine("TEST_START " + name);
        try
        {
            if (!test())
                throw new InvalidOperationException("assertion_returned_false method=" + test.Method.Name);
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception error)
        {
            failures++;
            Failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + error.ToString().ReplaceLineEndings(" "));
        }
    }

    public static void Run(string name, Action test, ref int failures) => Run(name, () =>
    {
        test();
        return true;
    }, ref failures);
    public static void Run(string name, Action test)
    {
        int failures = 0;
        Run(name, test, ref failures);
    }

    private static bool Selected(string name) => Filter is null || name.Contains(Filter, StringComparison.OrdinalIgnoreCase);
    public static void True(bool value, string? reason = null, [CallerArgumentExpression(nameof(value))] string expression = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (!value)
            throw new InvalidOperationException($"{reason ?? expression} expected=True actual=False at {Path.GetFileName(file)}:{line}");
    }

    public static bool Result(bool value, [CallerArgumentExpression(nameof(value))] string expression = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        True(value, null, expression, file, line);
        return true;
    }

    public static void Equal<T>(T expected, T actual, [CallerArgumentExpression(nameof(actual))] string expression = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{expression} expected={expected} actual={actual} at {Path.GetFileName(file)}:{line}");
    }
}
