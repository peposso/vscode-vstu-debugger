using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ImplementGenerator;

static class SyntaxHelper
{
    internal static string? GetNamespace(SyntaxNode node)
    {
        var list = new List<string>();
        var unit = null as CompilationUnitSyntax;
        var current = node;
        while (current != null)
        {
            if (current is NamespaceDeclarationSyntax ns)
            {
                list.Add(ns.Name.ToString());
                continue;
            }

            if (current is CompilationUnitSyntax x)
            {
                unit = x;
                break;
            }

            current = current.Parent;
        }

        if (list.Count > 0)
        {
            list.Reverse();
            return string.Join(".", list);
        }

        if (unit == null)
            throw new InvalidOperationException();

        foreach (var child in unit.DescendantNodes())
        {
            if (child is FileScopedNamespaceDeclarationSyntax ns)
            {
                return ns.Name.ToString();
            }
        }

        return null;
    }

    internal static bool HasNamedAttribute(SyntaxList<AttributeListSyntax> attributeLists, string name)
    {
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
            return HasNamedAttributeCore(attributeLists, name)
                || HasNamedAttributeCore(attributeLists, name.Substring(0, name.Length - 9));

        return HasNamedAttributeCore(attributeLists, name)
            || HasNamedAttributeCore(attributeLists, name + "Attribute");

        static bool HasNamedAttributeCore(SyntaxList<AttributeListSyntax> attributeLists, string v)
        {
            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (attribute.Name is IdentifierNameSyntax { Identifier: { ValueText: var name } }
                        && name == v)
                        return true;
                }
            }
            return false;
        }
    }

    internal static TypeSyntax? GetBaseType(SyntaxNode node)
    {
        if (node is not TypeDeclarationSyntax decl)
            return null;

        return decl.BaseList?.Types.FirstOrDefault()?.Type;
    }
}
