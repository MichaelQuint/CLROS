using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Discovers the allocator owner's complete partial family in the real
/// EFI profile. Mutations change captured syntax only; repository inputs remain
/// untouched and the independent admission controls still select their profiles.</summary>
internal sealed class EfiAllocatorOwnershipSources
{
    private const string Owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
    private readonly CSharpSourceCatalog catalog;
    internal IReadOnlyDictionary<string, string> Sources
    {
        get;
    }

    internal EfiAllocatorOwnershipSources(EfiProfile profile)
    {
        catalog = HostSourceCatalog.Efi(profile);
        string[][] recipes = [M05BarDmaEfiIntegrationTests.Flags, M05VirtqueuePollingEfiIntegrationTests.Flags,
            M05VirtqueueInterruptEfiIntegrationTests.Flags, M06VirtioBlockQueueSetupEfiIntegrationTests.Flags,
            M06VirtioBlockReadEfiIntegrationTests.Flags, M06LiveBlockContractEfiIntegrationTests.Flags,
            M06GptPartitionEfiIntegrationTests.Flags, M06FatReadEfiIntegrationTests.Flags, M06PackageReadEfiIntegrationTests.Flags,
            M07ArpReceiveEfiIntegrationTests.Flags.Where(flag => flag != "--EnableM07ArpReceiveProbe").ToArray(), M07ArpReceiveEfiIntegrationTests.Flags,
            ["--EnableM09004Probe"], ["--EnableM09005Probe"]];
        var profiles = recipes.Select(arguments => EfiOptions.Parse(arguments, build: false).Profile()).ToArray();
        foreach (string symbol in EfiContracts.OwnedDmaAllocatorProfiles.Select(item => item.Symbol).Concat(["M09_004_PROBE", "M09_005_PROBE"]))
        {
            string flag = EfiContracts.ProbeSymbols.Single(pair => pair.Value == symbol).Key;
            if (!profiles.Any(selected => selected.Flags[flag]))
                throw new InvalidOperationException("allocator_control_profile_missing " + symbol);
        }
        Dictionary<string, string> captured = new(StringComparer.Ordinal);
        foreach (var selected in profiles.Append(profile))
            foreach (var document in HostSourceCatalog.Efi(selected).TypeSources(Owner))
            {
                if (captured.TryGetValue(document.Path, out string? previous) && previous != document.Text)
                    throw new InvalidOperationException("allocator_control_source_changed " + document.Path);
                captured[document.Path] = document.Text;
            }
        Sources = captured;
    }

    internal IReadOnlyDictionary<string, string> ChangeEpoch(string caller, ulong expected, ulong changed)
    {
        var invocation = Call(caller, "RunBlockReadCore");
        if (invocation.ArgumentList.Arguments[4].Expression is not BinaryExpressionSyntax binary
            || !binary.IsKind(SyntaxKind.AddExpression)
            || catalog.Compilation.GetSemanticModel(binary.SyntaxTree).GetConstantValue(binary.Right)
                is not { HasValue: true, Value: ulong actual } || actual != expected)
            throw new InvalidOperationException("allocator_control_epoch_target");
        return Replace(binary.Right, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(changed)));
    }

    internal IReadOnlyDictionary<string, string> ChangePurpose(string caller, int ordinal, bool expected, bool changed)
    {
        var argument = Call(caller, "RunBlockReadCore").ArgumentList.Arguments[ordinal].Expression;
        if (catalog.Compilation.GetSemanticModel(argument.SyntaxTree).GetConstantValue(argument)
            is not { HasValue: true, Value: bool actual } || actual != expected)
            throw new InvalidOperationException("allocator_control_purpose_target");
        return Replace(argument, SyntaxFactory.LiteralExpression(changed ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression));
    }

    internal IReadOnlyDictionary<string, string> RenameMethod(string method)
    {
        var declaration = catalog.Method(Owner, method);
        return Replace(declaration, declaration.WithIdentifier(SyntaxFactory.Identifier("Unreviewed" + method).WithTriviaFrom(declaration.Identifier)));
    }

    internal IReadOnlyDictionary<string, string> RedirectCall(string caller, string target)
    {
        var invocation = Call(caller, target);
        return Replace(invocation.Expression, SyntaxFactory.IdentifierName("Unreviewed" + target));
    }

    internal IReadOnlyDictionary<string, string> SwapArguments(string caller, string target, int first, int second)
    {
        var invocation = Call(caller, target);
        var arguments = invocation.ArgumentList.Arguments.ToArray();
        (arguments[first], arguments[second]) = (arguments[second], arguments[first]);
        return Replace(invocation, invocation.WithArgumentList(invocation.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(arguments))));
    }

    internal IReadOnlyDictionary<string, string> AddContractPurpose()
    {
        var invocation = Call("RunOwnedVirtioBlockRead", "RunBlockReadCore");
        if (invocation.ArgumentList.Arguments.Count != 1)
            throw new InvalidOperationException("allocator_control_original_read_target");
        return Replace(invocation, invocation.AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))));
    }

    internal IReadOnlyDictionary<string, string> ChangeCooperativeReceipt()
    {
        var allocator = catalog.Compilation.GetTypeByMetadataName("OS.Kernel.Memory.BootPageAllocatorMutator");
        var invocation = (InvocationExpressionSyntax)catalog.MethodInvocations(catalog.Method(Owner, "RunCooperativeSwitch"))
            .Single(call => call.TargetMethod.Name == "FreeOnePage"
                && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, allocator)).Syntax;
        if (invocation.ArgumentList.Arguments.Last().Expression is not ElementAccessExpressionSyntax element
            || element.ArgumentList.Arguments.Count != 1
            || element.ArgumentList.Arguments[0].Expression is not BinaryExpressionSyntax index || !index.IsKind(SyntaxKind.SubtractExpression))
            throw new InvalidOperationException("allocator_control_receipt_target");
        return Replace(index, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));
    }

    internal IReadOnlyDictionary<string, string> MoveMethod(string name, string symbol)
    {
        var method = catalog.Method(Owner, name);
        var root = (CompilationUnitSyntax)method.SyntaxTree.GetRoot();
        string movedPath = method.SyntaxTree.FilePath + ".moved-" + name;
        var result = new Dictionary<string, string>(Sources, StringComparer.Ordinal);
        result[method.SyntaxTree.FilePath] = root.RemoveNode(method, SyntaxRemoveOptions.KeepExteriorTrivia)!.ToFullString();
        string imports = string.Join("\n", root.Usings.Select(directive => directive.WithoutTrivia().ToFullString()));
        result.Add(movedPath, imports + "\n#if " + symbol + "\nnamespace OS.Boot;\npublic static unsafe partial class EfiEntry\n{\n"
            + "private static partial class M03FirstExitAdapter\n{\n" + method.WithoutTrivia().ToFullString() + "\n}\n}\n#endif\n");
        return result.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    internal IReadOnlyDictionary<string, string> ChangeCooperativeMaterializerFailureReturn()
    {
        var method = catalog.Method(Owner, "RunCooperativeSwitch");
        var materializer = catalog.Compilation.GetTypeByMetadataName("OS.Kernel.Memory.BootPageAllocatorStateMaterializer");
        var invocation = (InvocationExpressionSyntax)catalog.MethodInvocations(method)
            .Single(call => call.TargetMethod.Name == "Materialize"
                && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, materializer)).Syntax;
        var branch = invocation.Ancestors().OfType<IfStatementSyntax>().First();
        var expected = SyntaxFactory.ParseStatement("{ _ = CooperativeMarker(state, 3); return false; }");
        if (branch.Statement is not BlockSyntax block || !SyntaxFactory.AreEquivalent(block, expected))
            throw new InvalidOperationException("allocator_control_materializer_failure_arm");
        var returned = block.Statements.OfType<ReturnStatementSyntax>().Single();
        return Replace(returned, returned.WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)));
    }

    private InvocationExpressionSyntax Call(string caller, string target)
    {
        var owner = catalog.Type(Owner);
        return (InvocationExpressionSyntax)catalog.MethodInvocations(catalog.Method(Owner, caller))
            .Single(call => call.TargetMethod.Name == target
                && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, owner)).Syntax;
    }

    private IReadOnlyDictionary<string, string> Replace(SyntaxNode node, SyntaxNode replacement)
    {
        var result = new Dictionary<string, string>(Sources, StringComparer.Ordinal);
        result[node.SyntaxTree.FilePath] = node.SyntaxTree.GetRoot().ReplaceNode(node, replacement.WithTriviaFrom(node)).ToFullString();
        return result;
    }
}
