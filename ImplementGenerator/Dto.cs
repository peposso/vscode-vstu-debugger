using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ImplementGenerator;

public class Dto
{
    public PropertyDto[] Properties { get; private set; } = Array.Empty<PropertyDto>();

    public MethodDto[] Methods { get; private set; } = Array.Empty<MethodDto>();
    public string? Namespace { get; private set; }
    public string TypeName { get; private set; } = "";
    public string Field { get; private set; } = "field";

    internal static bool Matches(SyntaxNode x)
    {
        return x is ClassDeclarationSyntax { AttributeLists.Count: > 0 } decl
                && SyntaxHelper.HasNamedAttribute(decl.AttributeLists, "GenerateImplement");
    }

    internal static ITypeSymbol GetSymbol(GeneratorSyntaxContext x, CancellationToken token)
    {
        if (x.Node is not ClassDeclarationSyntax clsDecl)
            throw new InvalidOperationException();

        var baseType = clsDecl.BaseList?.Types.FirstOrDefault()?.Type;
        if (baseType is null)
            throw new InvalidOperationException();

        var info = x.SemanticModel.GetTypeInfo(baseType, token)!;
        return info.Type!;
    }

    public static Dto? From(SyntaxNode syntax, ISymbol symbol)
    {
        // File.WriteAllText("C:\\temp\\debug.txt", $"ProcessId={Process.GetCurrentProcess().Id}, ThreadId={Thread.CurrentThread.ManagedThreadId}");
        // for (var i = 0; i < 1000; ++i)
        // {
        //     if (Debugger.IsAttached) break;
        //     Thread.Sleep(100);
        // }

        var s = (ITypeSymbol)symbol;
        var dto = new Dto();
        if (syntax is not ClassDeclarationSyntax clsDecl)
            return null;

        var baseType = clsDecl.BaseList?.Types.FirstOrDefault()?.Type;
        if (baseType is null)
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
                var propDto = new PropertyDto
                {
                    Name = p.Name,
                    Type = p.Type.ToDisplayString()
                };
                props.Add(propDto);
            }
            else if (m is IMethodSymbol mt && mt.MethodKind == MethodKind.Ordinary)
            {
                var methodDto = new MethodDto
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
                    }).ToArray()
                };
                methods.Add(methodDto);
            }
        }

        dto.Properties = props.ToArray();
        dto.Methods = methods.ToArray();
        return dto;
    }
}

public sealed class PropertyDto
{
    public string Name { get; internal set; } = "";
    public string Type { get; internal set; } = "";
}

public sealed class MethodDto
{
    public string Name { get; internal set; } = "";
    public string ReturnType { get; internal set; } = "";
    public ParameterDto[] Parameters { get; internal set; } = Array.Empty<ParameterDto>();
}

public class ParameterDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string ByRef { get; set; } = "";
}
