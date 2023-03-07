using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#pragma warning disable RS2008

namespace ImplementGenerator;

[Generator(LanguageNames.CSharp)]
public class SourceGenerator : IIncrementalGenerator
{
    const string Namespace = nameof(ImplementGenerator);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        File.WriteAllText("C:\\temp\\_killnethost.bat", $"taskkill /f /pid {Process.GetCurrentProcess().Id}");

        // Log("Initialize");
        context.RegisterPostInitializationOutput(static x => AddInitialCode(x));

        var syntaxes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (x, _) => Dto.Matches(x),
                transform: static (x, token) => (x.Node, Dto.GetSymbol(x, token)))
            .Where(x => x.Item2 != null);

        // .WithComparer(SyntaxEqualityComparer.Default);
        context.RegisterSourceOutput(syntaxes, static (x, y) => AddCode(x, y));
    }

    static void AddInitialCode(IncrementalGeneratorPostInitializationContext context)
    {
        // Log($"AddInitialCode()");
        var token = context.CancellationToken;
        token.ThrowIfCancellationRequested();
        context.AddSource($"{Namespace}.InitialCode.g.cs", Template.InitialCode);
    }

    static void AddCode(SourceProductionContext context, (SyntaxNode, ISymbol) arg)
    {
        var token = context.CancellationToken;
        var (syntax, symbol) = arg;
        token.ThrowIfCancellationRequested();

        var dto = Dto.From(syntax, symbol);
        if (dto != null)
        {
            var template = new Template(dto);
            var source = template.GenerateCode();
            context.AddSource($"{dto.TypeName}.g.cs", source);
        }
    }
}

sealed class SyntaxEqualityComparer : IEqualityComparer<SyntaxNode>
{
    public static SyntaxEqualityComparer Default = new();

    public bool Equals(SyntaxNode x, SyntaxNode y) => x.IsEquivalentTo(y);

    public int GetHashCode(SyntaxNode obj) => obj.GetHashCode();
}
