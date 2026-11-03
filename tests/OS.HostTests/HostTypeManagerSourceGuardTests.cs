using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

internal static class HostTypeManagerSourceGuardTests
{
    [HostTest("type manager source guards follow partials aliases and locals while retaining image and storage authority")]
    private static void BoundSourceControls()
    {
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(
            ["--EnableM01LoadedImageStackProbe", "--EnableM04TypeManagerModuleRegistrationProbe"], true).Profile());
        bool Check(CSharpSourceCatalog catalog)
        {
            try
            {
                return HostTypeManagerSourceGuard.OriginalImage(catalog) && HostTypeManagerSourceGuard.RegisteredStorage(catalog);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        HostCheck.True(Check(source), "actual admitted source");
        var execute = source.Method(HostTypeManagerSourceGuard.Probe, "Execute");
        var model = source.Compilation.GetSemanticModel(execute.SyntaxTree);

        // Rename bound locals only; member names and parameter identities remain
        // independent contracts. No text replacement chooses an unrelated symbol.
        var names = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        foreach (var node in execute.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            if (model.GetDeclaredSymbol(node) is ILocalSymbol local)
                names.Add(local, "renamed" + names.Count);
        var replacements = new Dictionary<SyntaxToken, string>();
        foreach (var token in execute.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)))
        {
            var symbol = token.Parent is VariableDeclaratorSyntax declaration ? model.GetDeclaredSymbol(declaration)
                : token.Parent is IdentifierNameSyntax identifier ? model.GetSymbolInfo(identifier).Symbol : null;
            if (symbol is not null && names.TryGetValue(symbol, out string? name))
                replacements.Add(token, name);
        }
        var renamed = execute.ReplaceTokens(replacements.Keys,
            (before, _) => SyntaxFactory.Identifier(before.LeadingTrivia, replacements[before], before.TrailingTrivia));
        HostCheck.True(Check(Change(execute, renamed)), "renamed locals");

        var document = source.Document(execute);
        string aliasText = "using OriginalModule = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;\n"
            + document.Text.Replace("TypeManagerModuleMetadataLiveNativeAOTImage.", "OriginalModule.", StringComparison.Ordinal);
        var alias = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(aliasText),
            (CSharpParseOptions)document.Syntax.Options);
        HostCheck.True(Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? alias : item))), "bound alias");
        var colocated = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(document.Text
            + "\ninternal static class UnrelatedGuardControl { internal const string Text = \"unrelated\"; }\n"),
            (CSharpParseOptions)document.Syntax.Options);
        HostCheck.True(Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? colocated : item))),
            "unrelated colocated type does not change probe ownership");

        var capture = source.Method(HostTypeManagerSourceGuard.Adapter, "TryGetOriginalTypeManagerImage");
        var captureDocument = source.Document(capture);
        var root = captureDocument.Syntax.GetRoot();
        var movedRoot = root.RemoveNode(capture, SyntaxRemoveOptions.KeepExteriorTrivia)!;
        var remaining = Replace(captureDocument, movedRoot);
        string imports = string.Join("\n", ((CompilationUnitSyntax)root).Usings
            .Select(item => item.WithoutTrivia().NormalizeWhitespace().ToFullString()));
        string movedText = imports + "\nnamespace OS.Boot; public static unsafe partial class EfiEntry { "
            + "private static partial class M01StageOneAdapter { "
            + capture.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString() + " } }";
        string movedPath = Path.Combine(Path.GetDirectoryName(captureDocument.Path)!, "guard-moved-image-control.cs");
        HostCheck.True(source.Documents.All(item => item.Path != movedPath), "fresh in-memory source path");
        var moved = CSharpSourceDocument.FromBytes(movedPath, System.Text.Encoding.UTF8.GetBytes(movedText),
            (CSharpParseOptions)capture.SyntaxTree.Options);
        HostCheck.True(Check(Rebuild(source.Documents.Select(item => item.Path == captureDocument.Path ? remaining : item).Append(moved))),
            "method moved into another partial");

        var captureModel = source.Compilation.GetSemanticModel(capture.SyntaxTree);
        var captureParameter = captureModel.GetDeclaredSymbol(capture)!.Parameters[0];
        var assignment = capture.DescendantNodes().OfType<AssignmentExpressionSyntax>().Last(syntax =>
            captureModel.GetOperation(syntax) is ISimpleAssignmentOperation operation
            && HostSourceOperations.IsParameter(operation.Target, captureParameter));
        HostCheck.True(!Check(Change(capture, capture.ReplaceNode(assignment,
            assignment.WithRight(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression))))), "fabricated capture");
        var captureParameters = captureModel.GetDeclaredSymbol(capture)!.Parameters;
        var finalWrites = captureParameters.Select(parameter => capture.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Last(syntax => captureModel.GetOperation(syntax) is ISimpleAssignmentOperation operation
                && HostSourceOperations.IsParameter(operation.Target, parameter)).Parent!).ToArray();
        HostCheck.True(!Check(Change(capture, capture.ReplaceNodes(finalWrites, (before, _) =>
            SyntaxFactory.IfStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression), (StatementSyntax)before)))),
            "dormant final capture writes");
        var captureBody = capture.Body!;
        HostCheck.True(!Check(Change(capture, capture.WithBody(captureBody.WithStatements(captureBody.Statements.InsertRange(
            captureBody.Statements.Count - 1,
            [
                SyntaxFactory.ParseStatement("ref " + captureParameter.Type.ToDisplayString() + " captureAlias = ref " + captureParameter.Name + ";"),
                SyntaxFactory.ParseStatement("captureAlias = default;")
            ]))))), "capture changed through ref alias after final assignment");

        var continuation = source.Method(HostTypeManagerSourceGuard.Adapter, "M01StageOneContinuation");
        var run = source.MethodInvocations(continuation).Single(call => call.TargetMethod.Name == "Run"
            && call.TargetMethod.ContainingType.ToDisplayString() == HostTypeManagerSourceGuard.Probe);
        var runSyntax = (InvocationExpressionSyntax)run.Syntax;
        HostCheck.True(!Check(Change(continuation, continuation.ReplaceNode(runSyntax,
            runSyntax.WithArgumentList(runSyntax.ArgumentList.WithArguments(runSyntax.ArgumentList.Arguments.Replace(
                runSyntax.ArgumentList.Arguments[0], runSyntax.ArgumentList.Arguments[0]
                    .WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression)))))))), "substituted caller input");
        var originalCapture = source.MethodInvocations(continuation).Single(call =>
            call.TargetMethod.Name == "TryGetOriginalTypeManagerImage");
        var output = (ILocalReferenceOperation)((IDeclarationExpressionOperation)HostSourceOperations.Argument(originalCapture, 0)).Expression;
        var captureStatement = originalCapture.Syntax.Ancestors().OfType<StatementSyntax>().First();
        var callerBlock = (BlockSyntax)captureStatement.Parent!;
        var mutatedCaller = callerBlock.WithStatements(callerBlock.Statements.InsertRange(
            callerBlock.Statements.IndexOf(captureStatement) + 1,
            [
                SyntaxFactory.ParseStatement("AlterCapture(ref " + output.Local.Name + ");"),
                SyntaxFactory.ParseStatement("static void AlterCapture(ref " + output.Local.Type.ToDisplayString()
                    + " value) { value = default; }")
            ]));
        HostCheck.True(!Check(Change(continuation, continuation.ReplaceNode(callerBlock, mutatedCaller))),
            "mutable capture escape before consumption");

        var shortRegistration = source.MethodInvocations(execute).Single(call => call.TargetMethod.Name == "RegisterOnce"
            && HostSourceOperations.Argument(call, 3) is IBinaryOperation { OperatorKind: BinaryOperatorKind.Subtract });
        var shortSyntax = (InvocationExpressionSyntax)shortRegistration.Syntax;
        var capacity = SyntaxFactory.IdentifierName(model.GetDeclaredSymbol(execute)!.Parameters[3].Name);
        HostCheck.True(!Check(Change(execute, execute.ReplaceNode(shortSyntax.ArgumentList.Arguments[3].Expression, capacity))),
            "short storage control removed");

        var fullRegistration = source.MethodInvocations(execute).Single(call => call.TargetMethod.Name == "RegisterOnce"
            && HostSourceOperations.IsParameter(HostSourceOperations.Argument(call, 3), model.GetDeclaredSymbol(execute)!.Parameters[3])
            && call.Syntax.Parent is EqualsValueClauseSyntax);
        var registeredDeclaration = (VariableDeclaratorSyntax)fullRegistration.Syntax.Parent!.Parent!;
        var registered = (ILocalSymbol)model.GetDeclaredSymbol(registeredDeclaration)!;
        var consumer = source.MethodInvocations(execute).Single(call => call.TargetMethod.Name == "TryGetSection"
            && call.Arguments.Last().Value.ConstantValue is { HasValue: true, Value: { } value } && Convert.ToUInt64(value) == 202);
        var consumerSyntax = (InvocationExpressionSyntax)consumer.Syntax;
        HostCheck.True(!Check(Change(execute, execute.ReplaceNode(consumerSyntax.ArgumentList.Arguments[4].Expression,
            SyntaxFactory.ParseExpression(registered.Name + ".Handle")))), "consumer bypasses published slot");
        var slotRead = source.MethodInvocations(execute).Single(call => call.TargetMethod.Name == "ReadWord"
            && call.Syntax.Parent is EqualsValueClauseSyntax
            && HostSourceOperations.Argument(call, 0) is IPropertyReferenceOperation { Property.Name: "Section" });
        var slotReference = (ILocalReferenceOperation)((IPropertyReferenceOperation)HostSourceOperations.Argument(slotRead, 0)).Instance!;
        var readStatement = slotRead.Syntax.Ancestors().OfType<StatementSyntax>().First();
        var readBlock = (BlockSyntax)readStatement.Parent!;
        HostCheck.True(!Check(Change(execute, execute.ReplaceNode(readBlock, readBlock.WithStatements(readBlock.Statements.Insert(
            readBlock.Statements.IndexOf(readStatement), SyntaxFactory.ParseStatement(slotReference.Local.Name + " = default;")))))),
            "published slot replaced before read");
        HostCheck.True(!Check(Change(execute, execute.ReplaceNode(readBlock, readBlock.WithStatements(readBlock.Statements.InsertRange(
            readBlock.Statements.IndexOf(readStatement),
            [
                SyntaxFactory.ParseStatement("ref " + slotReference.Local.Type.ToDisplayString() + " slotAlias = ref " + slotReference.Local.Name + ";"),
                SyntaxFactory.ParseStatement("slotAlias = default;")
            ]))))), "published slot changed through ref alias");

        var guard = execute.Body!.Statements.OfType<IfStatementSyntax>().Single(branch => branch.Condition.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>().Any(member => member.Name.Identifier.ValueText == "Succeeded"
                && model.GetSymbolInfo(member.Expression).Symbol is ILocalSymbol local
                && SymbolEqualityComparer.Default.Equals(local, registered)));
        HostCheck.True(!Check(Change(execute, execute.ReplaceNode(guard,
            guard.WithCondition(SyntaxFactory.ParseExpression("(" + guard.Condition + ") && false"))))), "dormant registration rejection");
        var statements = execute.Body.Statements.Remove(guard);
        var canary = source.MethodInvocations(execute).Single(call => call.TargetMethod.Name == "Canary");
        var loop = canary.Syntax.Ancestors().OfType<ForStatementSyntax>().First();
        int loopIndex = execute.Body.Statements.IndexOf(loop);
        int guardIndex = execute.Body.Statements.IndexOf(guard);
        int afterLoop = loopIndex + 1 - (guardIndex < loopIndex ? 1 : 0);
        HostCheck.True(afterLoop > 0);
        HostCheck.True(!Check(Change(execute, execute.WithBody(execute.Body.WithStatements(statements.Insert(afterLoop, guard))))),
            "canary before admission");

        var readWord = source.Method(HostTypeManagerSourceGuard.Probe, "ReadWord");
        HostCheck.True(!Check(Change(readWord, readWord.WithAttributeLists(default))), "no-inline boundary removed");
        HostCheck.True(!Check(Change(execute, execute.WithBody(execute.Body.WithStatements(execute.Body.Statements.Insert(0,
            SyntaxFactory.ParseStatement("string hiddenAllocation = null;")))))), "managed reference introduced");

        CSharpSourceCatalog Change(MethodDeclarationSyntax before, MethodDeclarationSyntax after)
        {
            var original = source.Document(before);
            var changed = Replace(original, original.Syntax.GetRoot().ReplaceNode(before, after));
            return Rebuild(source.Documents.Select(item => item.Path == original.Path ? changed : item));
        }
        CSharpSourceDocument Replace(CSharpSourceDocument original, SyntaxNode changed) =>
            new(original.Path, "in-memory guard control", CSharpSyntaxTree.Create((CompilationUnitSyntax)changed,
                (CSharpParseOptions)original.Syntax.Options, original.Path));
        CSharpSourceCatalog Rebuild(IEnumerable<CSharpSourceDocument> documents) =>
            new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }
}
