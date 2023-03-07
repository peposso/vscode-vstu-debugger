using System.Text;

namespace ImplementGenerator;

sealed class Template
{
    public const string InitialCode = """
// <auto-generated />
#pragma warning disable

sealed class GenerateImplementAttribute : Attribute
{
}
""";
    private Dto dto;

    public Template(Dto dto)
    {
        this.dto = dto;
    }

    public string GenerateCode()
    {
        var sb = new StringBuilder();
        sb.AppendLine($$"""
// <auto-generated />
#pragma warning disable

""");

        if (dto.Namespace != null) sb.AppendLine($$"""
namespace {{dto.Namespace}};
""");

        sb.AppendLine($$"""
partial class {{dto.TypeName}}
{
""");

        foreach (var p in dto.Properties) sb.AppendLine($$"""
    public {{p.Type}} {{p.Name}} => {{dto.Field}}.{{p.Name}};

""");

        foreach (var m in dto.Methods)
        {
            sb.Append($$"""
    public {{m.ReturnType}} {{m.Name}}(
""");
            var first = true;
            foreach (var pr in m.Parameters)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                sb.Append($"{pr.ByRef}{pr.Type} {pr.Name}"); 
            }
            sb.Append($") => {dto.Field}.{m.Name}(");
            first = true;
            foreach (var pr in m.Parameters)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                sb.Append($"{pr.ByRef}{pr.Name}"); 
            }
            sb.AppendLine($$"""
);

""");
        }

        sb.AppendLine($$"""
}
""");
        sb.Replace("\r", "");
        return sb.ToString();
    }
}
