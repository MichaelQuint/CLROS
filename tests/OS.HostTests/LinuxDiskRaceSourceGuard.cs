using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Fixture-local inspection of active disk-race methods. Discovery is
/// semantic; expected package bytes, ownership and observation order remain
/// independent test inputs. Source order is not guest race qualification.</summary>
internal sealed class LinuxDiskRaceSourceGuard(CSharpSourceCatalog catalog)
{
    internal const string Owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
    internal const string Threads = "OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices";
    internal const string Block = "OS.Storage.ContinuingReadOnlyBlockService";
    internal const string Files = "OS.Storage.ReadOnlyWebRootFileSystem";
    internal const string Gateway = "OS.Kernel.Compatibility.LinuxX64.LinuxSyscallGateway";

    internal MethodDeclarationSyntax Method(string name)
    {
        var method = catalog.Method(Owner, name);
        _ = catalog.MethodInvocations(method); // Includes body diagnostics before any filtering.
        HostCheck.True(!method.DescendantNodes().Any(node => node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax),
            "disk observation cannot hide asserted steps in a nested function");
        return method;
    }

    internal void Calls(string method, string name, string owner = Owner, int? count = null)
    {
        var calls = catalog.MethodInvocations(Method(method)).Where(call => call.TargetMethod.Name == name).ToArray();
        var expected = catalog.Type(owner);
        HostCheck.True(calls.Length != 0 && (count is null || calls.Length == count), method + " calls " + name);
        HostCheck.True(calls.All(call => SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, expected)),
            method + " bound owner of " + name);
    }

    internal bool HasMethod(string name) => catalog.Type(Owner).GetMembers(name).OfType<IMethodSymbol>().Any();

    internal bool PackageMatches(byte[] canonical, bool exit)
    {
        string name = exit ? "IsNativeDiskExitPackage" : "SelectNativeCloseReusePackage";
        var declaration = Method(name);
        var signature = (IMethodSymbol)catalog.Compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration)!;
        HostCheck.True(signature.IsStatic && signature.Arity == 0 && signature.ReturnType.SpecialType == SpecialType.System_Boolean
            && signature.RefKind == RefKind.None && signature.Parameters.Length == (exit ? 1 : 2)
            && signature.Parameters[0].RefKind == RefKind.None
            && signature.Parameters[0].Type is IPointerTypeSymbol pointer
            && SymbolEqualityComparer.Default.Equals(pointer.PointedAtType, catalog.Type(Owner + "+Storage"))
            && (exit || signature.Parameters[1].RefKind == RefKind.Out && signature.Parameters[1].Type.SpecialType == SpecialType.System_Boolean));
        Calls(name, "TryCompute", "OS.Std.Hash.Sha256", 1);
        var digest = System.Security.Cryptography.SHA256.HashData(canonical);
        string Word(int index) => "0x" + System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(digest.AsSpan(index * 8)).ToString("x16");
        string compare = string.Join(exit ? " && " : " || ", Enumerable.Range(0, 4)
            .Select(index => "VAR_digest[" + index + "] " + (exit ? "== " : "!= ") + Word(index)));
        string expected = exit ? $$"""
            {
                if (VAR_state == null || VAR_state->LinuxBytes != {{canonical.Length}} || VAR_state->LinuxPackage == 0) return false;
                ulong* VAR_digest = stackalloc ulong[4];
                return TryCompute((byte*)VAR_state->LinuxPackage, VAR_state->LinuxBytes, (byte*)VAR_digest, 32) && {{compare}};
            }
            """ : $$"""
            {
                VAR_selected = false;
                if (VAR_state == null) return false;
                if (VAR_state->LinuxBytes != {{canonical.Length}}) return true;
                ulong* VAR_digest = stackalloc ulong[4];
                if (VAR_state->LinuxPackage == 0 || !TryCompute((byte*)VAR_state->LinuxPackage, VAR_state->LinuxBytes, (byte*)VAR_digest, 32)) return false;
                if ({{compare}}) return true;
                VAR_selected = true;
                return true;
            }
            """;
        return Matches(Method(name), [expected], exact: true);
    }

    internal void ExitOrdering()
    {
        const string dispatch = "DispatchLinuxNativeCore";
        Calls(dispatch, "PollLinuxSocketNetwork");
        Calls(dispatch, "ObserveNativeDiskExitCall", count: 1);
        Calls(dispatch, "Dispatch", Gateway, 1);
        Ordered(dispatch, "LinuxSyscallFrame VAR_call = *VAR_frame;",
            "if (VAR_call.Number != 231 && !PollLinuxSocketNetwork(VAR_c, preserveClosePending: VAR_pending))",
            "ObserveNativeDiskExitCall(VAR_c, VAR_index, VAR_call)", "Dispatch(ref VAR_c->Gates[VAR_index], ref *VAR_frame)");
        const string transfer = "LinuxNativeTransfer";
        Calls(transfer, "ObserveNativeDiskExitCommit", count: 2);
        Calls(transfer, "CommitExit", Threads, 1);
        Ordered(transfer, "ObserveNativeDiskExitCommit(VAR_c, VAR_index, false)",
            "VAR_c->Process.CommitExit(VAR_c->Key, 2, VAR_worker, out long VAR_tid)",
            "ObserveNativeDiskExitCommit(VAR_c, VAR_index, true)");
        Calls("RetireNativeDisk", "ObserveNativeDiskExitBeforeStop");
        Calls("RetireNativeDisk", "ObserveNativeDiskExitStopped");
        // Stop has two independently required owners, checked in call order.
        RequireStopOwners();
        Ordered("RetireNativeDisk", "ObserveNativeDiskExitBeforeStop(VAR_o)", "Stop(&VAR_o->Device)",
            "ObserveNativeDiskExitStopped(VAR_o, VAR_stopped)", "Stop(&VAR_o->Mount)");
        string[] cleanup = ["RetireNativeDiskAttachment", "ObserveNativeDiskExitSentinel", "LinuxNativeRelease",
            "ObserveNativeDiskExitReleased", "LinuxNativeClearUserMemory", "ObserveNativeDiskExitMemoryRestored",
            "ClearLinuxRandom", "ObserveNativeDiskExitCleanupEnd"];
        foreach (string callee in cleanup)
            Calls("RunLinuxNativeOwner", callee);
        Calls("RunLinuxNativeOwner", "LinuxNativeRsp");
        Calls("RunLinuxNativeOwner", "LinuxText");
        Ordered("RunLinuxNativeOwner", "RetireNativeDiskAttachment(VAR_state, VAR_c)",
            "ObserveNativeDiskExitSentinel(VAR_state, VAR_c)", "LinuxNativeRelease(VAR_c, LinuxNativeRsp(VAR_c))",
            "ObserveNativeDiskExitReleased(VAR_state, VAR_c)", "LinuxNativeClearUserMemory(VAR_state)",
            "ObserveNativeDiskExitMemoryRestored(VAR_state)", "ClearLinuxRandom(VAR_state)",
            "ObserveNativeDiskExitCleanupEnd(VAR_state, VAR_started)", "\"M16_008_BUDGET_END nanos=\"");
        Ordered("ObserveNativeDiskExitCall", "!VAR_o->Device.ReadPending || !VAR_o->Mount.IoActive");
        Calls("NativeDiskExitSentinelBytes", "CopyLinuxAlias", count: 1);
        Ordered("NativeDiskExitSentinelBytes", "for (uint VAR_offset = 0; VAR_offset < 8192; VAR_offset += 128)",
            "CopyLinuxAlias(VAR_state, 0x404000UL + VAR_offset, VAR_chunk, 128, false)");
        Ordered("RejectNativeDiskExitObservation", "\"M16_025_EXIT_REJECT reason=\"");
        Calls("RejectNativeDiskExitObservation", "LinuxText");
        Ordered("ObserveNativeDiskExitCleanupEnd", "VAR_now - VAR_started >= LinuxThreadExecutionBudget.RuntimeNanoseconds");
        foreach (string name in new[] { "IsNativeDiskExitPackage", "ObserveNativeDiskExitCall", "RejectNativeDiskExitObservation",
            "ObserveNativeDiskExitCommit", "ObserveNativeDiskExitBeforeStop", "ObserveNativeDiskExitStopped", "NativeDiskExitSentinelBytes",
            "ObserveNativeDiskExitSentinel", "ObserveNativeDiskExitReleased", "ObserveNativeDiskExitMemoryRestored",
            "ObserveNativeDiskExitCleanupEnd", "WriteNativeDiskExitObservation" })
        {
            NoCalls(name, "Poll");
            NoWrites(name, Block + "+State", "CompletedCount");
        }
    }

    private void RequireStopOwners()
    {
        var calls = catalog.MethodInvocations(Method("RetireNativeDisk")).Where(call => call.TargetMethod.Name == "Stop").ToArray();
        HostCheck.Equal(2, calls.Length);
        HostCheck.True(SymbolEqualityComparer.Default.Equals(calls[0].TargetMethod.ContainingType, catalog.Type(Block)));
        HostCheck.True(SymbolEqualityComparer.Default.Equals(calls[1].TargetMethod.ContainingType, catalog.Type(Files)));
    }

    internal void CloseReuse(int packageBytes)
    {
        const string selector = "SelectNativeCloseReusePoll";
        Calls(selector, "SelectNativeCloseReusePackage", count: 1);
        Calls(selector, "LinuxNativeCallOrdinal", count: 1);
        Ordered(selector, "VAR_index != 1 || VAR_call.Number != 3 || VAR_call.Argument0 != 3 || VAR_c->State->LinuxBytes != " + packageBytes,
            "SelectNativeCloseReusePackage(VAR_c->State, out VAR_selected)", "if (!VAR_selected) return true;",
            "LinuxNativeCallOrdinal(VAR_c, VAR_index) == 4");
        const string pump = "PollNativeCloseReuseDiskReadiness";
        Calls(pump, "PollDiskReadiness", Threads, 1);
        Calls(pump, "RequireNativeCloseReuseParentRead", count: 3);
        Calls(pump, "FlushNativeDiskEvidence", count: 1);
        Calls(pump, "ReadNetworkTime", count: 2);
        Body(pump, """
            {
                if (!RequireNativeCloseReuseParentRead(VAR_c, VAR_disk, false) || !ReadNetworkTime(VAR_c->State, out ulong VAR_started)) return false;
                if (!VAR_disk->Device.ReadPending && !VAR_c->Process.PollDiskReadiness(VAR_c->Key, 2)) return false;
                if (!RequireNativeCloseReuseParentRead(VAR_c, VAR_disk, true)) return false;
                return FlushNativeDiskEvidence(VAR_c->State) && ReadNetworkTime(VAR_c->State, out ulong VAR_finished) && VAR_finished >= VAR_started &&
                    RequireNativeCloseReuseParentRead(VAR_c, VAR_disk, true);
            }
            """);
        NoCalls(pump, "NativeDiskQuantumFlush");
        HostCheck.True(!Method(pump).DescendantNodes().Any(node => node is BaseObjectCreationExpressionSyntax));
        const string dispatch = "DispatchLinuxNativeCore";
        Calls(dispatch, selector, count: 1);
        Calls(dispatch, "PollLinuxSocketNetwork", count: 1);
        Calls(dispatch, "RequireNativeCloseReuseParentRead", count: 1);
        Calls(dispatch, "Dispatch", Gateway, 1);
        Reference(dispatch, "ReadLinuxClock");
        Ordered(dispatch, "delegate* unmanaged<void*, ulong*, byte> VAR_clock = &ReadLinuxClock;",
            "SelectNativeCloseReusePoll(VAR_c, VAR_index, VAR_call, out bool VAR_pending)",
            "PollLinuxSocketNetwork(VAR_c, preserveClosePending: VAR_pending)",
            "VAR_clock(VAR_state, &VAR_admitted)", "RequireNativeCloseReuseParentRead(VAR_c, VAR_disk, true)",
            "Dispatch(ref VAR_c->Gates[VAR_index], ref *VAR_frame)");
        const string observed = "ObserveNativeCloseReuseCompletionTime";
        Calls(observed, "SelectNativeCloseReusePackage", count: 1);
        Calls(observed, "FlushNativeDiskEvidence", count: 1);
        Calls(observed, "ReadNetworkTime", count: 1);
        Calls(observed, "LinuxText");
        Ordered(observed, "SelectNativeCloseReusePackage(VAR_c->State, out bool VAR_selected)", "if (!VAR_selected) return true;",
            "FlushNativeDiskEvidence(VAR_c->State)", "ReadNetworkTime(VAR_c->State, out ulong VAR_now)",
            "VAR_now < VAR_c->Threads.ObservedTicks || VAR_now > (ulong)long.MaxValue", "\"M16_025_COMPLETE_TIME identity=\"");
        NoWrites(observed, "OS.Kernel.Threading.KernelThreadTable", "ObservedTicks");
        const string prepare = "LinuxNativeWorkerPrepare";
        Calls(prepare, "CompleteDiskWait", Threads, 1);
        Calls(prepare, observed, count: 1);
        Calls(prepare, "LinuxText");
        Calls(prepare, "LinuxNumber");
        Ordered(prepare, "VAR_c->Process.CompleteDiskWait(VAR_c->Key, 2)",
            "ObserveNativeCloseReuseCompletionTime(VAR_c, VAR_view.Wait.Sequence, VAR_operation.Generation)",
            "\"M16_025_COMPLETE identity=\"", "LinuxNumber(VAR_c->State, VAR_c->Threads.ObservedTicks)");
    }

    internal void CloseReuseAbsent()
    {
        foreach (string name in new[] { "SelectNativeCloseReusePoll", "SelectNativeCloseReusePackage", "PollNativeCloseReuseDiskReadiness",
            "ObserveNativeCloseReuseCompletionTime", "RequireNativeCloseReuseParentRead" })
            HostCheck.True(!HasMethod(name), "close-reuse method excluded: " + name);
        foreach (var method in catalog.Type(Owner).GetMembers("PollLinuxSocketNetwork").OfType<IMethodSymbol>())
            HostCheck.Equal(1, method.Parameters.Count(parameter => parameter.Type.SpecialType == SpecialType.System_Boolean));
        foreach (string method in new[] { "DispatchLinuxNativeCore", "LinuxNativeWorkerPrepare" })
            if (HasMethod(method))
                NoCalls(method, "SelectNativeCloseReusePoll", "ObserveNativeCloseReuseCompletionTime", "RequireNativeCloseReuseParentRead");
    }

    internal void NoCalls(string method, params string[] names)
        => HostCheck.True(!catalog.MethodInvocations(Method(method)).Any(call => names.Contains(call.TargetMethod.Name, StringComparer.Ordinal)));

    private void Reference(string method, string name)
    {
        var selected = Method(method);
        var model = catalog.Compilation.GetSemanticModel(selected.SyntaxTree);
        var symbols = selected.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.Identifier.ValueText == name).Select(identifier => model.GetSymbolInfo(identifier).Symbol).ToArray();
        var expected = catalog.Type(Owner).GetMembers(name).OfType<IMethodSymbol>().Single();
        HostCheck.True(symbols.Length != 0 && symbols.All(symbol => SymbolEqualityComparer.Default.Equals(symbol, expected)), method + " bound reference to " + name);
    }

    internal void Ordered(string method, params string[] patterns)
    {
        var selected = Method(method);
        HostCheck.True(Matches(selected, patterns, exact: false), method + " observation order");
    }

    internal void Body(string method, string expected)
    {
        var selected = Method(method);
        HostCheck.True(Matches(selected, [expected], exact: true), method + " direct body");
    }

    internal void NoWrites(string method, string owner, string member)
    {
        var selected = Method(method);
        var model = catalog.Compilation.GetSemanticModel(selected.SyntaxTree);
        var expected = catalog.Type(owner).GetMembers(member).Single();
        bool changed = false;
        foreach (var node in selected.DescendantNodes())
        {
            var operation = model.GetOperation(node);
            IOperation? target = operation switch
            {
                IAssignmentOperation assignment => assignment.Target,
                IIncrementOrDecrementOperation increment => increment.Target,
                _ => null
            };
            ISymbol? written = target switch
            {
                IFieldReferenceOperation field => field.Field,
                IPropertyReferenceOperation property => property.Property,
                _ => null
            };
            changed |= SymbolEqualityComparer.Default.Equals(written, expected);
        }
        HostCheck.True(!changed, method + " must not write " + member);
    }

    // VAR_name captures an actual local/parameter symbol, not identifier text.
    // Repeated captures must remain that same symbol across ordered patterns.
    // Operators, field names and literal values stay exact. Static invocation
    // qualification is removed only after binding, allowing aliases; callers
    // independently assert the callee's containing type with Calls.
    private bool Matches(MethodDeclarationSyntax method, string[] patterns, bool exact)
    {
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var calls = catalog.MethodInvocations(method).Where(call => call.TargetMethod.IsStatic)
            .ToDictionary(call => ((InvocationExpressionSyntax)call.Syntax).Expression.SpanStart);
        var source = new List<(SyntaxToken Token, ISymbol? Variable)>();
        int skipUntil = -1;
        foreach (var token in method.Body!.DescendantTokens())
        {
            if (token.SpanStart < skipUntil || OptionalBrace(token))
                continue;
            if (calls.TryGetValue(token.SpanStart, out var invocation))
            {
                source.Add((SyntaxFactory.Identifier(invocation.TargetMethod.Name), null));
                skipUntil = ((InvocationExpressionSyntax)invocation.Syntax).Expression.Span.End;
                continue;
            }
            ISymbol? symbol = token.Parent is IdentifierNameSyntax identifier ? model.GetSymbolInfo(identifier).Symbol
                : token.Parent is VariableDeclaratorSyntax variable ? model.GetDeclaredSymbol(variable)
                : token.Parent is SingleVariableDesignationSyntax designation ? model.GetDeclaredSymbol(designation)
                : null;
            source.Add((token, symbol is ILocalSymbol or IParameterSymbol ? symbol : null));
        }
        var captured = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
        int next = 0;
        foreach (string pattern in patterns)
        {
            var expected = SyntaxFactory.ParseTokens(pattern).Where(token => !token.IsKind(SyntaxKind.EndOfFileToken)).ToArray();
            bool found = false;
            for (int start = next; start + expected.Length <= source.Count; start++)
            {
                if (exact && (start != 0 || expected.Length != source.Count))
                    break;
                var candidate = new Dictionary<string, ISymbol>(captured, StringComparer.Ordinal);
                bool match = true;
                for (int index = 0; index < expected.Length && match; index++)
                {
                    var actual = source[start + index];
                    var wanted = expected[index];
                    if (wanted.IsKind(SyntaxKind.IdentifierToken) && wanted.ValueText.StartsWith("VAR_", StringComparison.Ordinal))
                    {
                        match = actual.Variable is not null;
                        if (match && candidate.TryGetValue(wanted.ValueText, out var prior))
                            match = SymbolEqualityComparer.Default.Equals(prior, actual.Variable);
                        else if (match)
                            candidate.Add(wanted.ValueText, actual.Variable!);
                    }
                    else
                        match = (wanted.RawKind == actual.Token.RawKind
                        || wanted.IsKind(SyntaxKind.IdentifierToken)
                            && SyntaxFacts.GetContextualKeywordKind(wanted.ValueText) == actual.Token.Kind())
                        && SameValue(wanted, actual.Token);
                }
                if (!match)
                    continue;
                captured = candidate;
                next = start + expected.Length;
                found = true;
                break;
            }
            if (!found)
                return false;
        }
        return true;
    }

    private static bool OptionalBrace(SyntaxToken token) =>
        token.IsKind(SyntaxKind.OpenBraceToken) || token.IsKind(SyntaxKind.CloseBraceToken)
            ? token.Parent is BlockSyntax { Statements.Count: 1, Parent: IfStatementSyntax or ElseClauseSyntax or ForStatementSyntax }
            : false;

    private static bool SameValue(SyntaxToken left, SyntaxToken right) =>
        left.IsKind(SyntaxKind.NumericLiteralToken)
            ? Convert.ToDecimal(left.Value, System.Globalization.CultureInfo.InvariantCulture)
                == Convert.ToDecimal(right.Value, System.Globalization.CultureInfo.InvariantCulture)
            : left.ValueText == right.ValueText;
}
