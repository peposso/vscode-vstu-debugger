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
        context.RegisterPostInitializationOutput(static x => AddInitialSource(x));

        var syntaxes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (x, _) => Core.Predicate(x),
                transform: static (x, token) => (x.Node, Core.Transform(x, token)!))
            .Where(x => x.Item2 != null);

        context.RegisterSourceOutput(syntaxes, static (x, y) => AddGeneratedSource(x, y));
    }

    static void AddInitialSource(IncrementalGeneratorPostInitializationContext context)
    {
        var token = context.CancellationToken;
        token.ThrowIfCancellationRequested();
        context.AddSource($"{Namespace}.Initial.g.cs", Template.InitialCode);
    }

    static void AddGeneratedSource(SourceProductionContext context, (SyntaxNode, ISymbol) arg)
    {
        var token = context.CancellationToken;
        var (syntax, symbol) = arg;
        token.ThrowIfCancellationRequested();

        var dto = Core.CreateDto(syntax, symbol);
        if (dto != null)
        {
            var template = new Template(dto);
            var source = template.GenerateCode();
            context.AddSource($"{dto.TypeName}.g.cs", source);
        }
    }
}
