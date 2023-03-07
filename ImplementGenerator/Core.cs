using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ImplementGenerator;

static class Core
{
    internal static bool Predicate(SyntaxNode x)
    {
        return x is ClassDeclarationSyntax { AttributeLists.Count: > 0 } decl
                && SyntaxHelper.HasNamedAttribute(decl.AttributeLists, "GenerateImplement")
                && SyntaxHelper.GetBaseType(decl) is not null;
    }

    internal static ITypeSymbol? Transform(GeneratorSyntaxContext x, CancellationToken token)
    {
        var baseType = SyntaxHelper.GetBaseType(x.Node)!;
        var info = x.SemanticModel.GetTypeInfo(baseType, token);
        return info.Type;
    }

    internal static Dto? CreateDto(SyntaxNode syntax, ISymbol symbol)
    {
        var s = (ITypeSymbol)symbol;
        var dto = new Dto();
        if (syntax is not ClassDeclarationSyntax clsDecl)
            return null;

        var field = clsDecl.DescendantNodes().OfType<FieldDeclarationSyntax>().FirstOrDefault()?.Declaration;
        if (field != null)
        {
            dto.Field = field.Variables[0].Identifier.ValueText;
        }

        dto.Namespace = SyntaxHelper.GetNamespace(clsDecl);
        dto.TypeName = clsDecl.Identifier.ValueText;

        var props = new List<PropertyDto>();
        var methods = new List<MethodDto>();
        foreach (var m in s.GetMembers())
        {
            if (m is IPropertySymbol p)
            {
                props.Add(new()
                {
                    Name = p.Name,
                    Type = p.Type.ToDisplayString()
                });
            }
            else if (m is IMethodSymbol mt && mt.MethodKind == MethodKind.Ordinary)
            {
                methods.Add(new()
                {
                    Name = mt.Name,
                    ReturnType = mt.ReturnType.ToDisplayString(),
                    Parameters = mt.Parameters.Select(x => new ParameterDto
                    {
                        Name = x.Name,
                        Type = x.Type.ToDisplayString(),
                        ByRef = x.RefKind switch
                        {
                            RefKind.None => "",
                            RefKind.Ref => "ref ",
                            RefKind.Out => "out ",
                            _ => throw new InvalidOperationException()
                        }
                    }).ToArray(),
                });
            }
        }

        dto.Properties = props.ToArray();
        dto.Methods = methods.ToArray();
        return dto;
    }
}
