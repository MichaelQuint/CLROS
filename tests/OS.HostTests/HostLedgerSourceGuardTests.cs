using OS.Tooling;

internal static class HostLedgerSourceGuardTests
{
    [HostTest("ledger source guard rejects boxing collections foreign getters and implicit conversions")]
    private static void AllocationBoundary()
    {
        void Check(string statement)
        {
            var catalog = HostSemanticFingerprintTests.Catalog(new Dictionary<string, string>
            {
                ["entry.cs"] = "readonly struct Entry { public Entry(int value) { Value = value; } public int Value { get; } }",
                ["outside.cs"] = "static class Outside { public static int Value => 1; } struct Foreign { public static implicit operator int(Foreign v) => 1; }",
                ["ledger.cs"] = "static class Ledger { public static Entry Get(int index) { " + statement + " return new Entry(index); } }"
            });
            HostLedgerSourceGuard.Validate(catalog, catalog.TypeSources("Ledger").Single(), catalog.Type("Ledger"), [catalog.Type("Entry")]);
        }
        Check("int local = index + 1; index = local;");
        foreach (string statement in new[] { "object boxed = index;", "int[] values = [1];", "int value = Outside.Value;",
            "Foreign value = default; int implicitValue = value;", "System.Action callback = () => {};", "string text = index.ToString();" })
        {
            try
            {
                Check(statement);
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("ledger_", StringComparison.Ordinal))
            {
                continue;
            }
            throw new InvalidOperationException("ledger mutation was accepted: " + statement);
        }
    }

    [HostTest("runtime halt guard rejects returning false-condition and executable-loop bodies")]
    private static void HaltBoundary()
    {
        bool Check(string body)
        {
            var catalog = HostSemanticFingerprintTests.Catalog(new Dictionary<string, string>
            {
                ["moved.cs"] = "namespace System; static class RuntimeFailFast { public static void Halt() " + body + " }"
            });
            return DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests.HaltDoesNotReturn(catalog);
        }
        HostCheck.True(Check("{ while (true) {} }"));
        foreach (string body in new[] { "{}", "{ while(false) {} }", "{ while(true) { break; } }", "{ while(true) { return; } }" })
            HostCheck.True(!Check(body), body);
    }

    [HostTest("ledger source guard rejects opaque owned methods accessors and constructors")]
    private static void OpaqueOwnedImplementations()
    {
        const string internalCall = "[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]";
        const string forwardRef = "[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.ForwardRef)]";
        foreach (var (entry, ledger) in new[]
        {
            ("readonly struct Entry { public Entry(int value) {} }", "static class Ledger { static extern int Value { " + internalCall + " get; } static Entry Get() => new Entry(Value); }"),
            ("readonly struct Entry { " + internalCall + " public extern Entry(int value); }", "static class Ledger { static Entry Get() => new Entry(1); }"),
            ("readonly struct Entry { public Entry(int value) {} }", "static class Ledger { " + internalCall + " static int Value() => 1; static Entry Get() => new Entry(Value()); }"),
            ("readonly struct Entry { public Entry(int value) {} }", "static class Ledger { " + forwardRef + " static int Value() => 1; static Entry Get() => new Entry(Value()); }"),
            ("readonly struct Entry {}", "static class Ledger { " + internalCall + " static Ledger() {} static Entry Get() => new Entry(); }"),
            ("readonly struct Entry { " + internalCall + " static Entry() {} }", "static class Ledger { static Entry Get() => new Entry(); }"),
            ("readonly struct Entry {}", "static class Ledger { [System.Runtime.CompilerServices.ModuleInitializer] " + internalCall + " internal static void Initialize() {} static Entry Get() => new Entry(); }")
        })
        {
            var catalog = HostSemanticFingerprintTests.Catalog(new Dictionary<string, string>
            {
                ["entry.cs"] = entry,
                ["ledger.cs"] = ledger
            });
            try
            {
                foreach (var document in catalog.TypeSources("Ledger").Concat(catalog.TypeSources("Entry")))
                    HostLedgerSourceGuard.Validate(catalog, document, catalog.Type("Ledger"), [catalog.Type("Entry")]);
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("ledger_", StringComparison.Ordinal))
            {
                continue;
            }
            throw new InvalidOperationException("opaque ledger implementation accepted");
        }
    }
}
